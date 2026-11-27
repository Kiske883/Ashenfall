using System.Data;
using System.Text.Json;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Persistence;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Api.Characters;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ashenfall.CatalogPublisher.Balance;

/// <summary>Administration application service. No console, tokens, automatic startup or schema changes.</summary>
public sealed class BalanceManagementService
{
    public const string Target = "localhost:5432/ashenfall_09b3_test";
    public const string LocalConnection = "Host=localhost;Port=5432;Database=ashenfall_09b3_test;Username=postgres;Pooling=false;Timeout=5;Command Timeout=30";
    private readonly string _connection;
    public BalanceManagementService(string connection = LocalConnection)
    {
        V2CatalogPublication.RequireIsolatedConnection(connection); _connection = connection;
    }

    public async Task<BalanceState> InspectAsync(CancellationToken ct = default)
    {
        await using var c = new NpgsqlConnection(_connection); await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await Exec(c, tx, "SET TRANSACTION READ ONLY", ct); await Verify(c, tx, ct);
        var state = await LoadAsync(c, tx, ct); await tx.CommitAsync(ct); return state;
    }
    public async Task<BalancePlan> PlanAsync(Guid operation, string version, IReadOnlyList<BalanceEdit> edits,
        Guid? configuration = null, Guid? character = null, CancellationToken ct = default)
        => BalancePlanner.Plan(await InspectAsync(ct), operation, version, edits, configuration, character);

    public async Task<BalanceApplyResult> ApplyAsync(BalancePlan plan, bool execute, CancellationToken ct = default)
    {
        if (!execute) throw new InvalidOperationException("Explicit publication execution intent is required.");
        await using var c = new NpgsqlConnection(_connection); await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var result = await ApplyInTransactionAsync(c, tx, plan, ct);
            await tx.CommitAsync(ct); return result;
        }
        catch { await tx.RollbackAsync(CancellationToken.None); throw; }
    }

    /// <summary>Caller owns commit. Entire preparation/publication is rolled back to a savepoint on failure.</summary>
    public async Task<BalanceApplyResult> ApplyInTransactionAsync(NpgsqlConnection c, NpgsqlTransaction tx,
        BalancePlan plan, CancellationToken ct = default)
    {
        V2CatalogPublication.RequireIsolatedConnection(c.ConnectionString);
        if (tx.Connection != c || tx.IsolationLevel != IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Apply requires the publisher's READ COMMITTED transaction.");
        string savepoint = "balance_" + Guid.NewGuid().ToString("N"); await tx.SaveAsync(savepoint, ct);
        try
        {
            await Verify(c, tx, ct);
            await Exec(c, tx, "SET LOCAL lock_timeout='8s'; SET LOCAL statement_timeout='30s'; SET LOCAL search_path=public,pg_catalog", ct);
            await Exec(c, tx, "SELECT pg_advisory_xact_lock(@n,@r)", ct, ("n", CatalogPublication.LockNamespace), ("r", CatalogPublication.LockResource));
            await Exec(c, tx, """
                LOCK TABLE public.catalog_releases,public.character_classes,public.enemy_definitions,public.item_definitions,
                public.class_level_rules,public.item_stat_modifiers,public.catalog_v2_manifest,public.catalog_class_successors,
                public.catalog_item_successors,public.player_characters,public.owned_items,public.character_equipment
                IN SHARE ROW EXCLUSIVE MODE
                """, ct);
            var state = await LoadAsync(c, tx, ct);
            var existing = state.Catalogs.SingleOrDefault(x => x.Catalog.Release.Id == plan.OperationId);
            if (existing is not null)
            {
                existing.Verify();
                if (plan.FormatVersion != 1 || existing.Hash != plan.TargetHash || existing.Catalog.Release.VersionKey != plan.Version ||
                    existing.Catalog.Release.Status is not (CatalogStates.Published or CatalogStates.Retired))
                    throw new InvalidOperationException("Operation identity is already used by different content.");
                await tx.ReleaseAsync(savepoint, ct);
                return new BalanceApplyResult("AlreadyApplied (historical operation; no reactivation)", existing.Catalog.Release.Id, existing.Hash,
                    plan.CharacterId is { } oldId && existing == state.Active ? BalancePlanner.Snapshot(state, existing, oldId) : null);
            }
            var candidate = BalancePlanner.Materialize(state, plan);
            if (plan.NoOp)
            {
                await tx.ReleaseAsync(savepoint, ct);
                return new BalanceApplyResult("NoOp", state.Active.Catalog.Release.Id, state.Active.Hash,
                    plan.CharacterId is { } id ? BalancePlanner.Snapshot(state, state.Active, id) : null);
            }
            if (state.Catalogs.Any(x => x.Catalog.Release.VersionKey == plan.Version)) throw new InvalidOperationException("Version already exists.");
            var fingerprint = await Fingerprint(c, tx, ct);
            await Insert(c, tx, candidate, ct);
            // This is the only activation implementation. It repeats all checks on the actual persisted draft under the same locks.
            await V2CatalogPublication.PublishInTransactionAsync(c, tx, candidate.Catalog.Release.Id, ct);
            var after = await LoadAsync(c, tx, ct); after.Active.Verify();
            if (after.Active.Catalog.Release.Id != plan.OperationId || after.Active.Hash != plan.TargetHash ||
                fingerprint != await Fingerprint(c, tx, ct))
                throw new InvalidOperationException("Publication or character preservation verification failed.");
            var snapshot = plan.CharacterId is { } selected ? BalancePlanner.Snapshot(after, after.Active, selected) : null;
            await tx.ReleaseAsync(savepoint, ct);
            return new BalanceApplyResult("Published", after.Active.Catalog.Release.Id, after.Active.Hash, snapshot);
        }
        catch
        {
            await tx.RollbackAsync(savepoint, CancellationToken.None); await tx.ReleaseAsync(savepoint, CancellationToken.None); throw;
        }
    }

    private static async Task Verify(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT current_database()='ashenfall_09b3_test' AND current_user='postgres' AND inet_server_port()=5432
            AND inet_server_addr() IN ('127.0.0.1'::inet,'::1'::inet)
            AND (SELECT system_identifier::text FROM pg_control_system())='7708337984335018212'
            AND (SELECT count(*) FROM pg_trigger WHERE NOT tgisinternal AND tgenabled='O' AND tgname IN
              ('catalog_release_guard','character_class_catalog_guard','enemy_definition_catalog_guard',
               'item_definition_catalog_guard','class_level_rule_catalog_guard','item_modifier_catalog_guard',
               'character_mutation_guard','owned_item_mutation_guard','equipment_mutation_guard',
               'catalog_v2_manifest_guard','catalog_class_successor_guard','catalog_item_successor_guard'))=12
            AND EXISTS (SELECT 1 FROM pg_index WHERE indexrelid='public.catalog_one_published'::regclass AND indisunique AND indisvalid)
            """, c, t);
        if (await cmd.ExecuteScalarAsync(ct) is not true) throw new InvalidOperationException("Database/cluster identity or catalog integrity prerequisites are not authorized.");
    }

    public static async Task<BalanceState> LoadAsync(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct = default)
    {
        V2CatalogPublication.RequireIsolatedConnection(c.ConnectionString); await Verify(c, t, ct);
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(c).Options);
        await using var chars = new CharacterDbContext(new DbContextOptionsBuilder<CharacterDbContext>().UseNpgsql(c).Options);
        await db.Database.UseTransactionAsync(t, ct); await chars.Database.UseTransactionAsync(t, ct);
        var releases = await db.Releases.AsNoTracking().ToListAsync(ct);
        var classes = await db.Classes.AsNoTracking().ToListAsync(ct); var enemies = await db.Enemies.AsNoTracking().ToListAsync(ct);
        var items = await db.Items.AsNoTracking().ToListAsync(ct);
        var combat = new List<CatalogCombatClassV2>(); var levels = new List<CatalogProgressionV2>(); var modifiers = new List<CatalogModifierV2>();
        var classLinks = new List<BalanceLink>(); var itemLinks = new List<BalanceLink>(); var manifests = new Dictionary<Guid, string>();
        async Task Read(string sql, Action<NpgsqlDataReader> accept)
        {
            await using var cmd = new NpgsqlCommand(sql, c, t); await using var rows = await cmd.ExecuteReaderAsync(ct);
            while (await rows.ReadAsync(ct)) accept(rows);
        }
        await Read("SELECT release_id,hash_format,content_sha256 FROM public.catalog_v2_manifest", r => {
            if (r.GetString(1) != CombatCatalogHashV2.Format) throw new InvalidOperationException("Unsupported manifest."); manifests.Add(r.GetGuid(0), r.GetString(2)); });
        await Read("SELECT id,min_damage,max_damage,critical_chance_percent,critical_damage_bonus_percent FROM public.character_classes WHERE min_damage IS NOT NULL", r => combat.Add(new(r.GetGuid(0), r.GetInt32(1), r.GetInt32(2), r.GetDecimal(3), r.GetDecimal(4))));
        await Read("SELECT class_definition_id,level,required_total_xp,health_bonus,damage_bonus,attribute_points,min_damage_bonus,max_damage_bonus,critical_chance_bonus_percent,critical_damage_bonus_percent FROM public.class_level_rules WHERE min_damage_bonus IS NOT NULL", r => levels.Add(new(r.GetGuid(0), r.GetInt32(1), r.GetInt64(2), r.GetInt32(3), r.GetInt32(4), r.GetInt32(5), r.GetInt32(6), r.GetInt32(7), r.GetDecimal(8), r.GetDecimal(9))));
        await Read("SELECT item_definition_id,stat_code,modifier_kind,amount FROM public.item_stat_modifiers", r => modifiers.Add(new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetDecimal(3))));
        await Read("SELECT source_class_id,target_class_id FROM public.catalog_class_successors", r => classLinks.Add(new(r.GetGuid(0), r.GetGuid(1))));
        await Read("SELECT source_item_id,target_item_id FROM public.catalog_item_successors", r => itemLinks.Add(new(r.GetGuid(0), r.GetGuid(1))));
        var state = new BalanceState { Characters = await chars.Characters.AsNoTracking().ToListAsync(ct),
            OwnedItems = await chars.OwnedItems.AsNoTracking().ToListAsync(ct), Equipment = await chars.Equipment.AsNoTracking().ToListAsync(ct) };
        foreach (var release in releases)
        {
            var cs = classes.Where(x => x.CatalogReleaseId == release.Id).ToArray(); var ids = cs.Select(x => x.Id).ToHashSet();
            var its = items.Where(x => x.CatalogReleaseId == release.Id).ToArray(); var itemIds = its.Select(x => x.Id).ToHashSet();
            state.Catalogs.Add(new BalanceCatalog { Catalog = new CatalogSnapshot(release, cs, enemies.Where(x => x.CatalogReleaseId == release.Id).ToArray(), its),
                Combat = combat.Where(x => ids.Contains(x.ClassId)).ToList(), Levels = levels.Where(x => ids.Contains(x.ClassId)).ToList(),
                Modifiers = modifiers.Where(x => itemIds.Contains(x.ItemId)).ToList(), ClassLinks = classLinks.Where(x => ids.Contains(x.TargetId)).ToList(),
                ItemLinks = itemLinks.Where(x => itemIds.Contains(x.TargetId)).ToList(), Hash = manifests.GetValueOrDefault(release.Id, "") });
        }
        return state;
    }

    private static async Task Insert(NpgsqlConnection c, NpgsqlTransaction t, BalanceCatalog data, CancellationToken ct)
    {
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(c).Options);
        await db.Database.UseTransactionAsync(t, ct);
        data.Catalog.Release.CreatedAt = DateTimeOffset.UtcNow;
        db.Releases.Add(data.Catalog.Release); await db.SaveChangesAsync(ct);
        db.Classes.AddRange(data.Catalog.Classes); db.Enemies.AddRange(data.Catalog.Enemies); db.Items.AddRange(data.Catalog.Items);
        await db.SaveChangesAsync(ct);
        foreach (var x in data.Combat)
            await Exec(c,t,"UPDATE public.character_classes SET min_damage=@a,max_damage=@b,critical_chance_percent=@p,critical_damage_bonus_percent=@q WHERE id=@id",ct,
                ("a",x.MinDamage),("b",x.MaxDamage),("p",x.CriticalChancePercent),("q",x.CriticalDamageBonusPercent),("id",x.ClassId));
        foreach (var x in data.Levels)
            await Exec(c,t,"INSERT INTO public.class_level_rules(class_definition_id,level,required_total_xp,health_bonus,damage_bonus,attribute_points,min_damage_bonus,max_damage_bonus,critical_chance_bonus_percent,critical_damage_bonus_percent) VALUES(@id,@l,@xp,@hp,@d,@a,@min,@max,@p,@q)",ct,
                ("id",x.ClassId),("l",x.Level),("xp",x.RequiredTotalXp),("hp",x.HealthBonus),("d",x.DamageBonus),("a",x.AttributePoints),("min",x.MinDamageBonus),("max",x.MaxDamageBonus),("p",x.CriticalChanceBonusPercent),("q",x.CriticalDamageBonusPercent));
        foreach (var x in data.Modifiers)
            await Exec(c,t,"INSERT INTO public.item_stat_modifiers(item_definition_id,stat_code,modifier_kind,amount) VALUES(@id,@s,@k,@a)",ct,("id",x.ItemId),("s",x.StatCode),("k",x.ModifierKind),("a",x.Amount));
        foreach (var x in data.ClassLinks) await Exec(c,t,"INSERT INTO public.catalog_class_successors(source_class_id,target_class_id) VALUES(@s,@t)",ct,("s",x.SourceId),("t",x.TargetId));
        foreach (var x in data.ItemLinks) await Exec(c,t,"INSERT INTO public.catalog_item_successors(source_item_id,target_item_id) VALUES(@s,@t)",ct,("s",x.SourceId),("t",x.TargetId));
        await Exec(c,t,"INSERT INTO public.catalog_v2_manifest(release_id,hash_format,content_sha256) VALUES(@r,@f,@h)",ct,("r",data.Catalog.Release.Id),("f",CombatCatalogHashV2.Format),("h",data.Hash));
    }

    private static async Task<string> Fingerprint(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT json_build_array((SELECT coalesce(json_agg(x ORDER BY id),'[]') FROM public.player_characters x),
              (SELECT coalesce(json_agg(x ORDER BY id),'[]') FROM public.owned_items x),
              (SELECT coalesce(json_agg(x ORDER BY character_id,slot_code),'[]') FROM public.character_equipment x))::text
            """, c,t);
        return (string)(await cmd.ExecuteScalarAsync(ct))!;
    }
    private static async Task Exec(NpgsqlConnection c, NpgsqlTransaction t, string sql, CancellationToken ct, params (string Name,object Value)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql,c,t); foreach (var (name,value) in args) cmd.Parameters.AddWithValue(name,value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
