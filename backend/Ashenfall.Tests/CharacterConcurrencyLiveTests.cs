using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Data.Common;
using Ashenfall.Api.Characters;
using Ashenfall.Api.Catalog.Persistence;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Characters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace Ashenfall.Tests;

// Explicit opt-in: these tests create and remove only uniquely identified test rows.
public sealed class Live09B5FactAttribute : FactAttribute
{
    public Live09B5FactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ASHENFALL_09B5_LIVE") != "1")
            Skip = "Set ASHENFALL_09B5_LIVE=1 to use the isolated PostgreSQL test database.";
    }
}

public sealed class CharacterConcurrencyLiveTests
{
    private sealed class PauseAfterAccountRead : DbCommandInterceptor
    {
        public readonly TaskCompletionSource Seen = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Continue = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool VerifiedReadOnlyRepeatableRead;
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText == "SET TRANSACTION READ ONLY")
            {
                await using var check = command.Connection!.CreateCommand();
                check.Transaction = command.Transaction;
                check.CommandText = "SELECT current_setting('transaction_read_only') = 'on' AND current_setting('transaction_isolation') = 'repeatable read'";
                VerifiedReadOnlyRepeatableRead = (bool)(await check.ExecuteScalarAsync(cancellationToken))!;
            }
            return result;
        }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM accounts AS", StringComparison.Ordinal))
            {
                Seen.TrySetResult();
                await Continue.Task.WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
            }
            return result;
        }
    }
    private const string Db = "ashenfall_09b3_test";
    private string ClassId = "90000000-0000-0000-0000-000000000002";
    private string OneHand = "90000000-0000-0000-0000-000000000011";
    private string TwoHand = "90000000-0000-0000-0000-000000000012";
    private string OffHand = "90000000-0000-0000-0000-000000000013";
    private string Stack = "90000000-0000-0000-0000-000000000015";
    private Guid fixtureRelease = SnapshotFixtureManifest.Create().Catalog.Release.Id;
    private string fixtureVersion = SnapshotFixtureManifest.Version;
    private string fixtureBaseHash = CatalogHash.Compute(SnapshotFixtureManifest.Create().Catalog);
    private string fixtureSnapshotHash = CatalogHash.Compute(SnapshotFixtureManifest.Create().Catalog);
    private readonly string applicationName = "ash09b5_" + Guid.NewGuid().ToString("N");
    private readonly string subject = "09b5-" + Guid.NewGuid().ToString("N");
    private readonly string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly Guid account = Guid.NewGuid(), character = Guid.NewGuid();
    private string Admin => $"Host=localhost;Port=5432;Database={Db};Username=postgres;Pooling=false;Command Timeout=10";
    private string App => $"Host=localhost;Port=5432;Database={Db};Username=ashenfall_app;Pooling=false;Command Timeout=10;Application Name={applicationName}";

    private static string RequireIsolated(string connection, string role)
    {
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Database != Db || settings.Host != "localhost" || settings.Port != 5432 ||
            settings.Username != role || !string.IsNullOrEmpty(settings.Password))
            throw new InvalidOperationException("Live test connection must use the isolated local database and expected role.");
        return settings.ConnectionString;
    }

    [Fact]
    public void Live_test_database_guard_rejects_development_and_other_targets()
    {
        Assert.Throws<InvalidOperationException>(() => RequireIsolated("Host=localhost;Database=ashenfall_dev;Username=ashenfall_app", "ashenfall_app"));
        Assert.Throws<InvalidOperationException>(() => RequireIsolated("Host=example.com;Database=ashenfall_09b3_test;Username=ashenfall_app", "ashenfall_app"));
        Assert.Throws<InvalidOperationException>(() => RequireIsolated("Host=localhost;Database=ashenfall_09b3_test;Username=postgres", "ashenfall_app"));
        Assert.Equal(Db, new NpgsqlConnectionStringBuilder(RequireIsolated(App, "ashenfall_app")).Database);
    }

    private sealed class Loopback : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, following) => { context.Connection.RemoteIpAddress = IPAddress.Loopback; return following(); });
            next(app);
        };
    }

    private sealed class Factory(string db, string subject, string token) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASHENFALL_DB_CONNECTION"] = db, ["ASHENFALL_CHARACTERS_ENABLED"] = "1",
                ["ASHENFALL_DEV_IDENTITY_TOKEN"] = token, ["ASHENFALL_DEV_IDENTITY_SUBJECT"] = subject
            }));
            builder.ConfigureTestServices(services =>
            {
                // Program can read its original connection before test-host config
                // callbacks run. Replace both registrations explicitly so no
                // request can reach the developer database.
                services.RemoveAll<DbContextOptions<CharacterDbContext>>();
                services.RemoveAll<DbContextOptions<CatalogDbContext>>();
                services.RemoveAll<CharacterDbContext>();
                services.RemoveAll<CatalogDbContext>();
                var isolated = RequireIsolated(db, "ashenfall_app");
                services.AddDbContext<CharacterDbContext>(options => options.UseNpgsql(isolated));
                services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(isolated));
                services.AddTransient<IStartupFilter, Loopback>();
            });
        }
    }

    private async Task<NpgsqlConnection> OpenAdmin()
    {
        var c = new NpgsqlConnection(RequireIsolated(Admin, "postgres")); await c.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT current_database()='ashenfall_09b3_test' AND current_user='postgres' AND inet_server_port()=5432 AND (inet_server_addr() <<= '127.0.0.0/8'::inet OR inet_server_addr()='::1'::inet) AND (SELECT system_identifier::text FROM pg_control_system())='7708337984335018212'", c);
        Assert.True((bool)(await cmd.ExecuteScalarAsync())!);
        await using (var active = new NpgsqlCommand("SELECT id,version_key,content_sha256,(SELECT content_sha256 FROM catalog_v2_manifest WHERE release_id=r.id) FROM catalog_releases r WHERE status='published'", c))
        await using (var row = await active.ExecuteReaderAsync())
        {
            Assert.True(await row.ReadAsync());
            if (row.GetString(1) == "snapshot-09c3-v2")
            {
                fixtureRelease = Guid.Parse("91000000-0000-0000-0000-000000000001");
                fixtureVersion = "snapshot-09c3-v2";
                fixtureBaseHash = "a33262049a29f4bde6d8e62d2f1d3837ed474a79d163b806917c4dde7fc6e5e1";
                fixtureSnapshotHash = "d1ef0a212b83df17300088210d081a63951621b2602e7ff35cf2a711c4de7d4d";
                ClassId = "91000000-0000-0000-0000-000000000002";
                OneHand = "91000000-0000-0000-0000-000000000011";
                TwoHand = "91000000-0000-0000-0000-000000000012";
                OffHand = "91000000-0000-0000-0000-000000000013";
                Stack = "91000000-0000-0000-0000-000000000015";
                Assert.False(row.IsDBNull(3)); Assert.Equal(fixtureSnapshotHash, row.GetString(3));
            }
            Assert.Equal(fixtureRelease, row.GetGuid(0)); Assert.Equal(fixtureVersion, row.GetString(1));
            Assert.Equal(fixtureBaseHash, row.GetString(2)); Assert.False(await row.ReadAsync());
        }
        await using var app = new NpgsqlConnection(App); await app.OpenAsync();
        await using var privileges = new NpgsqlCommand("""
            SELECT current_database()='ashenfall_09b3_test' AND current_user='ashenfall_app'
              AND has_schema_privilege(current_user,'public','USAGE')
              AND NOT has_schema_privilege(current_user,'public','CREATE')
              AND has_table_privilege(current_user,'character_classes','SELECT')
              AND has_table_privilege(current_user,'item_definitions','SELECT')
              AND has_table_privilege(current_user,'catalog_releases','SELECT')
              AND NOT has_table_privilege(current_user,'catalog_releases','UPDATE')
              AND has_table_privilege(current_user,'accounts','INSERT')
              AND has_table_privilege(current_user,'player_characters','UPDATE')
              AND has_table_privilege(current_user,'owned_items','UPDATE')
              AND has_table_privilege(current_user,'character_equipment','INSERT')
            """, app);
        Assert.True((bool)(await privileges.ExecuteScalarAsync())!, "Isolated application role prerequisites differ.");
        return c;
    }

    private static async Task Exec(NpgsqlConnection c, string sql, params (string, object)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task Setup(NpgsqlConnection c, params (Guid id, string definition, int quantity)[] items)
    {
        // Refuse drift before creating any data. Catalog markers are synthetic test fixtures.
        await using (var check = new NpgsqlCommand("SELECT count(*) FROM catalog_releases WHERE version_key=@version AND status='published' AND content_sha256=@hash", c))
        {
            check.Parameters.AddWithValue("version", fixtureVersion);
            check.Parameters.AddWithValue("hash", fixtureBaseHash);
            Assert.Equal(1L, (long)(await check.ExecuteScalarAsync())!);
        }
        await Exec(c, "INSERT INTO accounts(id,identity_provider,provider_subject) VALUES(@a,'local_development',@s); INSERT INTO player_characters(id,account_id,class_definition_id,character_name) VALUES(@c,@a,@class,'09b5 test')", ("a", account), ("s", subject), ("c", character), ("class", Guid.Parse(ClassId)));
        foreach (var item in items)
            await Exec(c, "INSERT INTO owned_items(id,character_id,item_definition_id,quantity) VALUES(@i,@c,@d,@q)", ("i", item.id), ("c", character), ("d", Guid.Parse(item.definition)), ("q", item.quantity));
    }

    private async Task Cleanup(NpgsqlConnection c)
    {
        await Exec(c, "DELETE FROM character_equipment WHERE character_id=@c; DELETE FROM owned_items WHERE character_id=@c; DELETE FROM player_characters WHERE id=@c AND account_id=@a; DELETE FROM accounts WHERE id=@a AND provider_subject=@s", ("c", character), ("a", account), ("s", subject));
    }

    private async Task<NpgsqlTransaction> BlockCharacter(NpgsqlConnection c)
    {
        var tx = await c.BeginTransactionAsync();
        await using var cmd = new NpgsqlCommand("SELECT id FROM player_characters WHERE id=@id FOR UPDATE", c, tx);
        cmd.Parameters.AddWithValue("id", character); await cmd.ExecuteScalarAsync(); return tx;
    }

    private async Task AwaitBlocked(NpgsqlConnection admin, int count)
    {
        // A statistics snapshot is cached for the lifetime of a transaction.
        // Observe from a separate autocommit connection, not the lock holder.
        await using var observer = new NpgsqlConnection(Admin);
        await observer.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (true)
        {
            await using var cmd = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE application_name=@name AND wait_event_type='Lock'", observer);
            cmd.Parameters.AddWithValue("name", applicationName);
            if ((long)(await cmd.ExecuteScalarAsync(timeout.Token))! >= count) return;
            await Task.Delay(50, timeout.Token);
        }
    }

    [Live09B5Fact]
    public async Task Published_snapshot_and_concurrent_unequip_are_consistent()
    {
        await using var admin = await OpenAdmin();
        var weapon = Guid.NewGuid();
        try
        {
            await Setup(admin, (weapon, OneHand, 1));
            await Exec(admin, "UPDATE player_characters SET experience=100,row_revision=2 WHERE id=@id AND row_revision=1", ("id", character));
            using var factory = new Factory(App, subject, token);
            using var client = factory.CreateClient();
            const string path = "/api/v1/dev/character/gameplay-snapshot";
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
            client.DefaultRequestHeaders.Add("X-Ashenfall-Dev-Token", token);
            var first = await client.GetFromJsonAsync<CharacterGameplaySnapshot>(path);
            Assert.NotNull(first);
            Assert.Equal(character, first.CharacterId);
            Assert.Equal(2, first.CharacterRevision);
            Assert.Equal(Guid.Parse(ClassId), first.ClassDefinitionId);
            Assert.Equal(fixtureRelease, first.CatalogReleaseId);
            Assert.Equal(fixtureVersion, first.CatalogVersion);
            Assert.Equal(fixtureSnapshotHash, first.CatalogHash);
            Assert.Equal((2, 105, 27), (first.EffectiveStats.Level, first.EffectiveStats.MaxHealth, first.EffectiveStats.FixedDamage));
            var service = Service(out var chars, out var catalogs);
            await using (chars) await using (catalogs) await service.EquipAsync(account, weapon, "main_hand", 2, default);
            var equipped = await client.GetFromJsonAsync<CharacterGameplaySnapshot>(path);
            Assert.NotNull(equipped);
            Assert.Equal(3, equipped.CharacterRevision);
            Assert.Equal((121, 30), (equipped.EffectiveStats.MaxHealth, equipped.EffectiveStats.FixedDamage));
            var gate = new PauseAfterAccountRead();
            var options = new DbContextOptionsBuilder<CharacterDbContext>().UseNpgsql(RequireIsolated(App, "ashenfall_app")).AddInterceptors(gate).Options;
            await using var db = new CharacterDbContext(options);
            var pending = new CharacterGameplaySnapshotReader(db).ReadAsync("local_development", subject, default);
            try
            {
                await gate.Seen.Task.WaitAsync(TimeSpan.FromSeconds(8));
                var unequip = Service(out var c, out var k);
                await using (c) await using (k) await unequip.UnequipAsync(account, "main_hand", 3, default);
            }
            finally { gate.Continue.TrySetResult(); }
            var old = await pending;
            Assert.True(gate.VerifiedReadOnlyRepeatableRead);
            Assert.NotNull(old);
            Assert.Equal((3L, 121, 30), (old.CharacterRevision, old.EffectiveStats.MaxHealth, old.EffectiveStats.FixedDamage));
            var fresh = await client.GetFromJsonAsync<CharacterGameplaySnapshot>(path);
            Assert.NotNull(fresh);
            Assert.Equal((4L, 105, 27), (fresh.CharacterRevision, fresh.EffectiveStats.MaxHealth, fresh.EffectiveStats.FixedDamage));
            Assert.Equal(fresh, await client.GetFromJsonAsync<CharacterGameplaySnapshot>(path));
        }
        finally { await Cleanup(admin); }
        await using var verify = new NpgsqlCommand("SELECT (SELECT count(*) FROM accounts WHERE id=@a)+(SELECT count(*) FROM player_characters WHERE id=@c)+(SELECT count(*) FROM owned_items WHERE id=@i)", admin);
        verify.Parameters.AddWithValue("a", account); verify.Parameters.AddWithValue("c", character); verify.Parameters.AddWithValue("i", weapon);
        Assert.Equal(0L, (long)(await verify.ExecuteScalarAsync())!);
    }

    private CharacterService Service(out CharacterDbContext characters, out CatalogDbContext catalog)
    {
        characters = new CharacterDbContext(new DbContextOptionsBuilder<CharacterDbContext>().UseNpgsql(App).Options);
        catalog = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(App).Options);
        return new CharacterService(characters, catalog);
    }

    [Live09B5Fact]
    public async Task Http_same_slot_and_opposing_hands_use_actual_endpoint_and_row_lock()
    {
        await using var admin = await OpenAdmin();
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var off = Guid.NewGuid();
        try
        {
            await Setup(admin, (first, OneHand, 1), (second, TwoHand, 1), (off, OffHand, 1));
            using var factory = new Factory(App, subject, token);
            using var a = factory.CreateClient(); using var b = factory.CreateClient();
            using (var scope = factory.Services.CreateScope())
            {
                RequireIsolated(scope.ServiceProvider.GetRequiredService<CharacterDbContext>().Database.GetConnectionString()!, "ashenfall_app");
                RequireIsolated(scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.GetConnectionString()!, "ashenfall_app");
            }
            using (var readClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }))
            {
                var persisted = await new PersistedCharacterClient(readClient).LoadAsync(new Uri("http://localhost/"), token, character);
                Assert.Equal(character, persisted.Id);
                await Assert.ThrowsAsync<InvalidOperationException>(() => new PersistedCharacterClient(readClient).LoadAsync(new Uri("http://localhost/"), token, Guid.NewGuid()));
                readClient.DefaultRequestHeaders.Add("X-Ashenfall-Dev-Token", token);
                // Published fixture has complete rules.
                Assert.Equal(HttpStatusCode.OK,
                    (await readClient.GetAsync("/api/v1/dev/character/gameplay-snapshot")).StatusCode);
            }
            using (var absentFactory = new Factory(App, "09c1-absent-" + Guid.NewGuid().ToString("N"), token))
            using (var absentClient = absentFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }))
            {
                var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => new PersistedCharacterClient(absentClient).LoadAsync(new Uri("http://localhost/"), token, character));
                Assert.Contains("not found", missing.Message);
                absentClient.DefaultRequestHeaders.Add("X-Ashenfall-Dev-Token", token);
                Assert.Equal(HttpStatusCode.NotFound,
                    (await absentClient.GetAsync("/api/v1/dev/character/gameplay-snapshot")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound,
                    (await absentClient.GetAsync($"/api/v1/dev/character/gameplay-snapshot?accountId={account}&characterId={character}")).StatusCode);
            }
            a.DefaultRequestHeaders.Add("X-Ashenfall-Dev-Token", token); b.DefaultRequestHeaders.Add("X-Ashenfall-Dev-Token", token);
            var path = "/api/v1/dev/character/equipment";
            await using (var tx = await BlockCharacter(admin))
            {
                var x = a.PutAsJsonAsync(path, new EquipmentRequest(first, "main_hand", 1));
                var y = b.PutAsJsonAsync(path, new EquipmentRequest(second, "main_hand", 1));
                await AwaitBlocked(admin, 2); await tx.CommitAsync();
                var codes = (await Task.WhenAll(x, y)).Select(r => r.StatusCode).Order().ToArray();
                Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }.Order(), codes);
            }
            await using (var verify = new NpgsqlCommand("SELECT row_revision,(SELECT count(*) FROM character_equipment WHERE character_id=@id AND slot_code='main_hand' AND owned_item_id IN (@first,@second)),(SELECT count(*) FROM owned_items WHERE character_id=@id AND quantity=1 AND row_revision=1) FROM player_characters WHERE id=@id", admin))
            { verify.Parameters.AddWithValue("id", character); verify.Parameters.AddWithValue("first", first); verify.Parameters.AddWithValue("second", second); await using var reader = await verify.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync()); Assert.Equal(2L, reader.GetInt64(0)); Assert.Equal(1L, reader.GetInt64(1)); Assert.Equal(3L, reader.GetInt64(2)); }
            // Reset only this unique test character, preserving revision contract.
            await Exec(admin, "DELETE FROM character_equipment WHERE character_id=@id; UPDATE player_characters SET row_revision=3 WHERE id=@id", ("id", character));
            await using (var tx = await BlockCharacter(admin))
            {
                var x = a.PutAsJsonAsync(path, new EquipmentRequest(second, "main_hand", 3));
                var y = b.PutAsJsonAsync(path, new EquipmentRequest(off, "off_hand", 3));
                await AwaitBlocked(admin, 2); await tx.CommitAsync();
                var codes = (await Task.WhenAll(x, y)).Select(r => r.StatusCode).Order().ToArray();
                Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }.Order(), codes);
            }
            await using var final = new NpgsqlCommand("SELECT row_revision,(SELECT count(*) FROM character_equipment WHERE character_id=@id AND ((slot_code='main_hand' AND owned_item_id=@two) OR (slot_code='off_hand' AND owned_item_id=@off))),(SELECT count(*) FROM owned_items WHERE character_id=@id AND quantity=1 AND row_revision=1) FROM player_characters WHERE id=@id", admin);
            final.Parameters.AddWithValue("id", character); final.Parameters.AddWithValue("two", second); final.Parameters.AddWithValue("off", off);
            await using (var result = await final.ExecuteReaderAsync()) { Assert.True(await result.ReadAsync()); Assert.Equal(4L, result.GetInt64(0)); Assert.Equal(1L, result.GetInt64(1)); Assert.Equal(3L, result.GetInt64(2)); }
            await using var equipped = new NpgsqlCommand("SELECT slot_code FROM character_equipment WHERE character_id=@id", admin); equipped.Parameters.AddWithValue("id", character);
            var winningSlot = (string?)await equipped.ExecuteScalarAsync();
            var incompatible = winningSlot == "main_hand" ? new EquipmentRequest(off, "off_hand", 4) : new EquipmentRequest(second, "main_hand", 4);
            var rejected = await a.PutAsJsonAsync(path, incompatible);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            await using var unchanged = new NpgsqlCommand("SELECT row_revision,(SELECT count(*) FROM character_equipment WHERE character_id=@id) FROM player_characters WHERE id=@id", admin);
            unchanged.Parameters.AddWithValue("id", character); await using var after = await unchanged.ExecuteReaderAsync(); Assert.True(await after.ReadAsync()); Assert.Equal(4L, after.GetInt64(0)); Assert.Equal(1L, after.GetInt64(1));
        }
        finally { await Cleanup(admin); }
    }

    [Live09B5Fact]
    public async Task Service_equip_races_quantity_and_delete_with_real_contexts()
    {
        await using var admin = await OpenAdmin(); var weapon = Guid.NewGuid(); var stack = Guid.NewGuid();
        try
        {
            await Setup(admin, (weapon, OneHand, 1), (stack, Stack, 2));
            var s1 = Service(out var c1, out var k1); var s2 = Service(out var c2, out var k2);
            await using (c1) await using (k1) await using (c2) await using (k2)
            {
                await using var tx = await BlockCharacter(admin);
                var equip = s1.EquipAsync(account, weapon, "main_hand", 1, default);
                var update = s2.SetQuantityAsync(character, weapon, 1, 1, default);
                await AwaitBlocked(admin, 2); await tx.CommitAsync();
                var equipError = await Record.ExceptionAsync(() => equip);
                var updateError = await Record.ExceptionAsync(() => update);
                Assert.True((equipError is null && updateError is ArgumentException) || (equipError is DbUpdateConcurrencyException && updateError is null));
            }
            // An equipped item cannot be deleted; if quantity update won, equip at current revision.
            await using var read = new NpgsqlCommand("SELECT row_revision,(SELECT count(*) FROM character_equipment WHERE character_id=@id) FROM player_characters WHERE id=@id", admin);
            read.Parameters.AddWithValue("id", character); long revision; long count;
            await using (var reader = await read.ExecuteReaderAsync()) { Assert.True(await reader.ReadAsync()); revision = reader.GetInt64(0); count = reader.GetInt64(1); }
            if (count == 0) { var svc = Service(out var c, out var k); await using (c) await using (k) await svc.EquipAsync(account, weapon, "main_hand", revision, default); }
            var deleteService = Service(out var dc, out var dk); await using (dc) await using (dk)
                await Assert.ThrowsAsync<ArgumentException>(() => deleteService.DeleteItemAsync(character, weapon, count == 0 ? 2 : 1, default));
            long currentCharacterRevision = count == 0 ? revision + 1 : revision;
            long currentItemRevision = count == 0 ? 2 : 1;
            var equipAgain = Service(out var ec, out var ek); var deleteAgain = Service(out var rc, out var rk);
            await using (ec) await using (ek) await using (rc) await using (rk)
            {
                await using var tx = await BlockCharacter(admin);
                var e = equipAgain.EquipAsync(account, weapon, "main_hand", currentCharacterRevision, default);
                var d = deleteAgain.DeleteItemAsync(character, weapon, currentItemRevision, default);
                await AwaitBlocked(admin, 2); await tx.CommitAsync();
                var equipFailure = await Record.ExceptionAsync(() => e);
                var deleteFailure = await Record.ExceptionAsync(() => d);
                Assert.Null(equipFailure); Assert.IsType<ArgumentException>(deleteFailure);
            }
            // Two independent real services use the same item revision under a held parent lock.
            var u1 = Service(out var uc1, out var uk1); var u2 = Service(out var uc2, out var uk2);
            await using (uc1) await using (uk1) await using (uc2) await using (uk2)
            {
                await using var tx = await BlockCharacter(admin);
                var x = u1.SetQuantityAsync(character, stack, 1, 3, default); var y = u2.SetQuantityAsync(character, stack, 1, 4, default);
                await AwaitBlocked(admin, 2); await tx.CommitAsync();
                var e1 = await Record.ExceptionAsync(() => x); var e2 = await Record.ExceptionAsync(() => y);
                Assert.True((e1 is null && e2 is DbUpdateConcurrencyException) || (e2 is null && e1 is DbUpdateConcurrencyException));
            }
            var limits = Service(out var lc, out var lk);
            await using (lc) await using (lk)
            {
                await Assert.ThrowsAsync<ArgumentException>(() => limits.SetQuantityAsync(character, stack, 2, 6, default));
                await Assert.ThrowsAsync<ArgumentException>(() => limits.SetQuantityAsync(character, stack, 2, 0, default));
            }
            await using var final = new NpgsqlCommand("SELECT quantity,row_revision,(SELECT count(*) FROM character_equipment WHERE character_id=@character AND owned_item_id=@weapon),(SELECT count(*) FROM owned_items WHERE id=@weapon AND quantity=1),(SELECT row_revision FROM player_characters WHERE id=@character) FROM owned_items WHERE id=@id", admin); final.Parameters.AddWithValue("id", stack); final.Parameters.AddWithValue("character", character); final.Parameters.AddWithValue("weapon", weapon);
            await using var r = await final.ExecuteReaderAsync(); Assert.True(await r.ReadAsync()); Assert.Contains(r.GetInt32(0), new[] { 3, 4 }); Assert.Equal(2L, r.GetInt64(1)); Assert.Equal(1L, r.GetInt64(2)); Assert.Equal(1L, r.GetInt64(3)); Assert.Equal(currentCharacterRevision + 2, r.GetInt64(4));
        }
        finally { await Cleanup(admin); }
    }

    [Live09B5Fact]
    public async Task Different_characters_do_not_share_the_same_lock()
    {
        await using var admin = await OpenAdmin();
        var otherAccount = Guid.NewGuid(); var otherCharacter = Guid.NewGuid();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var otherSubject = "09b5-" + Guid.NewGuid().ToString("N");
        try
        {
            await Setup(admin, (first, OneHand, 1));
            await Exec(admin, "INSERT INTO accounts(id,identity_provider,provider_subject) VALUES(@a,'local_development',@s); INSERT INTO player_characters(id,account_id,class_definition_id,character_name) VALUES(@c,@a,@class,'09b5 other'); INSERT INTO owned_items(id,character_id,item_definition_id,quantity) VALUES(@i,@c,@def,1)", ("a", otherAccount), ("s", otherSubject), ("c", otherCharacter), ("class", Guid.Parse(ClassId)), ("i", second), ("def", Guid.Parse(OneHand)));
            var s1 = Service(out var c1, out var k1); var s2 = Service(out var c2, out var k2);
            await using (c1) await using (k1) await using (c2) await using (k2)
            {
                await using var tx = await BlockCharacter(admin);
                var blocked = s1.EquipAsync(account, first, "main_hand", 1, default);
                await AwaitBlocked(admin, 1);
                await s2.EquipAsync(otherAccount, second, "main_hand", 1, default).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(blocked.IsCompleted);
                await tx.CommitAsync(); await blocked;
            }
            await using var verify = new NpgsqlCommand("SELECT count(*) FROM character_equipment e JOIN player_characters c ON c.id=e.character_id WHERE c.id IN (@first,@second) AND c.row_revision=2 AND e.slot_code='main_hand'", admin);
            verify.Parameters.AddWithValue("first", character); verify.Parameters.AddWithValue("second", otherCharacter);
            Assert.Equal(2L, (long)(await verify.ExecuteScalarAsync())!);
        }
        finally
        {
            await Exec(admin, "DELETE FROM character_equipment WHERE character_id=@c; DELETE FROM owned_items WHERE character_id=@c; DELETE FROM player_characters WHERE id=@c AND account_id=@a; DELETE FROM accounts WHERE id=@a AND provider_subject=@s", ("c", otherCharacter), ("a", otherAccount), ("s", otherSubject));
            await Cleanup(admin);
        }
    }

    [Live09B5Fact]
    public async Task Snapshot_reader_sees_consistent_character_when_soft_delete_commits_concurrently()
    {
        await using var admin = await OpenAdmin();
        try
        {
            await Setup(admin);
            var gate = new PauseAfterAccountRead();
            var options = new DbContextOptionsBuilder<CharacterDbContext>().UseNpgsql(RequireIsolated(App, "ashenfall_app"))
                .AddInterceptors(gate).Options;
            await using var db = new CharacterDbContext(options);
            var read = new CharacterGameplaySnapshotReader(db).ReadAsync("local_development", subject, default);
            try
            {
                await gate.Seen.Task.WaitAsync(TimeSpan.FromSeconds(8));
                await Exec(admin, "UPDATE player_characters SET deleted_at=now(),row_revision=2 WHERE id=@id AND row_revision=1", ("id", character));
            }
            finally { gate.Continue.TrySetResult(); }
            // A complete old snapshot must survive the concurrent delete.
            Assert.Equal(character, (await read)!.CharacterId);
            Assert.True(gate.VerifiedReadOnlyRepeatableRead);
            await using var fresh = new CharacterDbContext(new DbContextOptionsBuilder<CharacterDbContext>().UseNpgsql(App).Options);
            Assert.Null(await new CharacterGameplaySnapshotReader(fresh).ReadAsync("local_development", subject, default));
        }
        finally { await Cleanup(admin); }
    }
}
