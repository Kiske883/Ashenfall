using System.Data;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Persistence;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Api.Characters;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ashenfall.CatalogPublisher;

/// <summary>Explicit isolated-database publisher. Loads, validates and transitions persisted candidates atomically.</summary>
public static class V2CatalogPublication
{
    public static async Task PublishAsync(string connectionString, Guid candidateId, CancellationToken ct = default)
    {
        RequireIsolatedConnection(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await PublishInTransactionAsync(connection, transaction, candidateId, ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>Same publication path, with the caller owning commit/rollback. Failure rolls back this operation's savepoint.</summary>
    public static async Task PublishInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid candidateId, CancellationToken ct = default)
    {
        RequireIsolatedConnection(connection.ConnectionString);
        if (candidateId == Guid.Empty || transaction.Connection != connection || transaction.IsolationLevel != IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Publication requires an explicit candidate and a READ COMMITTED transaction on the supplied connection.");
        var savepoint = "publish_v2_" + Guid.NewGuid().ToString("N");
        await transaction.SaveAsync(savepoint, ct);
        try
        {
            await using (var check = new NpgsqlCommand("""
                SELECT current_database()='ashenfall_09b3_test' AND current_user='postgres'
                  AND inet_server_port()=5432 AND inet_server_addr() IS NOT NULL
                  AND (inet_server_addr() <<= '127.0.0.0/8'::inet OR inet_server_addr()='::1'::inet)
                  AND (SELECT system_identifier::text FROM pg_control_system())='7708337984335018212'
                  AND (SELECT count(*) FROM pg_trigger WHERE NOT tgisinternal AND tgenabled='O' AND tgname IN
                    ('catalog_release_guard','character_class_catalog_guard','enemy_definition_catalog_guard',
                     'item_definition_catalog_guard','class_level_rule_catalog_guard','item_modifier_catalog_guard',
                     'character_mutation_guard','owned_item_mutation_guard','equipment_mutation_guard',
                     'catalog_v2_manifest_guard','catalog_class_successor_guard','catalog_item_successor_guard'))=12
                  AND EXISTS (SELECT 1 FROM pg_index WHERE indexrelid='public.catalog_one_published'::regclass AND indisunique AND indisvalid)
                """, connection, transaction))
                if (await check.ExecuteScalarAsync(ct) is not true) throw new InvalidOperationException("Isolated publication identity or integrity preflight failed.");

            await Exec("SET LOCAL lock_timeout='8s'; SET LOCAL statement_timeout='30s'; SET LOCAL search_path=public,pg_catalog");
            await using (var advisory = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@n,@r)", connection, transaction))
            {
                advisory.Parameters.AddWithValue("n", CatalogPublication.LockNamespace);
                advisory.Parameters.AddWithValue("r", CatalogPublication.LockResource);
                await advisory.ExecuteNonQueryAsync(ct);
            }
            // Block every relevant writer, including inserts; READ COMMITTED reads after this lock see their latest commits.
            // SHARE ROW EXCLUSIVE still permits ordinary SELECT and MVCC snapshot readers.
            await Exec("""
                LOCK TABLE public.catalog_releases, public.character_classes, public.enemy_definitions,
                  public.item_definitions, public.class_level_rules, public.item_stat_modifiers,
                  public.catalog_v2_manifest, public.catalog_class_successors, public.catalog_item_successors,
                  public.player_characters, public.owned_items, public.character_equipment IN SHARE ROW EXCLUSIVE MODE
                """);

            await using var catalog = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).Options);
            await using var characters = new CharacterDbContext(new DbContextOptionsBuilder<CharacterDbContext>().UseNpgsql(connection).Options);
            await catalog.Database.UseTransactionAsync(transaction, ct);
            await characters.Database.UseTransactionAsync(transaction, ct);
            var releases = await catalog.Releases.AsNoTracking().ToListAsync(ct);
            var candidate = releases.SingleOrDefault(x => x.Id == candidateId) ?? throw new InvalidOperationException("Candidate does not exist.");
            if (candidate.Status != CatalogStates.Draft || candidate.PublishedAt is not null)
                throw new InvalidOperationException("Candidate must be an unpublished draft.");
            var previous = releases.SingleOrDefault(x => x.Status == CatalogStates.Published)
                ?? throw new InvalidOperationException("Exactly one previous published release is required.");
            var allClasses = await catalog.Classes.AsNoTracking().ToListAsync(ct);
            var allItems = await catalog.Items.AsNoTracking().ToListAsync(ct);
            var snapshot = new CatalogSnapshot(candidate,
                allClasses.Where(x => x.CatalogReleaseId == candidateId).ToArray(),
                await catalog.Enemies.AsNoTracking().Where(x => x.CatalogReleaseId == candidateId).ToListAsync(ct),
                allItems.Where(x => x.CatalogReleaseId == candidateId).ToArray());
            if (CatalogValidator.Validate(snapshot).Count != 0 || candidate.ContentSha256 != CatalogHash.Compute(snapshot))
                throw new InvalidOperationException("Candidate V1 content hash or catalog validation failed.");
            var combat = new List<CatalogCombatClassV2>();
            var levels = new List<CatalogProgressionV2>();
            var modifiers = new List<CatalogModifierV2>();
            var classLinks = new List<(Guid SourceId, Guid TargetId)>();
            var itemLinks = new List<(Guid SourceId, Guid TargetId)>();
            string? manifestHash = null;
            await Read("SELECT hash_format,content_sha256 FROM catalog_v2_manifest WHERE release_id=@r", r =>
            {
                if (r.GetString(0) != CombatCatalogHashV2.Format) throw new InvalidOperationException("Unsupported V2 manifest format.");
                manifestHash = r.GetString(1);
            });
            await Read("SELECT id,min_damage,max_damage,critical_chance_percent,critical_damage_bonus_percent FROM character_classes WHERE catalog_release_id=@r", r =>
            {
                if (Enumerable.Range(1, 4).Any(r.IsDBNull)) throw new InvalidOperationException("Incomplete class combat statistics.");
                combat.Add(new(r.GetGuid(0), r.GetInt32(1), r.GetInt32(2), r.GetDecimal(3), r.GetDecimal(4)));
            });
            await Read("SELECT l.class_definition_id,l.level,l.required_total_xp,l.health_bonus,l.damage_bonus,l.attribute_points,l.min_damage_bonus,l.max_damage_bonus,l.critical_chance_bonus_percent,l.critical_damage_bonus_percent FROM class_level_rules l JOIN character_classes c ON c.id=l.class_definition_id WHERE c.catalog_release_id=@r", r =>
            {
                if (Enumerable.Range(6, 4).Any(r.IsDBNull)) throw new InvalidOperationException("Incomplete V2 progression.");
                levels.Add(new(r.GetGuid(0), r.GetInt32(1), r.GetInt64(2), r.GetInt32(3), r.GetInt32(4), r.GetInt32(5), r.GetInt32(6), r.GetInt32(7), r.GetDecimal(8), r.GetDecimal(9)));
            });
            await Read("SELECT m.item_definition_id,m.stat_code,m.modifier_kind,m.amount FROM item_stat_modifiers m JOIN item_definitions i ON i.id=m.item_definition_id WHERE i.catalog_release_id=@r", r => modifiers.Add(new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetDecimal(3))));
            await Read("SELECT s.source_class_id,s.target_class_id FROM catalog_class_successors s JOIN character_classes c ON c.id=s.target_class_id WHERE c.catalog_release_id=@r", r => classLinks.Add((r.GetGuid(0), r.GetGuid(1))));
            await Read("SELECT s.source_item_id,s.target_item_id FROM catalog_item_successors s JOIN item_definitions i ON i.id=s.target_item_id WHERE i.catalog_release_id=@r", r => itemLinks.Add((r.GetGuid(0), r.GetGuid(1))));
            if (manifestHash is null || manifestHash != CombatCatalogHashV2.Compute(snapshot, combat, levels, modifiers, classLinks, itemLinks))
                throw new InvalidOperationException("Candidate V2 manifest hash mismatch.");
            foreach (var link in classLinks)
            {
                var source = allClasses.Single(x => x.Id == link.SourceId);
                var target = snapshot.Classes.Single(x => x.Id == link.TargetId);
                if (source.Code != target.Code || source.CatalogReleaseId == candidateId ||
                    !releases.Any(x => x.Id == source.CatalogReleaseId && x.Status is CatalogStates.Published or CatalogStates.Retired))
                    throw new InvalidOperationException("Invalid historical class mapping.");
            }
            foreach (var link in itemLinks)
            {
                var source = allItems.Single(x => x.Id == link.SourceId);
                var target = snapshot.Items.Single(x => x.Id == link.TargetId);
                if (source.CatalogReleaseId == candidateId || source.Code != target.Code || source.Category != target.Category ||
                    source.EquipSlot != target.EquipSlot || source.HandRequirement != target.HandRequirement ||
                    source.Stackable != target.Stackable || source.MaxStack != target.MaxStack ||
                    !releases.Any(x => x.Id == source.CatalogReleaseId && x.Status is CatalogStates.Published or CatalogStates.Retired))
                    throw new InvalidOperationException("Invalid or incompatible historical item mapping.");
            }
            var allowedStats = new HashSet<string>(["max_health", "fixed_damage", "strength", "dexterity", "vitality", "armor", "attack_speed", "critical_chance", "critical_multiplier", "min_damage", "max_damage"]);
            if (modifiers.Any(x => !allowedStats.Contains(x.StatCode) || x.Amount is < -100000 or > 100000 ||
                (x.StatCode is "critical_chance" or "critical_multiplier") && x.ModifierKind != "flat"))
                throw new InvalidOperationException("Invalid candidate modifier definition.");

            var players = await characters.Characters.AsNoTracking().ToListAsync(ct);
            var owned = await characters.OwnedItems.AsNoTracking().ToListAsync(ct);
            var equipped = await characters.Equipment.AsNoTracking().ToListAsync(ct);
            CatalogInventoryCompatibility.Validate(snapshot, players, owned, equipped, allItems, itemLinks);
            // Factory validation needs published metadata, but no activation is performed until all persisted references pass.
            var prospectiveRelease = new CatalogRelease { Id = candidate.Id, VersionKey = candidate.VersionKey,
                SchemaMajor = candidate.SchemaMajor, ContentSha256 = candidate.ContentSha256,
                Status = CatalogStates.Published, CreatedAt = candidate.CreatedAt, PublishedAt = DateTimeOffset.UtcNow };
            var prospective = snapshot with { Release = prospectiveRelease };
            foreach (var cls in snapshot.Classes)
                foreach (var level in levels.Where(x => x.ClassId == cls.Id))
                    GameplaySnapshotV2Factory.Create(new CharacterRow { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(),
                        ClassDefinitionId = cls.Id, Experience = level.RequiredTotalXp, RowRevision = 1 },
                        cls, prospectiveRelease, prospective, combat, levels, modifiers, classLinks,
                        Array.Empty<Guid>(), manifestHash, itemLinks);
            foreach (var player in players)
            {
                var source = allClasses.Single(x => x.Id == player.ClassDefinitionId);
                var sourceRelease = releases.Single(x => x.Id == source.CatalogReleaseId);
                var resolved = CatalogClassTransition.Resolve(source, sourceRelease, prospectiveRelease, snapshot.Classes, classLinks);
                var progression = levels.Where(x => x.ClassId == resolved.Id).OrderBy(x => x.Level).Select(x => new LevelRuleRow {
                    ClassDefinitionId=x.ClassId, Level=x.Level, RequiredTotalXp=x.RequiredTotalXp,
                    HealthBonus=x.HealthBonus, DamageBonus=x.DamageBonus, AttributePoints=x.AttributePoints }).ToArray();
                CharacterRules.ValidateAllocation(player.Experience, player.AllocatedStrength, player.AllocatedDexterity, player.AllocatedVitality, progression);
                if (player.DeletedAt is not null) continue; // Class/allocation/inventory remain validated; soft-deleted records have no live snapshot.
                var ids = equipped.Where(x => x.CharacterId == player.Id).Select(x => owned.Single(i => i.Id == x.OwnedItemId).ItemDefinitionId).ToArray();
                GameplaySnapshotV2Factory.Create(player, source, sourceRelease, prospective, combat, levels, modifiers,
                    classLinks, ids, manifestHash, itemLinks, allItems, releases);
            }
            await using (var retire = new NpgsqlCommand("UPDATE catalog_releases SET status='retired' WHERE id=@id AND status='published'", connection, transaction))
            {
                retire.Parameters.AddWithValue("id", previous.Id);
                if (await retire.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Previous active release changed.");
            }
            await using (var activate = new NpgsqlCommand("UPDATE catalog_releases SET status='published',published_at=now() WHERE id=@id AND status='draft'", connection, transaction))
            {
                activate.Parameters.AddWithValue("id", candidateId);
                if (await activate.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Candidate state changed.");
            }
            await using (var verify = new NpgsqlCommand("SELECT count(*)=1 AND bool_and(id=@id) FROM catalog_releases WHERE status='published'", connection, transaction))
            {
                verify.Parameters.AddWithValue("id", candidateId);
                if (await verify.ExecuteScalarAsync(ct) is not true) throw new InvalidOperationException("Publication did not produce exactly one active candidate.");
            }
            await transaction.ReleaseAsync(savepoint, ct);

            async Task Exec(string sql)
            {
                await using var command = new NpgsqlCommand(sql, connection, transaction);
                await command.ExecuteNonQueryAsync(ct);
            }
            async Task Read(string sql, Action<NpgsqlDataReader> accept)
            {
                await using var command = new NpgsqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("r", candidateId);
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) accept(reader);
            }
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }

    public static void RequireIsolatedConnection(string connectionString)
    {
        var value = new NpgsqlConnectionStringBuilder(connectionString);
        if (value.Database != "ashenfall_09b3_test" || value.Port != 5432 ||
            value.Host is not ("localhost" or "127.0.0.1" or "::1") || value.Username != "postgres")
            throw new InvalidOperationException("V2 publication is restricted to the verified isolated database and administrative identity.");
    }
}
