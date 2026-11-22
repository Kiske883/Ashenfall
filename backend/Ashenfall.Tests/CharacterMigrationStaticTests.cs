using System.Text.RegularExpressions;
using Ashenfall.Api.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Ashenfall.Tests;

public sealed class CharacterMigrationStaticTests
{
    private static string Script(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "MigrationReview", name));

    [Fact]
    public void ForwardScriptFailsClosedAndCreatesOnlyReviewedTables()
    {
        string sql = Script("CharacterPersistence09B.sql");
        Assert.Contains("\\set ON_ERROR_STOP on", sql);
        Assert.Contains("BEGIN;", sql);
        Assert.EndsWith("COMMIT;\n", sql.Replace("\r\n", "\n"));
        Assert.Contains("current_database() <> 'ashenfall_dev'", sql);
        Assert.Contains("20261115111756_InitialRpgCatalog", sql);
        Assert.Contains("prototype-1", sql);
        foreach (string name in new[] { "accounts", "player_characters", "class_level_rules",
            "item_stat_modifiers", "owned_items", "character_equipment" })
            Assert.Single(Regex.Matches(sql, $@"(?m)^CREATE TABLE {name} \(").Cast<Match>());
        Assert.DoesNotContain("CREATE TABLE catalog_releases", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
        Assert.DoesNotContain("GRANT CREATE", sql);
        Assert.Equal(4, Regex.Matches(sql, @"(?m)^CREATE FUNCTION ").Count);
        Assert.Equal(5, Regex.Matches(sql, @"(?m)^CREATE TRIGGER ").Count);
        Assert.Contains("BEFORE INSERT OR UPDATE OR DELETE ON class_level_rules", sql);
        Assert.Contains("BEFORE INSERT OR UPDATE OR DELETE ON item_stat_modifiers", sql);
        Assert.Contains("BEFORE INSERT OR UPDATE ON player_characters", sql);
        Assert.Contains("BEFORE INSERT OR UPDATE OR DELETE ON owned_items", sql);
        Assert.Contains("BEFORE INSERT OR UPDATE ON character_equipment", sql);
        Assert.Contains("SET search_path = public, pg_catalog AS $$", sql);
        Assert.Contains("GRANT SELECT ON class_level_rules, item_stat_modifiers TO ashenfall_app", sql);
        Assert.Contains("TO ashenfall_publisher", sql);
        Assert.Contains("GRANT DELETE ON owned_items TO ashenfall_app", sql);
        Assert.Contains("REVOKE ALL ON accounts", sql);
        foreach (string trigger in new[] { "class_level_rule_catalog_guard", "item_modifier_catalog_guard",
            "character_mutation_guard", "owned_item_mutation_guard", "equipment_mutation_guard" })
            Assert.Contains($"CREATE TRIGGER {trigger}", sql);
        Assert.Contains("GRANT SELECT, INSERT ON accounts TO ashenfall_app", sql);
        Assert.Contains("GRANT DELETE ON character_equipment TO ashenfall_app", sql);
        Assert.DoesNotContain("GRANT DELETE ON player_characters", sql);
    }

    [Fact]
    public void ReviewRollbackRefusesAnyPersistedData()
    {
        string sql = Script("CharacterPersistence09B.Rollback.sql");
        int transaction = sql.IndexOf("BEGIN;", StringComparison.Ordinal);
        int isolation = sql.IndexOf("SET TRANSACTION ISOLATION LEVEL READ COMMITTED;", StringComparison.Ordinal);
        int lockTimeout = sql.IndexOf("SET LOCAL lock_timeout = '5s';", StringComparison.Ordinal);
        int statementTimeout = sql.IndexOf("SET LOCAL statement_timeout = '30s';", StringComparison.Ordinal);
        int lockAt = sql.IndexOf("LOCK TABLE accounts, player_characters, owned_items, character_equipment,", StringComparison.Ordinal);
        int emptyAt = sql.IndexOf("DO $empty_data$", StringComparison.Ordinal);
        int dropAt = sql.IndexOf("DROP TABLE character_equipment;", StringComparison.Ordinal);
        int commitAt = sql.LastIndexOf("COMMIT;", StringComparison.Ordinal);
        Assert.True(transaction >= 0 && transaction < isolation && isolation < lockAt);
        Assert.True(lockTimeout > isolation && lockTimeout < lockAt);
        Assert.True(statementTimeout > isolation && statementTimeout < lockAt);
        Assert.True(lockAt < emptyAt && emptyAt < dropAt && dropAt < commitAt);
        Assert.Contains("class_level_rules, item_stat_modifiers IN ACCESS EXCLUSIVE MODE;", sql);
        foreach (string name in new[] { "accounts", "player_characters", "class_level_rules",
            "item_stat_modifiers", "owned_items", "character_equipment" })
        {
            Assert.Contains($"EXISTS (SELECT 1 FROM {name})", sql[emptyAt..dropAt]);
            Assert.Single(Regex.Matches(sql, $@"(?m)^DROP TABLE {name};").Cast<Match>());
        }
        Assert.Equal(4, Regex.Matches(sql, @"(?m)^DROP FUNCTION ").Count);
        Assert.Contains("rollback refused: persistent or authored data exists", sql);
        Assert.DoesNotContain("DROP TABLE catalog_releases", sql);
        Assert.DoesNotContain("CASCADE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("COMMIT;\n", sql.Replace("\r\n", "\n"));
    }

    [Fact]
    public void EveryMappedCharacterColumnExistsInReviewedSql()
    {
        string sql = Script("CharacterPersistence09B.sql");
        var options = new DbContextOptionsBuilder<CharacterDbContext>()
            .UseNpgsql("Host=invalid.local;Database=not_used").Options;
        using var db = new CharacterDbContext(options);
        foreach (var entity in db.Model.GetEntityTypes())
        {
            string table = entity.GetTableName()!;
            var match = Regex.Match(sql, $@"(?s)CREATE TABLE {table} \((.*?)\n\);",
                RegexOptions.CultureInvariant);
            Assert.True(match.Success, $"Missing SQL table {table}.");
            var store = StoreObjectIdentifier.Table(table, null);
            foreach (var property in entity.GetProperties())
            {
                string column = property.GetColumnName(store)!;
                Assert.Matches($@"(?m)^\s*{Regex.Escape(column)}\s+", match.Groups[1].Value);
            }
        }
    }
}
