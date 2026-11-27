using System.Data;
using System.Text.Json;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Persistence;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Api.Characters;
using Ashenfall.CatalogPublisher;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ashenfall.TestCatalogSeeder;

/// <summary>Explicit preparation/activation of the reviewed isolated test catalog; no configurable target.</summary>
internal static class V2TestCatalogActivation
{
    private const string Connection = "Host=localhost;Port=5432;Database=ashenfall_09b3_test;Username=postgres;Pooling=false;Timeout=5;Command Timeout=30";
    private static readonly Guid ReleaseId = Guid.Parse("91000000-0000-0000-0000-000000000001");
    private const string Version = "snapshot-09c3-v2";
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    public static async Task<int> PrepareCommandAsync()
    {
        try
        {
            V2CatalogPublication.RequireIsolatedConnection(Connection);
            await using var c = new NpgsqlConnection(Connection); await c.OpenAsync();
            await using var tx = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await Verify(c, tx);
            await Exec(c, tx, "SET LOCAL lock_timeout='8s'; SET LOCAL statement_timeout='30s'; SET LOCAL search_path=public,pg_catalog");
            await Exec(c, tx, "SELECT pg_advisory_xact_lock(@n,@r)", ("n", CatalogPublication.LockNamespace), ("r", CatalogPublication.LockResource));
            await Exec(c, tx, "LOCK TABLE catalog_releases,character_classes,enemy_definitions,item_definitions,class_level_rules,item_stat_modifiers,catalog_v2_manifest,catalog_class_successors,catalog_item_successors,player_characters,owned_items,character_equipment IN SHARE ROW EXCLUSIVE MODE");
            await using var catalog = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(c).Options);
            await using var characters = new CharacterDbContext(new DbContextOptionsBuilder<CharacterDbContext>().UseNpgsql(c).Options);
            await catalog.Database.UseTransactionAsync(tx); await characters.Database.UseTransactionAsync(tx);
            if (await catalog.Releases.AnyAsync(x => x.Id == ReleaseId || x.VersionKey == Version))
                throw new InvalidOperationException("V2 version already exists; no candidate was overwritten.");
            var previous = await catalog.Releases.AsNoTracking().SingleAsync(x => x.Status == CatalogStates.Published);
            if (previous.Id != Guid.Parse("90000000-0000-0000-0000-000000000001") || previous.VersionKey != "snapshot-09c2a1-test")
                throw new InvalidOperationException("Unexpected source release.");
            var oldClasses = await catalog.Classes.AsNoTracking().Where(x => x.CatalogReleaseId == previous.Id).ToListAsync();
            var oldItems = await catalog.Items.AsNoTracking().Where(x => x.CatalogReleaseId == previous.Id).ToListAsync();
            var oldEnemies = await catalog.Enemies.AsNoTracking().Where(x => x.CatalogReleaseId == previous.Id).ToListAsync();
            var source = new CatalogSnapshot(previous, oldClasses, oldEnemies, oldItems);
            if (previous.ContentSha256 != "a59cb3897f7764923b3e2c41c08d0ae618981e1cd8187835ceb428b6062c8438" || CatalogHash.Compute(source) != previous.ContentSha256)
                throw new InvalidOperationException("Source hash does not match the verified fixture.");
            if (oldClasses.Count != 1 || oldClasses[0].Code != "dwarf_warrior" || oldEnemies.Count != 1 || oldEnemies[0].Code != "fallen_guardian" || oldItems.Count != 4)
                throw new InvalidOperationException("Source definitions differ from the reviewed fixture.");
            var itemIds = new Dictionary<string, Guid> {
                ["guardian_sword"] = Guid.Parse("91000000-0000-0000-0000-000000000011"),
                ["training_battle_axe"] = Guid.Parse("91000000-0000-0000-0000-000000000012"),
                ["test_shield"] = Guid.Parse("91000000-0000-0000-0000-000000000013"),
                ["test_stack"] = Guid.Parse("91000000-0000-0000-0000-000000000015") };
            var release = new CatalogRelease { Id = ReleaseId, VersionKey = Version, SchemaMajor = 1, Status = CatalogStates.Draft, CreatedAt = DateTimeOffset.UtcNow };
            var cls = Clone(oldClasses.Single()); cls.Id = Guid.Parse("91000000-0000-0000-0000-000000000002"); cls.CatalogReleaseId = ReleaseId;
            var enemy = Clone(oldEnemies.Single()); enemy.Id = Guid.Parse("91000000-0000-0000-0000-000000000003"); enemy.CatalogReleaseId = ReleaseId;
            var items = oldItems.Select(Clone).ToArray();
            foreach (var item in items) { item.Id = itemIds[item.Code]; item.CatalogReleaseId = ReleaseId; }
            var classLinks = new[] { (SourceId: oldClasses.Single().Id, TargetId: cls.Id) };
            var itemLinks = oldItems.Select(x => (SourceId: x.Id, TargetId: itemIds[x.Code])).ToArray();
            var originalRules = await characters.LevelRules.AsNoTracking().Where(x => x.ClassDefinitionId == oldClasses[0].Id).OrderBy(x => x.Level).ToListAsync();
            if (originalRules.Count != 2) throw new InvalidOperationException("Unexpected source progression.");
            var levels = originalRules.Select(x => new CatalogProgressionV2(cls.Id, x.Level, x.RequiredTotalXp, x.HealthBonus, x.DamageBonus, x.AttributePoints, 0, 0, 0, 0)).ToArray();
            var oldItemIds = oldItems.Select(x => x.Id).ToArray();
            var originalModifiers = await characters.ItemModifiers.AsNoTracking().Where(x => oldItemIds.Contains(x.ItemDefinitionId)).ToListAsync();
            var modifiers = originalModifiers.Select(x => new CatalogModifierV2(itemLinks.Single(i => i.SourceId == x.ItemDefinitionId).TargetId, x.StatCode, x.ModifierKind, x.Amount)).ToArray();
            var combat = new[] { new CatalogCombatClassV2(cls.Id, cls.FixedDamage, cls.FixedDamage, 5, 100) };
            catalog.Releases.Add(release); await catalog.SaveChangesAsync();
            catalog.Classes.Add(cls); catalog.Enemies.Add(enemy); catalog.Items.AddRange(items); await catalog.SaveChangesAsync();
            await Exec(c, tx, "UPDATE character_classes SET min_damage=@d,max_damage=@d,critical_chance_percent=5,critical_damage_bonus_percent=100 WHERE id=@id", ("d", cls.FixedDamage), ("id", cls.Id));
            foreach (var x in levels)
                await Exec(c, tx, "INSERT INTO class_level_rules(class_definition_id,level,required_total_xp,health_bonus,damage_bonus,attribute_points,min_damage_bonus,max_damage_bonus,critical_chance_bonus_percent,critical_damage_bonus_percent) VALUES(@id,@level,@xp,@hp,@damage,@points,0,0,0,0)", ("id", x.ClassId), ("level", x.Level), ("xp", x.RequiredTotalXp), ("hp", x.HealthBonus), ("damage", x.DamageBonus), ("points", x.AttributePoints));
            foreach (var x in modifiers) await Exec(c, tx, "INSERT INTO item_stat_modifiers VALUES(@id,@stat,@kind,@amount)", ("id", x.ItemId), ("stat", x.StatCode), ("kind", x.ModifierKind), ("amount", x.Amount));
            foreach (var x in classLinks) await Exec(c, tx, "INSERT INTO catalog_class_successors VALUES(@s,@t)", ("s", x.SourceId), ("t", x.TargetId));
            foreach (var x in itemLinks) await Exec(c, tx, "INSERT INTO catalog_item_successors VALUES(@s,@t)", ("s", x.SourceId), ("t", x.TargetId));
            var snapshot = new CatalogSnapshot(release, [cls], [enemy], items);
            var v1Hash = CatalogHash.Compute(snapshot);
            var v2Hash = CombatCatalogHashV2.Compute(snapshot, combat, levels, modifiers, classLinks, itemLinks);
            await Exec(c, tx, "UPDATE catalog_releases SET content_sha256=@hash WHERE id=@id", ("hash", v1Hash), ("id", ReleaseId));
            await Exec(c, tx, "INSERT INTO catalog_v2_manifest VALUES(@id,@format,@hash)", ("id", ReleaseId), ("format", CombatCatalogHashV2.Format), ("hash", v2Hash));
            // Validate persisted data through the exact publisher; undo its lifecycle transition before saving the draft.
            await tx.SaveAsync("validate_real_candidate");
            await V2CatalogPublication.PublishInTransactionAsync(c, tx, ReleaseId);
            await tx.RollbackAsync("validate_real_candidate"); await tx.ReleaseAsync("validate_real_candidate");
            await tx.CommitAsync();
            Console.WriteLine($"Prepared and publisher-validated draft: {ReleaseId}; version={Version}; V1={v1Hash}; V2={v2Hash}");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine($"Candidate preparation failed ({e.GetType().Name}); no partial candidate was committed."); return 1; }
    }

    public static async Task<int> ActivateCommandAsync(Guid candidate)
    {
        if (candidate != ReleaseId) { Console.Error.WriteLine("Only the reviewed isolated V2 candidate is supported."); return 2; }
        try { await V2CatalogPublication.PublishAsync(Connection, candidate); Console.WriteLine($"Published isolated V2 release {candidate} ({Version})."); return 0; }
        catch (Exception e) { Console.Error.WriteLine($"V2 activation failed ({e.GetType().Name}); publication transaction rolled back."); return 1; }
    }

    private static async Task Verify(NpgsqlConnection c, NpgsqlTransaction tx)
    {
        await using var check = new NpgsqlCommand("SELECT current_database()='ashenfall_09b3_test' AND current_user='postgres' AND inet_server_port()=5432 AND (inet_server_addr() <<= '127.0.0.0/8'::inet OR inet_server_addr()='::1'::inet) AND (SELECT system_identifier::text FROM pg_control_system())='7708337984335018212'", c, tx);
        if (await check.ExecuteScalarAsync() is not true) throw new InvalidOperationException("Wrong isolated database or cluster.");
    }
    private static async Task Exec(NpgsqlConnection c, NpgsqlTransaction tx, string sql, params (string Name, object Value)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, c, tx);
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }
}
