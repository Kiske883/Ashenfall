using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Api.Characters;
using Xunit;

namespace Ashenfall.Tests;

public sealed class CatalogInventoryCompatibilityTests
{
    [Fact]
    public void ValidatesUnequippedAndEquippedInventoryWithoutMutation()
    {
        var catalog = InitialCatalogSeed.CreateDraft();
        var target = catalog.Items.Single(x => x.Code == "guardian_sword");
        var original = new ItemDefinition { Id = Guid.NewGuid(), CatalogReleaseId = Guid.NewGuid(), Code = target.Code,
            Category = target.Category, EquipSlot = target.EquipSlot, HandRequirement = target.HandRequirement,
            Stackable = target.Stackable, MaxStack = target.MaxStack };
        var actor = new CharacterRow { Id = Guid.NewGuid(), RowRevision = 1 };
        var item = new OwnedItemRow { Id = Guid.NewGuid(), CharacterId = actor.Id, ItemDefinitionId = original.Id, Quantity = 1, RowRevision = 1 };
        var link = new[] { (SourceId: original.Id, TargetId: target.Id) };
        CatalogInventoryCompatibility.Validate(catalog, [actor], [item], [], [original], link);
        var equipped = new EquipmentRow { CharacterId = actor.Id, OwnedItemId = item.Id, SlotCode = "main_hand" };
        CatalogInventoryCompatibility.Validate(catalog, [actor], [item], [equipped], [original], link);
        Assert.Throws<ArgumentException>(() => CatalogInventoryCompatibility.Validate(catalog, [actor], [item], [], [original], []));
        Assert.Throws<ArgumentException>(() => CatalogInventoryCompatibility.Validate(catalog, [actor], [item], [], [original], [link[0], link[0]]));
        target.MaxStack = 2;
        Assert.Throws<ArgumentException>(() => CatalogInventoryCompatibility.Validate(catalog, [actor], [item], [], [original], link));
        target.MaxStack = 1; item.Quantity = 2;
        Assert.Throws<ArgumentException>(() => CatalogInventoryCompatibility.Validate(catalog, [actor], [item], [], [original], link));
        item.Quantity = 1; equipped.SlotCode = "off_hand";
        Assert.Throws<ArgumentException>(() => CatalogInventoryCompatibility.Validate(catalog, [actor], [item], [equipped], [original], link));
        Assert.Equal(1, actor.RowRevision); Assert.Equal(original.Id, item.ItemDefinitionId);
    }
}
