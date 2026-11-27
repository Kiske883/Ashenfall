using System.Data;
using System.Data.Common;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Ashenfall.Api.Characters;

/// <summary>Reads and verifies one active V2 catalog and character within a read-only MVCC snapshot.</summary>
public sealed class GameplaySnapshotV2Reader(CharacterDbContext db)
{
    public async Task<(bool IsV2, CharacterGameplaySnapshotV2? Snapshot)> ReadAsync(string provider, string subject, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        var release = await db.SnapshotReleases.AsNoTracking().SingleOrDefaultAsync(x => x.Status == CatalogStates.Published, ct);
        if (release is null) throw new InvalidGameplaySnapshotException();
        async Task Read(string sql, Action<DbDataReader> accept)
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.Transaction = tx.GetDbTransaction(); cmd.CommandText = sql;
            var p = cmd.CreateParameter(); p.ParameterName = "r"; p.Value = release.Id; cmd.Parameters.Add(p);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) accept(reader);
        }
        await using (var schema = db.Database.GetDbConnection().CreateCommand())
        {
            schema.Transaction = tx.GetDbTransaction();
            schema.CommandText = "SELECT to_regclass('public.catalog_v2_manifest') IS NOT NULL";
            if ((bool)(await schema.ExecuteScalarAsync(ct))! == false) { await tx.CommitAsync(ct); return (false, null); }
        }
        string? hash = null;
        await Read("SELECT content_sha256 FROM catalog_v2_manifest WHERE release_id=@r AND hash_format='ASHENFALL-CATALOG-CONTENT-V2'", r => hash = r.GetString(0));
        if (hash is null) { await tx.CommitAsync(ct); return (false, null); }
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.IdentityProvider == provider && x.ProviderSubject == subject, ct);
        if (account is null) { await tx.CommitAsync(ct); return (true, null); }
        var character = await db.Characters.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == account.Id && x.DeletedAt == null, ct);
        if (character is null) { await tx.CommitAsync(ct); return (true, null); }
        var original = await db.SnapshotClasses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == character.ClassDefinitionId, ct) ?? throw new InvalidGameplaySnapshotException();
        var originalRelease = await db.SnapshotReleases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == original.CatalogReleaseId, ct) ?? throw new InvalidGameplaySnapshotException();
        var classes = await db.SnapshotClasses.AsNoTracking().Where(x => x.CatalogReleaseId == release.Id).ToListAsync(ct);
        var enemies = await db.SnapshotEnemies.AsNoTracking().Where(x => x.CatalogReleaseId == release.Id).ToListAsync(ct);
        var items = await db.SnapshotItems.AsNoTracking().Where(x => x.CatalogReleaseId == release.Id).ToListAsync(ct);
        var active = new CatalogSnapshot(release, classes, enemies, items);
        var combat = new List<CatalogCombatClassV2>(); var levels = new List<CatalogProgressionV2>();
        var modifiers = new List<CatalogModifierV2>(); var classLinks = new List<(Guid SourceId, Guid TargetId)>();
        var itemLinks = new List<(Guid SourceId, Guid TargetId)>();
        await Read("SELECT id,min_damage,max_damage,critical_chance_percent,critical_damage_bonus_percent FROM character_classes WHERE catalog_release_id=@r", r => { if (Enumerable.Range(1, 4).Any(r.IsDBNull)) throw new InvalidGameplaySnapshotException(); combat.Add(new(r.GetGuid(0), r.GetInt32(1), r.GetInt32(2), r.GetDecimal(3), r.GetDecimal(4))); });
        await Read("SELECT l.class_definition_id,l.level,l.required_total_xp,l.health_bonus,l.damage_bonus,l.attribute_points,l.min_damage_bonus,l.max_damage_bonus,l.critical_chance_bonus_percent,l.critical_damage_bonus_percent FROM class_level_rules l JOIN character_classes c ON c.id=l.class_definition_id WHERE c.catalog_release_id=@r", r => { if (Enumerable.Range(6, 4).Any(r.IsDBNull)) throw new InvalidGameplaySnapshotException(); levels.Add(new(r.GetGuid(0), r.GetInt32(1), r.GetInt64(2), r.GetInt32(3), r.GetInt32(4), r.GetInt32(5), r.GetInt32(6), r.GetInt32(7), r.GetDecimal(8), r.GetDecimal(9))); });
        await Read("SELECT m.item_definition_id,m.stat_code,m.modifier_kind,m.amount FROM item_stat_modifiers m JOIN item_definitions i ON i.id=m.item_definition_id WHERE i.catalog_release_id=@r", r => modifiers.Add(new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetDecimal(3))));
        await Read("SELECT s.source_class_id,s.target_class_id FROM catalog_class_successors s JOIN character_classes c ON c.id=s.target_class_id WHERE c.catalog_release_id=@r", r => classLinks.Add((r.GetGuid(0), r.GetGuid(1))));
        await Read("SELECT s.source_item_id,s.target_item_id FROM catalog_item_successors s JOIN item_definitions i ON i.id=s.target_item_id WHERE i.catalog_release_id=@r", r => itemLinks.Add((r.GetGuid(0), r.GetGuid(1))));
        var equipped = await (from e in db.Equipment join i in db.OwnedItems on e.OwnedItemId equals i.Id
                              where e.CharacterId == character.Id && i.CharacterId == character.Id select i.ItemDefinitionId).ToListAsync(ct);
        var historical = await db.SnapshotItems.AsNoTracking().Where(x => equipped.Contains(x.Id) && x.CatalogReleaseId != release.Id).ToListAsync(ct);
        var historicalIds = historical.Select(x => x.CatalogReleaseId).Distinct().ToArray();
        var historicalReleases = await db.SnapshotReleases.AsNoTracking().Where(x => historicalIds.Contains(x.Id)).ToListAsync(ct);
        var result = GameplaySnapshotV2Factory.Create(character, original, originalRelease, active, combat, levels, modifiers,
            classLinks, equipped, hash, itemLinks, historical, historicalReleases);
        await tx.CommitAsync(ct);
        return (true, result);
    }
}
