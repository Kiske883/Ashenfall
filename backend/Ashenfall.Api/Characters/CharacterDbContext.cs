using Microsoft.EntityFrameworkCore;

namespace Ashenfall.Api.Characters;

public sealed class AccountRow
{
    public Guid Id { get; set; }
    public string IdentityProvider { get; set; } = "";
    public string ProviderSubject { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class CharacterRow
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid ClassDefinitionId { get; set; }
    public string CharacterName { get; set; } = "";
    public long Experience { get; set; }
    public int AllocatedStrength { get; set; }
    public int AllocatedDexterity { get; set; }
    public int AllocatedVitality { get; set; }
    public long RowRevision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
public sealed class OwnedItemRow
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public Guid ItemDefinitionId { get; set; }
    public int Quantity { get; set; }
    public long RowRevision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class EquipmentRow
{
    public Guid CharacterId { get; set; }
    public string SlotCode { get; set; } = "";
    public Guid OwnedItemId { get; set; }
    public DateTimeOffset EquippedAt { get; set; }
}
public sealed class LevelRuleRow
{
    public Guid ClassDefinitionId { get; set; }
    public int Level { get; set; }
    public long RequiredTotalXp { get; set; }
    public int HealthBonus { get; set; }
    public int DamageBonus { get; set; }
    public int AttributePoints { get; set; }
}
public sealed class ItemModifierRow
{
    public Guid ItemDefinitionId { get; set; }
    public string StatCode { get; set; } = "";
    public string ModifierKind { get; set; } = "";
    public decimal Amount { get; set; }
}

public sealed class CharacterDbContext(DbContextOptions<CharacterDbContext> options) : DbContext(options)
{
    public DbSet<AccountRow> Accounts => Set<AccountRow>();
    public DbSet<CharacterRow> Characters => Set<CharacterRow>();
    public DbSet<OwnedItemRow> OwnedItems => Set<OwnedItemRow>();
    public DbSet<EquipmentRow> Equipment => Set<EquipmentRow>();
    public DbSet<LevelRuleRow> LevelRules => Set<LevelRuleRow>();
    public DbSet<ItemModifierRow> ItemModifiers => Set<ItemModifierRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AccountRow>(e => { e.ToTable("accounts"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.IdentityProvider).HasColumnName("identity_provider");
            e.Property(x => x.ProviderSubject).HasColumnName("provider_subject");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at"); });
        model.Entity<CharacterRow>(e => { e.ToTable("player_characters"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.ClassDefinitionId).HasColumnName("class_definition_id");
            e.Property(x => x.CharacterName).HasColumnName("character_name");
            e.Property(x => x.Experience).HasColumnName("experience");
            e.Property(x => x.AllocatedStrength).HasColumnName("allocated_strength");
            e.Property(x => x.AllocatedDexterity).HasColumnName("allocated_dexterity");
            e.Property(x => x.AllocatedVitality).HasColumnName("allocated_vitality");
            e.Property(x => x.RowRevision).HasColumnName("row_revision").IsConcurrencyToken();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at"); });
        model.Entity<OwnedItemRow>(e => { e.ToTable("owned_items"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CharacterId).HasColumnName("character_id");
            e.Property(x => x.ItemDefinitionId).HasColumnName("item_definition_id");
            e.Property(x => x.Quantity).HasColumnName("quantity");
            e.Property(x => x.RowRevision).HasColumnName("row_revision").IsConcurrencyToken();
            e.Property(x => x.CreatedAt).HasColumnName("created_at"); });
        model.Entity<EquipmentRow>(e => { e.ToTable("character_equipment");
            e.HasKey(x => new { x.CharacterId, x.SlotCode });
            e.HasIndex(x => x.OwnedItemId).IsUnique();
            e.Property(x => x.CharacterId).HasColumnName("character_id");
            e.Property(x => x.SlotCode).HasColumnName("slot_code");
            e.Property(x => x.OwnedItemId).HasColumnName("owned_item_id");
            e.Property(x => x.EquippedAt).HasColumnName("equipped_at"); });
        model.Entity<LevelRuleRow>(e => { e.ToTable("class_level_rules");
            e.HasKey(x => new { x.ClassDefinitionId, x.Level });
            e.Property(x => x.ClassDefinitionId).HasColumnName("class_definition_id");
            e.Property(x => x.Level).HasColumnName("level");
            e.Property(x => x.RequiredTotalXp).HasColumnName("required_total_xp");
            e.Property(x => x.HealthBonus).HasColumnName("health_bonus");
            e.Property(x => x.DamageBonus).HasColumnName("damage_bonus");
            e.Property(x => x.AttributePoints).HasColumnName("attribute_points"); });
        model.Entity<ItemModifierRow>(e => { e.ToTable("item_stat_modifiers");
            e.HasKey(x => new { x.ItemDefinitionId, x.StatCode, x.ModifierKind });
            e.Property(x => x.ItemDefinitionId).HasColumnName("item_definition_id");
            e.Property(x => x.StatCode).HasColumnName("stat_code");
            e.Property(x => x.ModifierKind).HasColumnName("modifier_kind");
            e.Property(x => x.Amount).HasColumnName("amount"); });
    }
}
