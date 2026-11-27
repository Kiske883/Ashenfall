using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Api.Characters;

namespace Ashenfall.CatalogPublisher.Balance;

/// <summary>Pure planning and compatibility validation. Never opens a connection or mutates its input.</summary>
public static class BalancePlanner
{
    public static IReadOnlyList<BalanceDifference> Differences(BalanceCatalog from, BalanceCatalog to)
    {
        var before = BalanceParameters.Discover(from).ToDictionary(x => x.Key, x => x.Value);
        var after = BalanceParameters.Discover(to).ToDictionary(x => x.Key, x => x.Value);
        return before.Keys.Union(after.Keys).Order(StringComparer.Ordinal)
            .Where(x => !before.ContainsKey(x) || !after.ContainsKey(x) || before[x] != after[x])
            .Select(x => new BalanceDifference(x, before.GetValueOrDefault(x), after.GetValueOrDefault(x))).ToArray();
    }
    public static BalancePlan Plan(BalanceState state, Guid operation, string version, IReadOnlyList<BalanceEdit> changes,
        Guid? configurationId = null, Guid? characterId = null)
    {
        if (operation == Guid.Empty || string.IsNullOrWhiteSpace(version) || version.Length > 64 || version.Any(char.IsControl))
            throw new ArgumentException("Explicit unique operation ID and version (1..64 characters) are required.");
        var active = state.Active; active.Verify();
        var source = state.Catalogs.Single(x => x.Catalog.Release.Id == (configurationId ?? active.Catalog.Release.Id));
        if (source.Catalog.Release.Status is not (CatalogStates.Published or CatalogStates.Retired)) throw new ArgumentException("Configuration source must be published or retired.");
        source.Verify();
        var edited = BalanceJson.Copy(source);
        BalanceParameters.Edit(edited, changes);
        var diff = Differences(active, edited);
        bool noOp = ConfigurationSignature(active) == ConfigurationSignature(edited);
        if (!noOp && state.Catalogs.Any(x => x.Catalog.Release.Id == operation || x.Catalog.Release.VersionKey == version))
            throw new ArgumentException("Operation ID or version already exists; choose a new plan identity/version.");
        var candidate = CloneCandidate(state, edited, operation, version);
        Validate(state, candidate);
        var snapshot = characterId is { } id ? Snapshot(state, noOp ? active : candidate, id) : null;
        return new BalancePlan(1, operation, version, active.Catalog.Release.Id, active.Hash,
            source.Catalog.Release.Id, source.Hash, changes.ToArray(), characterId, noOp ? active.Hash : candidate.Hash, noOp, diff,
            candidate.ClassLinks.Count, candidate.ItemLinks.Count, snapshot);
    }

    public static BalanceCatalog Materialize(BalanceState state, BalancePlan plan)
    {
        if (plan.FormatVersion != 1 || state.Active.Catalog.Release.Id != plan.ExpectedActiveId || state.Active.Hash != plan.ExpectedActiveHash)
            throw new InvalidOperationException("Stale or unsupported plan; replan against the active catalog.");
        var source = state.Catalogs.Single(x => x.Catalog.Release.Id == plan.ConfigurationId);
        if (source.Hash != plan.ConfigurationHash) throw new InvalidOperationException("Configuration source hash changed.");
        var regenerated = Plan(state, plan.OperationId, plan.Version, plan.Changes, plan.ConfigurationId, plan.CharacterId);
        if (regenerated.TargetHash != plan.TargetHash || regenerated.NoOp != plan.NoOp ||
            regenerated.ClassMappings != plan.ClassMappings || regenerated.ItemMappings != plan.ItemMappings ||
            JsonSerializer.Serialize(regenerated.ExpectedSnapshot) != JsonSerializer.Serialize(plan.ExpectedSnapshot) ||
            JsonSerializer.Serialize(regenerated.Differences) != JsonSerializer.Serialize(plan.Differences))
            throw new InvalidOperationException("Plan content changed; obtain a fresh plan.");
        var edited = BalanceJson.Copy(source); BalanceParameters.Edit(edited, plan.Changes);
        return CloneCandidate(state, edited, plan.OperationId, plan.Version);
    }

    public static BalanceCatalog CloneCandidate(BalanceState state, BalanceCatalog source, Guid operation, string version)
    {
        var copied = BalanceJson.Copy(source);
        var classMap = copied.Catalog.Classes.ToDictionary(x => x.Id, x => BalanceJson.Identity(operation, x.Id));
        var itemMap = copied.Catalog.Items.ToDictionary(x => x.Id, x => BalanceJson.Identity(operation, x.Id));
        foreach (var c in copied.Catalog.Classes) { c.Id = classMap[c.Id]; c.CatalogReleaseId = operation; }
        foreach (var i in copied.Catalog.Items) { i.Id = itemMap[i.Id]; i.CatalogReleaseId = operation; }
        foreach (var e in copied.Catalog.Enemies) { e.Id = BalanceJson.Identity(operation, e.Id); e.CatalogReleaseId = operation; }
        var release = new CatalogRelease { Id = operation, VersionKey = version, SchemaMajor = source.Catalog.Release.SchemaMajor,
            Status = CatalogStates.Draft, CreatedAt = DateTimeOffset.UnixEpoch };
        var candidate = new BalanceCatalog { Catalog = copied.Catalog with { Release = release },
            Combat = copied.Combat.Select(x => x with { ClassId = classMap[x.ClassId] }).ToList(),
            Levels = copied.Levels.Select(x => x with { ClassId = classMap[x.ClassId] }).ToList(),
            Modifiers = copied.Modifiers.Select(x => x with { ItemId = itemMap[x.ItemId] }).ToList(),
            ClassLinks = MapHistory(state, classMap, true), ItemLinks = MapHistory(state, itemMap, false) };
        release.ContentSha256 = CatalogHash.Compute(candidate.Catalog);
        candidate.Hash = candidate.ComputeHash();
        return candidate;
    }

    private static List<BalanceLink> MapHistory(BalanceState state, Dictionary<Guid, Guid> selected, bool classes)
    {
        // Resolve only explicit persisted identity relationships. Never infer a mapping from a name/code.
        var edges = state.Catalogs.Where(x => x.Catalog.Release.Status is CatalogStates.Published or CatalogStates.Retired)
            .SelectMany(x => classes ? x.ClassLinks : x.ItemLinks).ToArray();
        var known = state.Catalogs.Where(x => x.Catalog.Release.Status is CatalogStates.Published or CatalogStates.Retired)
            .SelectMany(x => classes ? x.Catalog.Classes.Select(i => i.Id) : x.Catalog.Items.Select(i => i.Id)).ToHashSet();
        var result = new Dictionary<Guid, Guid>();
        foreach (var pair in selected)
        {
            var component = new HashSet<Guid> { pair.Key }; var queue = new Queue<Guid>(); queue.Enqueue(pair.Key);
            while (queue.TryDequeue(out var current))
                foreach (var edge in edges.Where(x => x.SourceId == current || x.TargetId == current))
                {
                    var other = edge.SourceId == current ? edge.TargetId : edge.SourceId;
                    if (component.Add(other)) queue.Enqueue(other);
                }
            foreach (var id in component.Where(known.Contains))
            {
                if (result.TryGetValue(id, out var previous) && previous != pair.Value)
                    throw new ArgumentException("Ambiguous historical identity component.");
                result[id] = pair.Value;
            }
        }
        return result.OrderBy(x => x.Key).Select(x => new BalanceLink(x.Key, x.Value)).ToList();
    }

    public static void Validate(BalanceState state, BalanceCatalog candidate)
    {
        candidate.Verify();
        var errors = CatalogValidator.Validate(candidate.Catalog);
        if (errors.Count != 0) throw new ArgumentException(string.Join(" ", errors));
        var allClasses = state.Catalogs.SelectMany(x => x.Catalog.Classes).ToArray();
        var allItems = state.Catalogs.SelectMany(x => x.Catalog.Items).ToArray();
        foreach (var link in candidate.ClassLinks)
            if (allClasses.Single(x => x.Id == link.SourceId).Code != candidate.Catalog.Classes.Single(x => x.Id == link.TargetId).Code)
                throw new ArgumentException("Historical class identity is incompatible.");
        foreach (var link in candidate.ItemLinks)
        {
            var old = allItems.Single(x => x.Id == link.SourceId); var next = candidate.Catalog.Items.Single(x => x.Id == link.TargetId);
            if (old.Code != next.Code || old.Category != next.Category || old.EquipSlot != next.EquipSlot ||
                old.HandRequirement != next.HandRequirement || old.Stackable != next.Stackable || old.MaxStack != next.MaxStack)
                throw new ArgumentException("Historical item identity is incompatible.");
        }
        if (candidate.Modifiers.Any(x => x.Amount is < -100000 or > 100000 || decimal.Round(x.Amount, 4) != x.Amount ||
            (x.StatCode is "critical_chance" or "critical_multiplier") && x.ModifierKind != "flat"))
            throw new ArgumentException("Invalid modifier precision, kind or bounds.");
        CatalogInventoryCompatibility.Validate(candidate.Catalog, state.Characters, state.OwnedItems, state.Equipment,
            allItems, candidate.ItemLinks.Select(x => (x.SourceId, x.TargetId)).ToArray());
        foreach (var cls in candidate.Catalog.Classes)
            foreach (var l in candidate.Levels.Where(x => x.ClassId == cls.Id))
            {
                var sample = new CharacterRow { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), ClassDefinitionId = cls.Id,
                    Experience = l.RequiredTotalXp, RowRevision = 1 };
                var prospective = Published(candidate);
                GameplaySnapshotV2Factory.Create(sample, cls, prospective.Release, prospective, candidate.Combat, candidate.Levels,
                    candidate.Modifiers, candidate.ClassLinks.Select(x => (x.SourceId, x.TargetId)).ToArray(), [], candidate.Hash,
                    candidate.ItemLinks.Select(x => (x.SourceId, x.TargetId)).ToArray());
            }
        foreach (var p in state.Characters)
        {
            var original = allClasses.Single(x => x.Id == p.ClassDefinitionId);
            var originRelease = state.Catalogs.Single(x => x.Catalog.Release.Id == original.CatalogReleaseId).Catalog.Release;
            var target = CatalogClassTransition.Resolve(original, originRelease, Published(candidate).Release,
                candidate.Catalog.Classes, candidate.ClassLinks.Select(x => (x.SourceId, x.TargetId)).ToArray());
            CharacterRules.ValidateAllocation(p.Experience, p.AllocatedStrength, p.AllocatedDexterity, p.AllocatedVitality,
                candidate.Levels.Where(x => x.ClassId == target.Id).Select(x => new LevelRuleRow { ClassDefinitionId = x.ClassId,
                    Level = x.Level, RequiredTotalXp = x.RequiredTotalXp, AttributePoints = x.AttributePoints }).ToArray());
            if (p.DeletedAt is null) Snapshot(state, candidate, p.Id);
        }
    }

    private static CatalogSnapshot Published(BalanceCatalog c)
    {
        var release = BalanceJson.Copy(c.Catalog.Release); release.Status = CatalogStates.Published;
        release.PublishedAt = DateTimeOffset.UnixEpoch; return c.Catalog with { Release = release };
    }
    public static CharacterGameplaySnapshotV2 Snapshot(BalanceState state, BalanceCatalog candidate, Guid character)
    {
        var player = state.Characters.Single(x => x.Id == character && x.DeletedAt is null);
        var original = state.Catalogs.SelectMany(x => x.Catalog.Classes).Single(x => x.Id == player.ClassDefinitionId);
        var releases = state.Catalogs.Select(x => x.Catalog.Release).ToArray();
        var items = state.Catalogs.SelectMany(x => x.Catalog.Items).ToArray();
        var equipped = state.Equipment.Where(x => x.CharacterId == character)
            .Select(x => state.OwnedItems.Single(i => i.Id == x.OwnedItemId && i.CharacterId == character).ItemDefinitionId).ToArray();
        return GameplaySnapshotV2Factory.Create(player, original, releases.Single(x => x.Id == original.CatalogReleaseId),
            Published(candidate), candidate.Combat, candidate.Levels, candidate.Modifiers,
            candidate.ClassLinks.Select(x => (x.SourceId, x.TargetId)).ToArray(), equipped, candidate.Hash,
            candidate.ItemLinks.Select(x => (x.SourceId, x.TargetId)).ToArray(), items, releases);
    }

    public static string ConfigurationSignature(BalanceCatalog c)
    {
        // Ignore version/UUID provenance, preserve authored content including metadata and structure.
        var view = new {
            Parameters = BalanceParameters.Discover(c).Select(x => new { x.Key, x.Value }).ToArray(),
            Classes = c.Catalog.Classes.OrderBy(x => x.Code).Select(x => new { x.Code, x.DisplayName, x.VisualKey }),
            Enemies = c.Catalog.Enemies.OrderBy(x => x.Code).Select(x => new { x.Code, x.DisplayName, x.VisualKey }),
            Items = c.Catalog.Items.OrderBy(x => x.Code).Select(x => new { x.Code, x.DisplayName, x.VisualKey, x.Category, x.Rarity, x.Stackable, x.MaxStack, x.EquipSlot, x.HandRequirement }) };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(view))));
    }
}
