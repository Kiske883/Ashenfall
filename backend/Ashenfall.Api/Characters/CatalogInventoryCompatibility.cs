using Ashenfall.Api.Catalog.Domain;

namespace Ashenfall.Api.Characters;

/// <summary>Fail-closed candidate check for every owned item, including unequipped inventory.</summary>
public static class CatalogInventoryCompatibility
{
    public static void Validate(CatalogSnapshot candidate, IReadOnlyCollection<CharacterRow> characters,
        IReadOnlyCollection<OwnedItemRow> owned, IReadOnlyCollection<EquipmentRow> equipment,
        IReadOnlyCollection<ItemDefinition> historicalItems,
        IReadOnlyCollection<(Guid SourceId, Guid TargetId)> itemLinks)
    {
        if (candidate.Release.Status is not (CatalogStates.Draft or CatalogStates.Published))
            throw new ArgumentException("Candidate release must be draft or published.");
        if (characters.Select(x => x.Id).Distinct().Count() != characters.Count || owned.Select(x => x.Id).Distinct().Count() != owned.Count ||
            itemLinks.GroupBy(x => x.SourceId).Any(x => x.Count() != 1) || equipment.GroupBy(x => x.OwnedItemId).Any(x => x.Count() != 1) ||
            equipment.GroupBy(x => (x.CharacterId, x.SlotCode)).Any(x => x.Count() != 1))
            throw new ArgumentException("Duplicate persisted or mapping identity.");
        var actors = characters.ToDictionary(x => x.Id);
        var originals = historicalItems.ToDictionary(x => x.Id);
        var targets = candidate.Items.ToDictionary(x => x.Id);
        var mappings = itemLinks.ToDictionary(x => x.SourceId, x => x.TargetId);
        var resolved = new Dictionary<Guid, ItemDefinition>();
        foreach (var item in owned)
        {
            if (!actors.ContainsKey(item.CharacterId) || item.Quantity < 1 || item.RowRevision < 1)
                throw new ArgumentException("Invalid owner or item state.");
            if (!originals.TryGetValue(item.ItemDefinitionId, out var original))
                throw new ArgumentException("Missing historical item definition.");
            var targetId = original.CatalogReleaseId == candidate.Release.Id ? original.Id :
                mappings.TryGetValue(original.Id, out var mapped) ? mapped : throw new ArgumentException("Missing item successor.");
            if (!targets.TryGetValue(targetId, out var target) || target.CatalogReleaseId != candidate.Release.Id ||
                target.Code != original.Code || target.Category != original.Category || target.EquipSlot != original.EquipSlot ||
                target.HandRequirement != original.HandRequirement || target.Stackable != original.Stackable ||
                target.MaxStack != original.MaxStack || item.Quantity > target.MaxStack || (!target.Stackable && item.Quantity != 1))
                throw new ArgumentException("Incompatible candidate item or quantity.");
            resolved.Add(item.Id, target);
        }
        foreach (var entry in equipment)
        {
            var ownedItem = owned.SingleOrDefault(x => x.Id == entry.OwnedItemId);
            if (ownedItem is null || ownedItem.CharacterId != entry.CharacterId || !resolved.TryGetValue(entry.OwnedItemId, out var target))
                throw new ArgumentException("Equipment is not owned by this character.");
            bool twoHanded = equipment.Where(x => x.CharacterId == entry.CharacterId && x.SlotCode == "main_hand")
                .Any(x => resolved.TryGetValue(x.OwnedItemId, out var main) && main.HandRequirement == 2);
            bool offHand = equipment.Any(x => x.CharacterId == entry.CharacterId && x.SlotCode == "off_hand");
            CharacterRules.ValidateEquipment(entry.SlotCode, target.Category, target.EquipSlot, target.HandRequirement,
                target.Stackable, ownedItem.Quantity, twoHanded, offHand);
        }
    }
}
