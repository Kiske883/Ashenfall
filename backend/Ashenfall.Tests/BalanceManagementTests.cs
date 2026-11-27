using System.Text.Json;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Api.Characters;
using Ashenfall.CatalogPublisher.Balance;
using Xunit;

namespace Ashenfall.Tests;

public sealed class BalanceManagementTests
{
    private const string Chance = "class/dwarf_warrior/critical_chance_percent";
    internal static BalanceState Fixture(bool equipped = false)
    {
        var (catalog, rules, mods) = SnapshotFixtureManifest.Create();
        catalog.Release.Status = CatalogStates.Published; catalog.Release.PublishedAt = DateTimeOffset.UnixEpoch;
        catalog.Release.ContentSha256 = CatalogHash.Compute(catalog);
        var data = new BalanceCatalog { Catalog = catalog,
            Combat = catalog.Classes.Select(x => new CatalogCombatClassV2(x.Id,25,25,5,100)).ToList(),
            Levels = rules.Select(x => new CatalogProgressionV2(x.ClassDefinitionId,x.Level,x.RequiredTotalXp,x.HealthBonus,x.DamageBonus,x.AttributePoints,0,0,0,0)).ToList(),
            Modifiers = mods.Select(x => new CatalogModifierV2(x.ItemDefinitionId,x.StatCode,x.ModifierKind,x.Amount)).ToList() };
        data.Hash=data.ComputeHash();
        var player = new CharacterRow { Id=Guid.NewGuid(),AccountId=Guid.NewGuid(),ClassDefinitionId=catalog.Classes.Single().Id,Experience=100,RowRevision=1 };
        var state = new BalanceState { Catalogs=[data],Characters=[player] };
        if(equipped)
        {
            var item=new OwnedItemRow { Id=Guid.NewGuid(),CharacterId=player.Id,ItemDefinitionId=catalog.Items.Single(x=>x.Code=="guardian_sword").Id,Quantity=1 };
            state.OwnedItems.Add(item); state.Equipment.Add(new EquipmentRow { CharacterId=player.Id,OwnedItemId=item.Id,SlotCode="main_hand" });
        }
        return state;
    }
    private static BalancePlan Plan(BalanceState s,params BalanceEdit[] changes) => BalancePlanner.Plan(s,Guid.NewGuid(),"balance-test",changes,characterId:s.Characters[0].Id);
    private static string Persistence(BalanceState s)=>JsonSerializer.Serialize(new { s.Characters,s.OwnedItems,s.Equipment });
    internal static void ActivateInMemory(BalanceState state,BalanceCatalog candidate)
    {
        state.Active.Catalog.Release.Status=CatalogStates.Retired;
        candidate.Catalog.Release.Status=CatalogStates.Published; candidate.Catalog.Release.PublishedAt=DateTimeOffset.UtcNow;
        state.Catalogs.Add(candidate);
    }

    [Fact] public void DiscoverReportsTypedUnitsOwnersAndOnlyExistingParameters()
    {
        var parameters=BalanceParameters.Discover(Fixture().Active);
        var chance=Assert.Single(parameters,x=>x.Key==Chance);
        Assert.Equal(5,chance.Value); Assert.Contains("5%",chance.Unit); Assert.Equal(100,chance.Maximum);
        Assert.Contains(parameters,x=>x.Key=="modifier/guardian_sword/max_health/flat/amount");
        Assert.DoesNotContain(parameters,x=>x.Category is "spell" or "gem" || x.Key.Contains("password"));
        Assert.All(parameters,x=>Assert.NotEqual(Guid.Empty,x.OwnerId));
    }
    [Theory] [InlineData(0)] [InlineData(5)] [InlineData(8)] [InlineData(100)] [InlineData(0.05)]
    public void PercentagesArePercentagePointsNotFractions(decimal value)
    {
        var state=Fixture();var plan=Plan(state,new BalanceEdit(Chance,value));
        Assert.Equal(value,plan.ExpectedSnapshot!.EffectiveStats.CriticalChancePercent);
    }
    [Theory] [InlineData(-1)] [InlineData(100.0001)] [InlineData(0.00001)]
    public void InvalidPercentageOrPrecisionIsRejected(decimal value)=>Assert.Throws<ArgumentException>(()=>Plan(Fixture(),new BalanceEdit(Chance,value)));
    [Fact] public void PlanningIsReadOnlyAndCanonicalHashMatchesExistingImplementation()
    {
        var state=Fixture();string before=JsonSerializer.Serialize(state);
        var plan=Plan(state,new BalanceEdit(Chance,100)); var candidate=BalancePlanner.Materialize(state,plan);
        Assert.Equal(before,JsonSerializer.Serialize(state)); Assert.Single(plan.Differences);
        Assert.Equal(plan.TargetHash,candidate.ComputeHash()); Assert.Equal(CatalogHash.Compute(candidate.Catalog),candidate.Catalog.Release.ContentSha256);
        Assert.Equal(105,plan.ExpectedSnapshot!.EffectiveStats.MaxHealth); Assert.Equal(27,plan.ExpectedSnapshot.EffectiveStats.MinDamage);
        Assert.Equal(100,plan.ExpectedSnapshot.EffectiveStats.CriticalDamageBonusPercent);
    }
    [Fact] public void MultipleParametersUseExistingEffectiveCalculation()
    {
        var s=Fixture(true);var p=Plan(s,new BalanceEdit(Chance,15),new BalanceEdit("class/dwarf_warrior/critical_damage_bonus_percent",150),
            new BalanceEdit("level/dwarf_warrior/2/damage_bonus",3));
        Assert.Equal(3,p.Differences.Count); Assert.Equal(121,p.ExpectedSnapshot!.EffectiveStats.MaxHealth);
        Assert.Equal(31,p.ExpectedSnapshot.EffectiveStats.MinDamage); Assert.Equal(150,p.ExpectedSnapshot.EffectiveStats.CriticalDamageBonusPercent);
    }
    [Fact] public void ExistingItemModifierCanBeChangedWithoutChangingInventory()
    {
        var s=Fixture(true);var before=Persistence(s);
        var p=Plan(s,new BalanceEdit("modifier/guardian_sword/fixed_damage/flat/amount",4));
        Assert.Equal(31,p.ExpectedSnapshot!.EffectiveStats.MinDamage); Assert.Equal(before,Persistence(s));
    }
    [Fact] public void UnsupportedDuplicateAndInvalidRangeChangesFailClosed()
    {
        Assert.Throws<ArgumentException>(()=>Plan(Fixture(),new BalanceEdit("class/dwarf_warrior/sql",1)));
        Assert.Throws<ArgumentException>(()=>Plan(Fixture(),new BalanceEdit(Chance,8),new BalanceEdit(Chance,9)));
        Assert.Throws<ArgumentException>(()=>Plan(Fixture(),new BalanceEdit("class/dwarf_warrior/min_damage",30)));
        Assert.Throws<ArgumentException>(()=>Plan(Fixture(),new BalanceEdit("class/dwarf_warrior/base_max_health",0)));
    }
    [Fact] public void NoOpDoesNotRequireNewGameplayConfiguration()
    {
        var state=Fixture();var p=Plan(state,new BalanceEdit(Chance,5)); Assert.True(p.NoOp); Assert.Empty(p.Differences);
        Assert.Equal(5,state.Active.Combat[0].CriticalChancePercent);
        Assert.Equal(state.Active.Hash,p.TargetHash);Assert.Equal(state.Active.Catalog.Release.Id,p.ExpectedSnapshot!.CatalogReleaseId);
    }
    [Fact] public void StaleOrTamperedPlansAreRejected()
    {
        var s=Fixture();var p=Plan(s,new BalanceEdit(Chance,8));
        Assert.Throws<InvalidOperationException>(()=>BalancePlanner.Materialize(s,p with { ExpectedActiveHash=new string('0',64) }));
        Assert.Throws<InvalidOperationException>(()=>BalancePlanner.Materialize(s,p with { TargetHash=new string('0',64) }));
        Assert.Throws<InvalidOperationException>(()=>BalancePlanner.Materialize(s,p with { FormatVersion=99 }));
    }
    [Fact] public void PlanRoundTripHasStableDraftIdsHashAndMappings()
    {
        var s=Fixture();var p=Plan(s,new BalanceEdit(Chance,100));
        var restored=JsonSerializer.Deserialize<BalancePlan>(JsonSerializer.Serialize(p,BalanceJson.Options),BalanceJson.Options)!;
        var a=BalancePlanner.Materialize(s,p);var b=BalancePlanner.Materialize(s,restored);
        Assert.Equal(a.Hash,b.Hash);Assert.Equal(p.OperationId,a.Catalog.Release.Id);
        Assert.Equal(a.Catalog.Classes[0].Id,b.Catalog.Classes[0].Id);
        Assert.Contains(a.ClassLinks,x=>x.SourceId==s.Characters[0].ClassDefinitionId);
    }
    [Fact] public void ChangedSelectedCharacterStateInvalidatesReviewedExpectedStatistics()
    {
        var s=Fixture();var p=Plan(s,new BalanceEdit(Chance,8));
        s.Characters[0].RowRevision++;
        Assert.Throws<InvalidOperationException>(()=>BalancePlanner.Materialize(s,p));
    }
    [Fact] public void ForwardRestorationAndRepeatedTransitionsKeepAllHistoricalReferences()
    {
        var s=Fixture(true);var original=s.Active;var fingerprint=Persistence(s);var originalHash=original.Hash;
        var p=Plan(s,new BalanceEdit(Chance,100));var first=BalancePlanner.Materialize(s,p);ActivateInMemory(s,first);
        Assert.Equal(100,BalancePlanner.Snapshot(s,s.Active,s.Characters[0].Id).EffectiveStats.CriticalChancePercent);
        var restore=BalancePlanner.Plan(s,Guid.NewGuid(),"restored",[],original.Catalog.Release.Id,s.Characters[0].Id);
        var second=BalancePlanner.Materialize(s,restore);ActivateInMemory(s,second);
        Assert.Equal(5,BalancePlanner.Snapshot(s,s.Active,s.Characters[0].Id).EffectiveStats.CriticalChancePercent);
        Assert.Equal(121,BalancePlanner.Snapshot(s,s.Active,s.Characters[0].Id).EffectiveStats.MaxHealth);
        Assert.Equal(CatalogStates.Retired,original.Catalog.Release.Status);Assert.Equal(originalHash,original.Hash);
        Assert.NotEqual(original.Catalog.Release.Id,second.Catalog.Release.Id);Assert.Equal(fingerprint,Persistence(s));
        Assert.Single(s.Catalogs,x=>x.Catalog.Release.Status==CatalogStates.Published);
        Assert.Contains(second.ClassLinks,x=>x.SourceId==first.Catalog.Classes[0].Id);
        Assert.Contains(second.ItemLinks,x=>x.SourceId==s.OwnedItems[0].ItemDefinitionId);
        Assert.Throws<InvalidOperationException>(()=>BalancePlanner.Materialize(s,p));
    }
    [Fact] public void MissingHistoricalClassOrItemRelationshipRejectsPlanning()
    {
        var s=Fixture(true);var p=Plan(s,new BalanceEdit(Chance,8));var candidate=BalancePlanner.Materialize(s,p);
        candidate.ClassLinks.Clear(); candidate.Hash=candidate.ComputeHash();
        Assert.ThrowsAny<InvalidOperationException>(()=>BalancePlanner.Validate(s,candidate));
        candidate=BalancePlanner.Materialize(s,p); candidate.ItemLinks.Clear();candidate.Hash=candidate.ComputeHash();
        Assert.Throws<ArgumentException>(()=>BalancePlanner.Validate(s,candidate));
    }
    [Fact] public void IncompatibleUnequippedQuantityAndEquipmentFailValidation()
    {
        var s=Fixture(true);var p=Plan(s,new BalanceEdit(Chance,8));
        var candidate=BalancePlanner.Materialize(s,p);s.OwnedItems[0].Quantity=2;
        Assert.Throws<ArgumentException>(()=>BalancePlanner.Validate(s,candidate));
        s.OwnedItems[0].Quantity=1;s.Equipment[0].SlotCode="head";
        Assert.Throws<ArgumentException>(()=>BalancePlanner.Validate(s,candidate));
    }
    [Fact] public void MissingAndAmbiguousMappingsOrBadManifestAreRejected()
    {
        var s=Fixture();var p=Plan(s,new BalanceEdit(Chance,8));var c=BalancePlanner.Materialize(s,p);
        c.Hash=new string('0',64);Assert.Throws<InvalidOperationException>(()=>BalancePlanner.Validate(s,c));
        c=BalancePlanner.Materialize(s,p);c.ClassLinks.Add(c.ClassLinks[0]);
        Assert.Throws<ArgumentException>(()=>c.ComputeHash());
    }
    [Fact] public async Task DatabaseTargetAndExecutionIntentFailBeforeAnyConnection()
    {
        Assert.Throws<InvalidOperationException>(()=>new BalanceManagementService("Host=localhost;Database=ashenfall_dev;Username=postgres"));
        Assert.Throws<InvalidOperationException>(()=>new BalanceManagementService("Host=remote;Database=ashenfall_09b3_test;Username=postgres"));
        var p=Plan(Fixture(),new BalanceEdit(Chance,8));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new BalanceManagementService().ApplyAsync(p,false));
    }
}
