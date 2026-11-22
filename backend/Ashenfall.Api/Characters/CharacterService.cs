using Ashenfall.Api.Catalog.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ashenfall.Api.Characters;

/// <summary>Trusted account identity comes from the server boundary, never a request DTO.</summary>
public sealed class CharacterService(CharacterDbContext db, CatalogDbContext catalog)
{
    public Task<AccountRow?> FindAccountAsync(string provider, string subject, CancellationToken ct) =>
        db.Accounts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.IdentityProvider == provider && x.ProviderSubject == subject, ct);

    public async Task<AccountRow> GetOrCreateAccountAsync(string provider, string subject, CancellationToken ct)
    {
        var existing = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.IdentityProvider == provider && x.ProviderSubject == subject, ct);
        if (existing is not null) return existing;
        var account = new AccountRow { Id = Guid.NewGuid(), IdentityProvider = provider,
            ProviderSubject = subject, CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow };
        db.Accounts.Add(account);
        try { await db.SaveChangesAsync(ct); return account; }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        {
            db.Entry(account).State = EntityState.Detached;
            return await db.Accounts.AsNoTracking().SingleAsync(x =>
                x.IdentityProvider == provider && x.ProviderSubject == subject, ct);
        }
    }

    public Task<CharacterRow?> GetActiveAsync(Guid accountId, CancellationToken ct) =>
        db.Characters.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == accountId && x.DeletedAt == null, ct);

    public async Task<CharacterRow> CreateAsync(Guid accountId, Guid classId, string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Character name is required.");
        name = name.Trim();
        if (name.Length is < 3 or > 32 || name.Any(char.IsControl))
            throw new ArgumentException("Character name must contain 3 to 32 visible characters.");
        if (!await catalog.Classes.AnyAsync(x => x.Id == classId &&
            catalog.Releases.Any(r => r.Id == x.CatalogReleaseId && r.Status == "published"), ct))
            throw new ArgumentException("The class must belong to a published catalog release.");
        var character = new CharacterRow { Id = Guid.NewGuid(), AccountId = accountId,
            ClassDefinitionId = classId, CharacterName = name, CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow };
        db.Characters.Add(character);
        try { await db.SaveChangesAsync(ct); return character; }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        {
            db.Entry(character).State = EntityState.Detached;
            throw new InvalidOperationException("This account already has an active character.");
        }
    }

    public async Task<CharacterRow> SaveAsync(Guid accountId, long expectedRevision,
        long experience, int strength, int dexterity, int vitality, CancellationToken ct)
    {
        if (experience < 0 || strength < 0 || dexterity < 0 || vitality < 0 || expectedRevision < 1)
            throw new ArgumentException("Persistent character values must be non-negative.");
        var current = await GetActiveAsync(accountId, ct);
        if (current is null || current.RowRevision != expectedRevision)
            throw new DbUpdateConcurrencyException("Character revision is stale or character is absent.");
        if (experience < current.Experience)
            throw new ArgumentException("Experience cannot decrease.");
        var rules = await db.LevelRules.AsNoTracking()
            .Where(x => x.ClassDefinitionId == current.ClassDefinitionId)
            .OrderBy(x => x.Level).ToListAsync(ct);
        if (rules.Count == 0) rules.Add(new LevelRuleRow { ClassDefinitionId = current.ClassDefinitionId,
            Level = 1, RequiredTotalXp = 0 });
        CharacterRules.ValidateAllocation(experience, strength, dexterity, vitality, rules);
        int changed = await db.Characters.Where(x => x.AccountId == accountId &&
            x.DeletedAt == null && x.RowRevision == expectedRevision).ExecuteUpdateAsync(set => set
            .SetProperty(x => x.Experience, experience)
            .SetProperty(x => x.AllocatedStrength, strength)
            .SetProperty(x => x.AllocatedDexterity, dexterity)
            .SetProperty(x => x.AllocatedVitality, vitality)
            .SetProperty(x => x.RowRevision, expectedRevision + 1)
            .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);
        if (changed != 1) throw new DbUpdateConcurrencyException("Character revision is stale or character is absent.");
        return (await GetActiveAsync(accountId, ct))!;
    }

    public async Task SoftDeleteAsync(Guid accountId, long expectedRevision, CancellationToken ct)
    {
        int changed = await db.Characters.Where(x => x.AccountId == accountId &&
            x.DeletedAt == null && x.RowRevision == expectedRevision).ExecuteUpdateAsync(set => set
            .SetProperty(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .SetProperty(x => x.RowRevision, expectedRevision + 1)
            .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);
        if (changed != 1) throw new DbUpdateConcurrencyException("Character revision is stale or character is absent.");
    }

    public async Task<IReadOnlyList<OwnedItemRow>> InventoryAsync(Guid accountId, CancellationToken ct)
    {
        var character = await GetActiveAsync(accountId, ct);
        return character is null ? [] : await db.OwnedItems.AsNoTracking()
            .Where(x => x.CharacterId == character.Id).ToListAsync(ct);
    }

    public async Task<EffectiveCharacterStats?> EffectiveStatsAsync(Guid accountId, CancellationToken ct)
    {
        var character = await GetActiveAsync(accountId, ct);
        if (character is null) return null;
        var definition = await catalog.Classes.AsNoTracking().SingleAsync(x => x.Id == character.ClassDefinitionId, ct);
        var rules = await db.LevelRules.AsNoTracking().Where(x => x.ClassDefinitionId == definition.Id)
            .OrderBy(x => x.Level).ToListAsync(ct);
        // Legacy prototype-1 has no authored progression. It remains level 1 until a
        // reviewed future catalog release supplies rules; no published row is edited.
        if (rules.Count == 0) rules.Add(new LevelRuleRow { ClassDefinitionId = definition.Id,
            Level = 1, RequiredTotalXp = 0 });
        var itemIds = await (from gear in db.Equipment
            join owned in db.OwnedItems on gear.OwnedItemId equals owned.Id
            where gear.CharacterId == character.Id select owned.ItemDefinitionId).ToListAsync(ct);
        var modifiers = await db.ItemModifiers.AsNoTracking()
            .Where(x => itemIds.Contains(x.ItemDefinitionId)).ToListAsync(ct);
        return CharacterRules.CalculateStats(definition.BaseMaxHealth, definition.FixedDamage,
            definition.BaseStrength ?? 0, definition.BaseDexterity ?? 0,
            definition.BaseVitality ?? 0, definition.BaseArmor ?? 0,
            character, rules, modifiers);
    }

    /// <summary>Trusted server-side grant only; never expose as a client inventory endpoint.</summary>
    public async Task<OwnedItemRow> GrantItemAsync(Guid characterId, Guid definitionId, int quantity, CancellationToken ct)
    {
        if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
        var definition = await catalog.Items.SingleOrDefaultAsync(x => x.Id == definitionId &&
            catalog.Releases.Any(r => r.Id == x.CatalogReleaseId && r.Status == "published"), ct)
            ?? throw new ArgumentException("Unknown item definition.");
        if (quantity > definition.MaxStack || (!definition.Stackable && quantity != 1))
            throw new ArgumentException("Quantity exceeds the authored stack limit.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var character = await LockCharacterByIdAsync(characterId, ct);
        var item = new OwnedItemRow { Id = Guid.NewGuid(), CharacterId = characterId,
            ItemDefinitionId = definitionId, Quantity = quantity, CreatedAt = DateTimeOffset.UtcNow };
        db.OwnedItems.Add(item);
        character.RowRevision++;
        character.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return item;
    }

    /// <summary>Trusted inventory mutation with optimistic item revision.</summary>
    public async Task<OwnedItemRow> SetQuantityAsync(Guid characterId, Guid itemId,
        long expectedRevision, int quantity, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var character = await LockCharacterByIdAsync(characterId, ct);
        var item = await db.OwnedItems.SingleOrDefaultAsync(x =>
            x.Id == itemId && x.CharacterId == characterId, ct)
            ?? throw new ArgumentException("Owned item not found.");
        if (item.RowRevision != expectedRevision)
            throw new DbUpdateConcurrencyException("Owned item revision is stale.");
        var definition = await catalog.Items.AsNoTracking().SingleAsync(x => x.Id == item.ItemDefinitionId, ct);
        if (quantity < 1 || quantity > definition.MaxStack || (!definition.Stackable && quantity != 1))
            throw new ArgumentException("Invalid quantity for the item definition.");
        if (await db.Equipment.AnyAsync(x => x.OwnedItemId == itemId, ct))
            throw new ArgumentException("Unequip the item before changing its quantity.");
        item.Quantity = quantity;
        item.RowRevision++;
        character.RowRevision++;
        character.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return item;
    }

    /// <summary>Remove an unequipped owned item; a stale item revision cannot remove it.</summary>
    public async Task DeleteItemAsync(Guid characterId, Guid itemId,
        long expectedRevision, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var character = await LockCharacterByIdAsync(characterId, ct);
        var item = await db.OwnedItems.SingleOrDefaultAsync(x =>
            x.Id == itemId && x.CharacterId == characterId, ct)
            ?? throw new ArgumentException("Owned item not found.");
        if (item.RowRevision != expectedRevision)
            throw new DbUpdateConcurrencyException("Owned item revision is stale.");
        if (await db.Equipment.AnyAsync(x => x.OwnedItemId == itemId, ct))
            throw new ArgumentException("Unequip the item before deleting it.");
        db.OwnedItems.Remove(item);
        character.RowRevision++;
        character.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<EquipmentRow>> EquipmentAsync(Guid accountId, CancellationToken ct)
    {
        var character = await GetActiveAsync(accountId, ct);
        return character is null ? [] : await db.Equipment.AsNoTracking()
            .Where(x => x.CharacterId == character.Id).ToListAsync(ct);
    }

    public async Task EquipAsync(Guid accountId, Guid ownedId, string slot, long expectedRevision, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var character = await LockedCharacterAsync(accountId, expectedRevision, ct);
        var item = await db.OwnedItems.SingleOrDefaultAsync(x => x.Id == ownedId && x.CharacterId == character.Id, ct)
            ?? throw new ArgumentException("Item is not owned by this character.");
        var definition = await catalog.Items.AsNoTracking().SingleAsync(x => x.Id == item.ItemDefinitionId, ct);
        var current = await db.Equipment.Where(x => x.CharacterId == character.Id).ToListAsync(ct);
        bool twoHanded = false;
        var main = current.FirstOrDefault(x => x.SlotCode == "main_hand");
        if (main is not null && main.OwnedItemId != ownedId)
        {
            var mainItem = await db.OwnedItems.AsNoTracking().SingleAsync(x => x.Id == main.OwnedItemId, ct);
            twoHanded = await catalog.Items.AnyAsync(x => x.Id == mainItem.ItemDefinitionId && x.HandRequirement == 2, ct);
        }
        CharacterRules.ValidateEquipment(slot, definition.Category, definition.EquipSlot,
            definition.HandRequirement, definition.Stackable, item.Quantity, twoHanded,
            current.Any(x => x.SlotCode == "off_hand" && x.OwnedItemId != ownedId));
        if (current.Any(x => x.OwnedItemId == ownedId && x.SlotCode != slot))
            throw new ArgumentException("The owned item is already equipped in another slot.");
        var previous = current.FirstOrDefault(x => x.SlotCode == slot);
        if (previous is not null)
        {
            previous.OwnedItemId = ownedId;
            previous.EquippedAt = DateTimeOffset.UtcNow;
        }
        else db.Equipment.Add(new EquipmentRow { CharacterId = character.Id, SlotCode = slot,
            OwnedItemId = ownedId, EquippedAt = DateTimeOffset.UtcNow });
        character.RowRevision++;
        character.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task UnequipAsync(Guid accountId, string slot, long expectedRevision, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var character = await LockedCharacterAsync(accountId, expectedRevision, ct);
        var equipped = await db.Equipment.SingleOrDefaultAsync(x => x.CharacterId == character.Id && x.SlotCode == slot, ct)
            ?? throw new ArgumentException("The slot is empty.");
        db.Equipment.Remove(equipped);
        character.RowRevision++;
        character.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<CharacterRow> LockedCharacterAsync(Guid accountId, long expectedRevision, CancellationToken ct)
    {
        var character = await db.Characters.FromSqlInterpolated(
            $"SELECT * FROM player_characters WHERE account_id = {accountId} AND deleted_at IS NULL FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (character is null || character.RowRevision != expectedRevision)
            throw new DbUpdateConcurrencyException("Character revision is stale or character is absent.");
        return character;
    }

    private async Task<CharacterRow> LockCharacterByIdAsync(Guid characterId, CancellationToken ct)
    {
        var character = await db.Characters.FromSqlInterpolated(
            $"SELECT * FROM player_characters WHERE id = {characterId} AND deleted_at IS NULL FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        return character ?? throw new ArgumentException("Character is absent or deleted.");
    }
}
