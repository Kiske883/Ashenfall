using System.Data;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Persistence;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Api.Characters;
using Ashenfall.Tests;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ashenfall.TestCatalogSeeder;

if (args.Length == 1 && args[0] == "--prepare-v2-test-candidate")
    return await V2TestCatalogActivation.PrepareCommandAsync();
if (args.Length == 2 && args[0] == "--activate-v2-test-candidate" && Guid.TryParse(args[1], out var candidateId))
    return await V2TestCatalogActivation.ActivateCommandAsync(candidateId);

// Explicit standalone test tool. No configurable database, host, user or password.
if (args.Length != 1 || args[0] != "--publish-isolated-test-fixture")
{
    Console.Error.WriteLine("Explicit --publish-isolated-test-fixture argument required.");
    return 2;
}
try
{
    await using var connection = new NpgsqlConnection("Host=localhost;Port=5432;Database=ashenfall_09b3_test;Username=postgres;Pooling=false;Timeout=5");
    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable);
    await using var preflight = new NpgsqlCommand("""
        SELECT current_database()='ashenfall_09b3_test' AND current_user='postgres'
        AND inet_server_port()=5432 AND inet_server_addr() IS NOT NULL
        AND (inet_server_addr() <<= '127.0.0.0/8'::inet OR inet_server_addr()='::1'::inet)
        AND (SELECT system_identifier::text FROM pg_control_system())='7708337984335018212'
        AND (SELECT count(*) FROM pg_trigger WHERE NOT tgisinternal AND tgenabled='O' AND tgname IN
             ('catalog_release_guard','character_class_catalog_guard','enemy_definition_catalog_guard',
              'item_definition_catalog_guard','class_level_rule_catalog_guard','item_modifier_catalog_guard',
              'character_mutation_guard','owned_item_mutation_guard','equipment_mutation_guard'))=9
        AND to_regclass('public.catalog_one_published') IS NOT NULL
        AND NOT EXISTS(SELECT 1 FROM catalog_releases) AND NOT EXISTS(SELECT 1 FROM character_classes)
        AND NOT EXISTS(SELECT 1 FROM enemy_definitions) AND NOT EXISTS(SELECT 1 FROM item_definitions)
        AND NOT EXISTS(SELECT 1 FROM class_level_rules) AND NOT EXISTS(SELECT 1 FROM item_stat_modifiers)
        AND NOT EXISTS(SELECT 1 FROM accounts) AND NOT EXISTS(SELECT 1 FROM player_characters)
        AND NOT EXISTS(SELECT 1 FROM owned_items) AND NOT EXISTS(SELECT 1 FROM character_equipment)
        """, connection, transaction);
    if ((bool?)await preflight.ExecuteScalarAsync() != true) throw new InvalidOperationException("Test database preflight failed.");

    var (fixture, rules, modifiers) = SnapshotFixtureManifest.Create();
    if (CatalogValidator.Validate(fixture).Count != 0) throw new InvalidOperationException("Invalid catalog fixture.");
    CharacterRules.ValidateCatalogProgression(fixture.Classes.Select(x => x.Id).ToArray(), rules);
    var catalogOptions = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).Options;
    var characterOptions = new DbContextOptionsBuilder<CharacterDbContext>().UseNpgsql(connection).Options;
    await using var catalog = new CatalogDbContext(catalogOptions);
    await using var characters = new CharacterDbContext(characterOptions);
    await catalog.Database.UseTransactionAsync(transaction);
    await characters.Database.UseTransactionAsync(transaction);
    catalog.Releases.Add(fixture.Release);
    await catalog.SaveChangesAsync();
    catalog.Classes.AddRange(fixture.Classes); catalog.Enemies.AddRange(fixture.Enemies); catalog.Items.AddRange(fixture.Items);
    await catalog.SaveChangesAsync();
    characters.LevelRules.AddRange(rules); characters.ItemModifiers.AddRange(modifiers);
    await characters.SaveChangesAsync();
    var persisted = new CatalogSnapshot(await catalog.Releases.AsNoTracking().SingleAsync(),
        await catalog.Classes.AsNoTracking().ToListAsync(), await catalog.Enemies.AsNoTracking().ToListAsync(),
        await catalog.Items.AsNoTracking().ToListAsync());
    var persistedRules = await characters.LevelRules.AsNoTracking().ToListAsync();
    var persistedModifiers = await characters.ItemModifiers.AsNoTracking().ToListAsync();
    if (CatalogValidator.Validate(persisted).Count != 0 || CatalogHash.Compute(persisted) != CatalogHash.Compute(fixture) ||
        persistedModifiers.Count != modifiers.Length) throw new InvalidOperationException("Persisted draft mismatch.");
    CharacterRules.ValidateCatalogProgression(persisted.Classes.Select(x => x.Id).ToArray(), persistedRules);
    var dwarf = persisted.Classes.Single();
    var sample = new CharacterRow { ClassDefinitionId = dwarf.Id, Experience = 100 };
    var plain = CharacterRules.CalculateStats(dwarf.BaseMaxHealth, dwarf.FixedDamage, 0, 0, 0, 0, sample, persistedRules, []);
    var equipped = CharacterRules.CalculateStats(dwarf.BaseMaxHealth, dwarf.FixedDamage, 0, 0, 0, 0, sample, persistedRules, persistedModifiers);
    if ((plain.Level, plain.MaxHealth, plain.Damage) != (2, 105, 27) ||
        (equipped.Level, equipped.MaxHealth, equipped.Damage) != (2, 121, 30))
        throw new InvalidOperationException("Unexpected fixture statistics.");
    fixture.Release.ContentSha256 = CatalogHash.Compute(persisted);
    fixture.Release.Status = CatalogStates.Published;
    fixture.Release.PublishedAt = DateTimeOffset.UtcNow;
    await catalog.SaveChangesAsync();
    var published = await catalog.Releases.AsNoTracking().SingleAsync();
    if (published.Status != CatalogStates.Published || published.ContentSha256 != CatalogHash.Compute(persisted))
        throw new InvalidOperationException("Publication verification failed.");
    await transaction.CommitAsync();
    Console.WriteLine("Isolated synthetic catalog published and validated.");
    return 0;
}
catch (Exception)
{
    Console.Error.WriteLine("Test catalog publication failed; transaction rolled back. Inspect configuration and database state securely.");
    return 1;
}
