using Ashenfall.Api.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ashenfall.Api.Catalog.Persistence;

public sealed class CatalogReleaseConfiguration : IEntityTypeConfiguration<CatalogRelease>
{
    public void Configure(EntityTypeBuilder<CatalogRelease> e)
    {
        e.ToTable("catalog_releases", t =>
        {
            t.HasCheckConstraint("ck_catalog_version_length", "length(version_key) BETWEEN 1 AND 64");
            t.HasCheckConstraint("ck_catalog_schema_major", "schema_major >= 1");
            t.HasCheckConstraint("ck_catalog_hash", "content_sha256 IS NULL OR content_sha256 ~ '^[0-9a-f]{64}$'");
            t.HasCheckConstraint("ck_catalog_status", "status IN ('draft', 'published', 'retired')");
            t.HasCheckConstraint("ck_catalog_publication", "(status = 'draft' AND published_at IS NULL) OR (status IN ('published', 'retired') AND published_at IS NOT NULL AND content_sha256 IS NOT NULL)");
        });
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.VersionKey).HasColumnName("version_key").IsRequired();
        e.Property(x => x.SchemaMajor).HasColumnName("schema_major");
        e.Property(x => x.ContentSha256).HasColumnName("content_sha256").HasColumnType("char(64)");
        e.Property(x => x.Status).HasColumnName("status").IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        e.Property(x => x.PublishedAt).HasColumnName("published_at").HasColumnType("timestamptz");
        e.HasIndex(x => x.VersionKey).IsUnique();
        e.HasIndex(x => x.Status).IsUnique().HasFilter("status = 'published'").HasDatabaseName("catalog_one_published");
    }
}

public sealed class CharacterClassConfiguration : IEntityTypeConfiguration<CharacterClass>
{
    public void Configure(EntityTypeBuilder<CharacterClass> e)
    {
        e.ToTable("character_classes", t =>
        {
            t.HasCheckConstraint("ck_class_code", "code ~ '^[a-z][a-z0-9_]{1,63}$'");
            t.HasCheckConstraint("ck_class_name", "length(display_name) BETWEEN 1 AND 100");
            t.HasCheckConstraint("ck_class_visual", "length(visual_key) BETWEEN 1 AND 100");
            t.HasCheckConstraint("ck_class_health", "base_max_health > 0");
            t.HasCheckConstraint("ck_class_damage", "fixed_damage >= 0");
            t.HasCheckConstraint("ck_class_attributes", "(base_strength IS NULL OR base_strength >= 0) AND (base_dexterity IS NULL OR base_dexterity >= 0) AND (base_vitality IS NULL OR base_vitality >= 0) AND (base_armor IS NULL OR base_armor >= 0)");
            t.HasCheckConstraint("ck_class_speed_range", "move_speed_mps > 0 AND melee_range_m > 0");
            t.HasCheckConstraint("ck_class_timing", "attack_cycle_ms >= 100 AND impact_ms BETWEEN 0 AND attack_cycle_ms");
        });
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.CatalogReleaseId).HasColumnName("catalog_release_id");
        e.Property(x => x.Code).HasColumnName("code").IsRequired();
        e.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
        e.Property(x => x.VisualKey).HasColumnName("visual_key").IsRequired();
        e.Property(x => x.BaseMaxHealth).HasColumnName("base_max_health");
        e.Property(x => x.FixedDamage).HasColumnName("fixed_damage");
        e.Property(x => x.BaseStrength).HasColumnName("base_strength");
        e.Property(x => x.BaseDexterity).HasColumnName("base_dexterity");
        e.Property(x => x.BaseVitality).HasColumnName("base_vitality");
        e.Property(x => x.BaseArmor).HasColumnName("base_armor");
        e.Property(x => x.MoveSpeedMps).HasColumnName("move_speed_mps").HasPrecision(7, 3);
        e.Property(x => x.MeleeRangeM).HasColumnName("melee_range_m").HasPrecision(7, 3);
        e.Property(x => x.AttackCycleMs).HasColumnName("attack_cycle_ms");
        e.Property(x => x.ImpactMs).HasColumnName("impact_ms");
        e.HasOne<CatalogRelease>().WithMany().HasForeignKey(x => x.CatalogReleaseId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.CatalogReleaseId, x.Code }).IsUnique();
    }
}

public sealed class EnemyDefinitionConfiguration : IEntityTypeConfiguration<EnemyDefinition>
{
    public void Configure(EntityTypeBuilder<EnemyDefinition> e)
    {
        e.ToTable("enemy_definitions", t =>
        {
            t.HasCheckConstraint("ck_enemy_code", "code ~ '^[a-z][a-z0-9_]{1,63}$'");
            t.HasCheckConstraint("ck_enemy_name_visual", "length(display_name) BETWEEN 1 AND 100 AND length(visual_key) BETWEEN 1 AND 100");
            t.HasCheckConstraint("ck_enemy_health_damage", "max_health > 0 AND fixed_damage >= 0");
            t.HasCheckConstraint("ck_enemy_attributes", "(base_strength IS NULL OR base_strength >= 0) AND (base_dexterity IS NULL OR base_dexterity >= 0) AND (base_vitality IS NULL OR base_vitality >= 0) AND (base_armor IS NULL OR base_armor >= 0)");
            t.HasCheckConstraint("ck_enemy_timing", "attack_range_m > 0 AND windup_ms >= 50 AND recovery_ms >= 50 AND impact_after_execute_ms >= 0");
            t.HasCheckConstraint("ck_enemy_movement", "chase_speed_mps >= 0 AND return_speed_mps >= 0 AND detection_radius_m > 0 AND disengagement_radius_m > detection_radius_m AND max_spawn_distance_m > 0 AND stopping_distance_m > 0 AND resume_distance_m > stopping_distance_m");
        });
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.CatalogReleaseId).HasColumnName("catalog_release_id");
        e.Property(x => x.Code).HasColumnName("code").IsRequired();
        e.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
        e.Property(x => x.VisualKey).HasColumnName("visual_key").IsRequired();
        e.Property(x => x.MaxHealth).HasColumnName("max_health");
        e.Property(x => x.FixedDamage).HasColumnName("fixed_damage");
        e.Property(x => x.BaseStrength).HasColumnName("base_strength");
        e.Property(x => x.BaseDexterity).HasColumnName("base_dexterity");
        e.Property(x => x.BaseVitality).HasColumnName("base_vitality");
        e.Property(x => x.BaseArmor).HasColumnName("base_armor");
        e.Property(x => x.AttackRangeM).HasColumnName("attack_range_m").HasPrecision(7, 3);
        e.Property(x => x.WindupMs).HasColumnName("windup_ms");
        e.Property(x => x.RecoveryMs).HasColumnName("recovery_ms");
        e.Property(x => x.ImpactAfterExecuteMs).HasColumnName("impact_after_execute_ms");
        e.Property(x => x.ChaseSpeedMps).HasColumnName("chase_speed_mps").HasPrecision(7, 3);
        e.Property(x => x.ReturnSpeedMps).HasColumnName("return_speed_mps").HasPrecision(7, 3);
        e.Property(x => x.DetectionRadiusM).HasColumnName("detection_radius_m").HasPrecision(7, 3);
        e.Property(x => x.DisengagementRadiusM).HasColumnName("disengagement_radius_m").HasPrecision(7, 3);
        e.Property(x => x.MaxSpawnDistanceM).HasColumnName("max_spawn_distance_m").HasPrecision(7, 3);
        e.Property(x => x.StoppingDistanceM).HasColumnName("stopping_distance_m").HasPrecision(7, 3);
        e.Property(x => x.ResumeDistanceM).HasColumnName("resume_distance_m").HasPrecision(7, 3);
        e.HasOne<CatalogRelease>().WithMany().HasForeignKey(x => x.CatalogReleaseId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.CatalogReleaseId, x.Code }).IsUnique();
    }
}

public sealed class ItemDefinitionConfiguration : IEntityTypeConfiguration<ItemDefinition>
{
    public void Configure(EntityTypeBuilder<ItemDefinition> e)
    {
        e.ToTable("item_definitions", t =>
        {
            t.HasCheckConstraint("ck_item_code", "code ~ '^[a-z][a-z0-9_]{1,63}$'");
            t.HasCheckConstraint("ck_item_name", "length(display_name) BETWEEN 1 AND 100");
            t.HasCheckConstraint("ck_item_category", "category IN ('weapon', 'armor', 'consumable', 'quest')");
            t.HasCheckConstraint("ck_item_rarity", "rarity IN ('common', 'uncommon', 'rare', 'epic', 'legendary')");
            t.HasCheckConstraint("ck_item_stack", "max_stack >= 1 AND ((stackable AND max_stack > 1) OR (NOT stackable AND max_stack = 1))");
            t.HasCheckConstraint("ck_item_slot", "equip_slot IS NULL OR equip_slot IN ('main_hand', 'off_hand', 'head', 'chest', 'hands', 'legs', 'feet')");
            t.HasCheckConstraint("ck_item_visual", "visual_key IS NULL OR length(visual_key) BETWEEN 1 AND 100");
            t.HasCheckConstraint("ck_item_category_slot", "(category IN ('weapon', 'armor') AND equip_slot IS NOT NULL) OR (category IN ('consumable', 'quest') AND equip_slot IS NULL)");
            t.HasCheckConstraint("ck_item_hands", "(category = 'weapon' AND hand_requirement IS NOT NULL AND hand_requirement IN (1, 2) AND equip_slot IN ('main_hand', 'off_hand') AND (hand_requirement = 1 OR equip_slot = 'main_hand')) OR (category <> 'weapon' AND hand_requirement IS NULL)");
        });
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.CatalogReleaseId).HasColumnName("catalog_release_id");
        e.Property(x => x.Code).HasColumnName("code").IsRequired();
        e.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
        e.Property(x => x.Category).HasColumnName("category").IsRequired();
        e.Property(x => x.Rarity).HasColumnName("rarity").IsRequired();
        e.Property(x => x.Stackable).HasColumnName("stackable").HasDefaultValue(false);
        e.Property(x => x.MaxStack).HasColumnName("max_stack").HasDefaultValue(1);
        e.Property(x => x.EquipSlot).HasColumnName("equip_slot");
        e.Property(x => x.HandRequirement).HasColumnName("hand_requirement");
        e.Property(x => x.VisualKey).HasColumnName("visual_key");
        e.HasOne<CatalogRelease>().WithMany().HasForeignKey(x => x.CatalogReleaseId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.CatalogReleaseId, x.Code }).IsUnique();
    }
}
