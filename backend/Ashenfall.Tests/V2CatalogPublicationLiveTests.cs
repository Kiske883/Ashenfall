using System.Data;
using System.Text.Json;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Persistence;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Api.Characters;
using Ashenfall.CatalogPublisher;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Ashenfall.Tests;

public sealed class PublisherV2LiveFactAttribute : FactAttribute
{
    public PublisherV2LiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ASHENFALL_09C3_PUBLISHER_LIVE") != "1")
            Skip = "Explicitly opt in to rolled-back V2 publication tests on the isolated database.";
    }
}
[CollectionDefinition("V2Publication", DisableParallelization = true)]
public sealed class V2PublicationCollection { }

[Collection("V2Publication")]
public sealed class V2CatalogPublicationLiveTests
{
    private const string Connection = "Host=localhost;Port=5432;Database=ashenfall_09b3_test;Username=postgres;Pooling=false;Timeout=5;Command Timeout=20";
    private sealed record Fixture(Guid Release, Guid Previous, Guid Account, Guid Character, Guid Sword,
        Guid Stack, Guid CandidateSword, Guid CandidateStack, string Fingerprint);

    [Fact]
    public void PublisherRejectsNonisolatedConnectionBeforeOpeningIt()
    {
        Assert.Throws<InvalidOperationException>(() => V2CatalogPublication.RequireIsolatedConnection("Host=localhost;Database=ashenfall_dev;Username=postgres"));
        Assert.Throws<InvalidOperationException>(() => V2CatalogPublication.RequireIsolatedConnection("Host=remote.example;Database=ashenfall_09b3_test;Username=postgres"));
        Assert.Throws<InvalidOperationException>(() => V2CatalogPublication.RequireIsolatedConnection("Host=localhost;Database=ashenfall_09b3_test;Username=ashenfall_app"));
    }

    private static async Task<NpgsqlConnection> Open(string? application = null)
    {
        var value = new NpgsqlConnectionStringBuilder(Connection) { ApplicationName = application ?? "v2publisher_test" };
        V2CatalogPublication.RequireIsolatedConnection(value.ConnectionString);
        var c = new NpgsqlConnection(value.ConnectionString);
        try
        {
            await c.OpenAsync();
            await using var check = new NpgsqlCommand("SELECT current_database()='ashenfall_09b3_test' AND current_user='postgres' AND inet_server_port()=5432 AND (inet_server_addr() <<= '127.0.0.0/8'::inet OR inet_server_addr()='::1'::inet) AND (SELECT system_identifier::text FROM pg_control_system())='7708337984335018212'", c);
            Assert.True((bool)(await check.ExecuteScalarAsync())!);
            return c;
        }
        catch { await c.DisposeAsync(); throw; }
    }

    private static async Task Exec(NpgsqlConnection c, NpgsqlTransaction t, string sql, params (string Name, object Value)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, c, t);
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }
    private static async Task<string> Fingerprint(NpgsqlConnection c, NpgsqlTransaction? t)
    {
        // All persistent character rows, not merely the manual character: publication must change none of them.
        await using var cmd = new NpgsqlCommand("""
            SELECT json_build_array(
              (SELECT coalesce(json_agg(x ORDER BY id),'[]') FROM player_characters x),
              (SELECT coalesce(json_agg(x ORDER BY id),'[]') FROM owned_items x),
              (SELECT coalesce(json_agg(x ORDER BY character_id,slot_code),'[]') FROM character_equipment x))::text
            """, c, t);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static async Task<Fixture> Prepare(NpgsqlConnection c, NpgsqlTransaction tx)
    {
        await using var catalog = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(c).Options);
        await using var characters = new CharacterDbContext(new DbContextOptionsBuilder<CharacterDbContext>().UseNpgsql(c).Options);
        await catalog.Database.UseTransactionAsync(tx); await characters.Database.UseTransactionAsync(tx);
        var source = await catalog.Releases.AsNoTracking().SingleAsync(x => x.Status == CatalogStates.Published);
        Assert.Contains(source.VersionKey, new[] { SnapshotFixtureManifest.Version, "snapshot-09c3-v2" });
        var originals = await catalog.Classes.AsNoTracking().Where(x => x.CatalogReleaseId == source.Id).ToListAsync();
        var originalItems = await catalog.Items.AsNoTracking().Where(x => x.CatalogReleaseId == source.Id).ToListAsync();
        var classes = originals.Select(Clone).ToArray(); var items = originalItems.Select(Clone).ToArray();
        var release = new CatalogRelease { Id = Guid.NewGuid(), VersionKey = "publisher-test-" + Guid.NewGuid().ToString("N"), SchemaMajor = 1, Status = CatalogStates.Draft };
        var classLinks = new List<(Guid SourceId, Guid TargetId)>(); var itemLinks = new List<(Guid SourceId, Guid TargetId)>();
        foreach (var cls in classes) { var old = cls.Id; cls.Id = Guid.NewGuid(); cls.CatalogReleaseId = release.Id; classLinks.Add((old, cls.Id)); }
        foreach (var item in items) { var old = item.Id; item.Id = Guid.NewGuid(); item.CatalogReleaseId = release.Id; itemLinks.Add((old, item.Id)); }
        // Carry forward explicit historical identities through the active release's existing links.
        // This preserves the manual V1 character while publication tests run after real V2 activation.
        var classMap = classLinks.ToDictionary(x => x.SourceId, x => x.TargetId);
        var itemMap = itemLinks.ToDictionary(x => x.SourceId, x => x.TargetId);
        await using (var cmd = new NpgsqlCommand("SELECT s.source_class_id,s.target_class_id FROM catalog_class_successors s JOIN character_classes c ON c.id=s.target_class_id WHERE c.catalog_release_id=@r", c, tx))
        { cmd.Parameters.AddWithValue("r", source.Id); await using var rows = await cmd.ExecuteReaderAsync(); while (await rows.ReadAsync()) classLinks.Add((rows.GetGuid(0), classMap[rows.GetGuid(1)])); }
        await using (var cmd = new NpgsqlCommand("SELECT s.source_item_id,s.target_item_id FROM catalog_item_successors s JOIN item_definitions i ON i.id=s.target_item_id WHERE i.catalog_release_id=@r", c, tx))
        { cmd.Parameters.AddWithValue("r", source.Id); await using var rows = await cmd.ExecuteReaderAsync(); while (await rows.ReadAsync()) itemLinks.Add((rows.GetGuid(0), itemMap[rows.GetGuid(1)])); }
        var enemies = (await catalog.Enemies.AsNoTracking().Where(x => x.CatalogReleaseId == source.Id).ToListAsync()).Select(Clone).ToArray();
        foreach (var enemy in enemies) { enemy.Id = Guid.NewGuid(); enemy.CatalogReleaseId = release.Id; }
        Assert.Equal(source.ContentSha256, CatalogHash.Compute(new CatalogSnapshot(source, originals,
            await catalog.Enemies.AsNoTracking().Where(x => x.CatalogReleaseId == source.Id).ToListAsync(), originalItems)));
        var oldRules = await characters.LevelRules.AsNoTracking().Where(x => originals.Select(i => i.Id).Contains(x.ClassDefinitionId)).ToListAsync();
        var oldMods = await characters.ItemModifiers.AsNoTracking().Where(x => originalItems.Select(i => i.Id).Contains(x.ItemDefinitionId)).ToListAsync();
        var combat = classes.Select(x => new CatalogCombatClassV2(x.Id, x.FixedDamage, x.FixedDamage, 5, 100)).ToArray();
        var levels = oldRules.Select(x => new CatalogProgressionV2(classLinks.Single(i => i.SourceId == x.ClassDefinitionId).TargetId,
            x.Level, x.RequiredTotalXp, x.HealthBonus, x.DamageBonus, x.AttributePoints, 0, 0, 0, 0)).ToArray();
        var modifiers = oldMods.Select(x => new CatalogModifierV2(itemLinks.Single(i => i.SourceId == x.ItemDefinitionId).TargetId, x.StatCode, x.ModifierKind, x.Amount)).ToArray();
        catalog.Releases.Add(release); await catalog.SaveChangesAsync();
        catalog.Classes.AddRange(classes); catalog.Items.AddRange(items); catalog.Enemies.AddRange(enemies); await catalog.SaveChangesAsync();
        foreach (var x in combat)
            await Exec(c, tx, "UPDATE character_classes SET min_damage=@min,max_damage=@max,critical_chance_percent=@chance,critical_damage_bonus_percent=@bonus WHERE id=@id", ("min", x.MinDamage), ("max", x.MaxDamage), ("chance", x.CriticalChancePercent), ("bonus", x.CriticalDamageBonusPercent), ("id", x.ClassId));
        foreach (var x in levels)
            await Exec(c, tx, "INSERT INTO class_level_rules(class_definition_id,level,required_total_xp,health_bonus,damage_bonus,attribute_points,min_damage_bonus,max_damage_bonus,critical_chance_bonus_percent,critical_damage_bonus_percent) VALUES(@id,@level,@xp,@hp,@damage,@points,0,0,0,0)", ("id", x.ClassId), ("level", x.Level), ("xp", x.RequiredTotalXp), ("hp", x.HealthBonus), ("damage", x.DamageBonus), ("points", x.AttributePoints));
        foreach (var x in modifiers)
            await Exec(c, tx, "INSERT INTO item_stat_modifiers VALUES(@id,@stat,@kind,@amount)", ("id", x.ItemId), ("stat", x.StatCode), ("kind", x.ModifierKind), ("amount", x.Amount));
        foreach (var x in classLinks) await Exec(c, tx, "INSERT INTO catalog_class_successors VALUES(@s,@t)", ("s", x.SourceId), ("t", x.TargetId));
        foreach (var x in itemLinks) await Exec(c, tx, "INSERT INTO catalog_item_successors VALUES(@s,@t)", ("s", x.SourceId), ("t", x.TargetId));
        var snapshot = new CatalogSnapshot(release, classes, enemies, items);
        await Exec(c, tx, "UPDATE catalog_releases SET content_sha256=@hash WHERE id=@id", ("hash", CatalogHash.Compute(snapshot)), ("id", release.Id));
        await Exec(c, tx, "INSERT INTO catalog_v2_manifest VALUES(@id,@format,@hash)", ("id", release.Id), ("format", CombatCatalogHashV2.Format), ("hash", CombatCatalogHashV2.Compute(snapshot, combat, levels, modifiers, classLinks, itemLinks)));
        var account = Guid.NewGuid(); var character = Guid.NewGuid(); var sword = Guid.NewGuid(); var stack = Guid.NewGuid();
        var oldSword = originalItems.Single(x => x.Code == "guardian_sword"); var oldStack = originalItems.Single(x => x.Code == "test_stack");
        await Exec(c, tx, "INSERT INTO accounts(id,identity_provider,provider_subject) VALUES(@a,'local_development',@subject); INSERT INTO player_characters(id,account_id,class_definition_id,character_name,experience) VALUES(@c,@a,@class,'V2 publisher test',100); INSERT INTO owned_items(id,character_id,item_definition_id,quantity) VALUES(@sword,@c,@definition,1),(@stack,@c,@stackdef,5); INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES(@c,'main_hand',@sword)",
            ("a", account), ("subject", "publisher-test-" + account.ToString("N")), ("c", character), ("class", originals.Single().Id), ("sword", sword), ("stack", stack), ("definition", oldSword.Id), ("stackdef", oldStack.Id));
        return new Fixture(release.Id, source.Id, account, character, sword, stack,
            itemLinks.Single(x => x.SourceId == oldSword.Id).TargetId, itemLinks.Single(x => x.SourceId == oldStack.Id).TargetId, await Fingerprint(c, tx));
    }

    private static async Task AssertStates(NpgsqlConnection c, NpgsqlTransaction tx, Fixture f, bool published)
    {
        await using var cmd = new NpgsqlCommand("SELECT (SELECT count(*) FROM catalog_releases WHERE status='published'),(SELECT id FROM catalog_releases WHERE status='published'),(SELECT status FROM catalog_releases WHERE id=@candidate)", c, tx);
        cmd.Parameters.AddWithValue("candidate", f.Release);
        await using (var r = await cmd.ExecuteReaderAsync())
        { Assert.True(await r.ReadAsync()); Assert.Equal(1L, r.GetInt64(0)); Assert.Equal(published ? f.Release : f.Previous, r.GetGuid(1)); Assert.Equal(published ? "published" : "draft", r.GetString(2)); }
        Assert.Equal(f.Fingerprint, await Fingerprint(c, tx));
    }

    private static async Task RefreshHash(NpgsqlConnection c, NpgsqlTransaction tx, Guid releaseId)
    {
        await using var catalog = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(c).Options);
        await using var characters = new CharacterDbContext(new DbContextOptionsBuilder<CharacterDbContext>().UseNpgsql(c).Options);
        await catalog.Database.UseTransactionAsync(tx); await characters.Database.UseTransactionAsync(tx);
        var release = await catalog.Releases.AsNoTracking().SingleAsync(x => x.Id == releaseId);
        var classes = await catalog.Classes.AsNoTracking().Where(x => x.CatalogReleaseId == releaseId).ToListAsync();
        var items = await catalog.Items.AsNoTracking().Where(x => x.CatalogReleaseId == releaseId).ToListAsync();
        var snapshot = new CatalogSnapshot(release, classes,
            await catalog.Enemies.AsNoTracking().Where(x => x.CatalogReleaseId == releaseId).ToListAsync(), items);
        var classIds = classes.Select(x => x.Id).ToArray(); var itemIds = items.Select(x => x.Id).ToArray();
        var levels = (await characters.LevelRules.AsNoTracking().Where(x => classIds.Contains(x.ClassDefinitionId)).ToListAsync())
            .Select(x => new CatalogProgressionV2(x.ClassDefinitionId, x.Level, x.RequiredTotalXp, x.HealthBonus, x.DamageBonus, x.AttributePoints, 0, 0, 0, 0)).ToArray();
        var modifiers = (await characters.ItemModifiers.AsNoTracking().Where(x => itemIds.Contains(x.ItemDefinitionId)).ToListAsync())
            .Select(x => new CatalogModifierV2(x.ItemDefinitionId, x.StatCode, x.ModifierKind, x.Amount)).ToArray();
        var classLinks = new List<(Guid SourceId, Guid TargetId)>(); var itemLinks = new List<(Guid SourceId, Guid TargetId)>();
        await using (var cmd = new NpgsqlCommand("SELECT s.source_class_id,s.target_class_id FROM catalog_class_successors s JOIN character_classes c ON c.id=s.target_class_id WHERE c.catalog_release_id=@r", c, tx))
        { cmd.Parameters.AddWithValue("r", releaseId); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) classLinks.Add((r.GetGuid(0), r.GetGuid(1))); }
        await using (var cmd = new NpgsqlCommand("SELECT s.source_item_id,s.target_item_id FROM catalog_item_successors s JOIN item_definitions i ON i.id=s.target_item_id WHERE i.catalog_release_id=@r", c, tx))
        { cmd.Parameters.AddWithValue("r", releaseId); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) itemLinks.Add((r.GetGuid(0), r.GetGuid(1))); }
        var combat = classes.Select(x => new CatalogCombatClassV2(x.Id, x.FixedDamage, x.FixedDamage, 5, 100)).ToArray();
        await Exec(c, tx, "UPDATE catalog_releases SET content_sha256=@h WHERE id=@r", ("h", CatalogHash.Compute(snapshot)), ("r", releaseId));
        await Exec(c, tx, "UPDATE catalog_v2_manifest SET content_sha256=@h WHERE release_id=@r", ("h", CombatCatalogHashV2.Compute(snapshot, combat, levels, modifiers, classLinks, itemLinks)), ("r", releaseId));
    }

    [PublisherV2LiveFact]
    public async Task SuccessfulPublicationIsVisibleInsideTransactionAndFullyRollsBack()
    {
        await using var c = await Open(); var before = await Fingerprint(c, null);
        await using (var tx = await c.BeginTransactionAsync())
        {
            var f = await Prepare(c, tx);
            await V2CatalogPublication.PublishInTransactionAsync(c, tx, f.Release);
            await AssertStates(c, tx, f, true);
            await tx.RollbackAsync();
        }
        Assert.Equal(before, await Fingerprint(c, null));
    }

    [PublisherV2LiveFact]
    public async Task InvalidCandidatesAndActivationFailureRestoreThePreviousRelease()
    {
        foreach (var failure in new[] { "hash", "class", "item", "max_stack", "slot", "activation" })
        {
            await using var c = await Open();
            await using var tx = await c.BeginTransactionAsync();
            var f = await Prepare(c, tx);
            switch (failure)
            {
                case "hash": await Exec(c, tx, "UPDATE catalog_v2_manifest SET content_sha256=repeat('a',64) WHERE release_id=@r", ("r", f.Release)); break;
                case "class": await Exec(c, tx, "DELETE FROM catalog_class_successors WHERE target_class_id IN (SELECT id FROM character_classes WHERE catalog_release_id=@r)", ("r", f.Release)); break;
                case "item": await Exec(c, tx, "DELETE FROM catalog_item_successors WHERE target_item_id=@id", ("id", f.CandidateSword)); break;
                case "max_stack": await Exec(c, tx, "UPDATE item_definitions SET max_stack=4 WHERE id=@id", ("id", f.CandidateStack)); break;
                case "slot": await Exec(c, tx, "UPDATE item_definitions SET equip_slot='off_hand' WHERE id=@id", ("id", f.CandidateSword)); break;
                case "activation":
                    var name = "reject_v2_" + Guid.NewGuid().ToString("N");
                    await Exec(c, tx, $"CREATE FUNCTION public.{name}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Injected activation rejection'; END $$; CREATE TRIGGER {name} BEFORE UPDATE ON catalog_releases FOR EACH ROW WHEN (NEW.id='{f.Release}'::uuid AND NEW.status='published') EXECUTE FUNCTION public.{name}()");
                    break;
            }
            // Recompute legitimate hashes for compatibility failures so the test reaches the intended check.
            if (failure is not ("hash" or "activation")) await RefreshHash(c, tx, f.Release);
            var error = await Record.ExceptionAsync(() => V2CatalogPublication.PublishInTransactionAsync(c, tx, f.Release));
            Assert.NotNull(error);
            if (failure == "activation") Assert.Contains("Injected activation rejection", error.Message);
            if (failure == "hash") Assert.Contains("manifest hash mismatch", error.Message);
            if (failure == "class") Assert.Contains("class successor", error.Message);
            if (failure == "item") Assert.Contains("Missing item successor", error.Message);
            if (failure is "max_stack" or "slot") Assert.Contains("incompatible historical item mapping", error.Message);
            await AssertStates(c, tx, f, false);
            await tx.RollbackAsync();
        }
    }

    [PublisherV2LiveFact]
    public async Task OwningTransactionEntryPointRollsBackRejectedPublication()
    {
        await using var admin = await Open();
        Fixture f;
        await using (var setup = await admin.BeginTransactionAsync())
        {
            f = await Prepare(admin, setup);
            await Exec(admin, setup, "UPDATE catalog_v2_manifest SET content_sha256=repeat('a',64) WHERE release_id=@r", ("r", f.Release));
            await setup.CommitAsync();
        }
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => V2CatalogPublication.PublishAsync(Connection, f.Release));
            Assert.Contains("manifest hash mismatch", error.Message);
            await using var verify = await admin.BeginTransactionAsync();
            await AssertStates(admin, verify, f, false);
            await verify.RollbackAsync();
        }
        finally { await Cleanup(admin, f); }
    }

    [PublisherV2LiveFact]
    public async Task ConcurrentPublicationAttemptsSerializeAndDoNotUseStaleState()
    {
        await using var admin = await Open();
        Fixture f;
        await using (var setup = await admin.BeginTransactionAsync()) { f = await Prepare(admin, setup); await setup.CommitAsync(); }
        try
        {
            await using var first = await Open("v2_pub_first");
            await using var second = await Open("v2_pub_second");
            await using var a = await first.BeginTransactionAsync();
            await using var b = await second.BeginTransactionAsync();
            await V2CatalogPublication.PublishInTransactionAsync(first, a, f.Release);
            var waiting = V2CatalogPublication.PublishInTransactionAsync(second, b, f.Release);
            var seen = false;
            for (var i = 0; i < 100; i++)
            {
                await using var check = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE application_name='v2_pub_second' AND wait_event_type='Lock'", admin);
                if ((long)(await check.ExecuteScalarAsync())! == 1) { seen = true; break; }
                await Task.Delay(30);
            }
            Assert.True(seen); Assert.False(waiting.IsCompleted);
            await a.RollbackAsync();
            await waiting.WaitAsync(TimeSpan.FromSeconds(15));
            await AssertStates(second, b, f, true);
            await b.RollbackAsync();
        }
        finally { await Cleanup(admin, f); }
    }

    private static async Task Cleanup(NpgsqlConnection c, Fixture f)
    {
        await using var tx = await c.BeginTransactionAsync();
        await using (var check = new NpgsqlCommand("SELECT status FROM catalog_releases WHERE id=@r FOR UPDATE", c, tx))
        { check.Parameters.AddWithValue("r", f.Release); Assert.Equal("draft", (string?)await check.ExecuteScalarAsync()); }
        await Exec(c, tx, "DELETE FROM character_equipment WHERE character_id=@c; DELETE FROM owned_items WHERE character_id=@c; DELETE FROM player_characters WHERE id=@c AND account_id=@a; DELETE FROM accounts WHERE id=@a; DELETE FROM catalog_v2_manifest WHERE release_id=@r; DELETE FROM catalog_class_successors WHERE target_class_id IN (SELECT id FROM character_classes WHERE catalog_release_id=@r); DELETE FROM catalog_item_successors WHERE target_item_id IN (SELECT id FROM item_definitions WHERE catalog_release_id=@r); DELETE FROM item_stat_modifiers WHERE item_definition_id IN (SELECT id FROM item_definitions WHERE catalog_release_id=@r); DELETE FROM class_level_rules WHERE class_definition_id IN (SELECT id FROM character_classes WHERE catalog_release_id=@r); DELETE FROM enemy_definitions WHERE catalog_release_id=@r; DELETE FROM item_definitions WHERE catalog_release_id=@r; DELETE FROM character_classes WHERE catalog_release_id=@r; DELETE FROM catalog_releases WHERE id=@r", ("c", f.Character), ("a", f.Account), ("r", f.Release));
        await tx.CommitAsync();
        await using var verify = new NpgsqlCommand("SELECT (SELECT count(*) FROM catalog_releases WHERE id=@r)+(SELECT count(*) FROM accounts WHERE id=@a)+(SELECT count(*) FROM player_characters WHERE id=@c)+(SELECT count(*) FROM owned_items WHERE character_id=@c)+(SELECT count(*) FROM character_equipment WHERE character_id=@c)", c);
        verify.Parameters.AddWithValue("r", f.Release); verify.Parameters.AddWithValue("a", f.Account); verify.Parameters.AddWithValue("c", f.Character);
        Assert.Equal(0L, (long)(await verify.ExecuteScalarAsync())!);
    }
}
