using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ashenfall.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialRpgCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_releases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_key = table.Column<string>(type: "text", nullable: false),
                    schema_major = table.Column<short>(type: "smallint", nullable: false),
                    content_sha256 = table.Column<string>(type: "char(64)", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    published_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_releases", x => x.id);
                    table.CheckConstraint("ck_catalog_hash", "content_sha256 IS NULL OR content_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_catalog_publication", "(status = 'draft' AND published_at IS NULL) OR (status IN ('published', 'retired') AND published_at IS NOT NULL AND content_sha256 IS NOT NULL)");
                    table.CheckConstraint("ck_catalog_schema_major", "schema_major >= 1");
                    table.CheckConstraint("ck_catalog_status", "status IN ('draft', 'published', 'retired')");
                    table.CheckConstraint("ck_catalog_version_length", "length(version_key) BETWEEN 1 AND 64");
                });

            migrationBuilder.CreateTable(
                name: "character_classes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    visual_key = table.Column<string>(type: "text", nullable: false),
                    base_max_health = table.Column<int>(type: "integer", nullable: false),
                    fixed_damage = table.Column<int>(type: "integer", nullable: false),
                    base_strength = table.Column<int>(type: "integer", nullable: true),
                    base_dexterity = table.Column<int>(type: "integer", nullable: true),
                    base_vitality = table.Column<int>(type: "integer", nullable: true),
                    base_armor = table.Column<int>(type: "integer", nullable: true),
                    move_speed_mps = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false),
                    melee_range_m = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false),
                    attack_cycle_ms = table.Column<int>(type: "integer", nullable: false),
                    impact_ms = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_classes", x => x.id);
                    table.CheckConstraint("ck_class_attributes", "(base_strength IS NULL OR base_strength >= 0) AND (base_dexterity IS NULL OR base_dexterity >= 0) AND (base_vitality IS NULL OR base_vitality >= 0) AND (base_armor IS NULL OR base_armor >= 0)");
                    table.CheckConstraint("ck_class_code", "code ~ '^[a-z][a-z0-9_]{1,63}$'");
                    table.CheckConstraint("ck_class_damage", "fixed_damage >= 0");
                    table.CheckConstraint("ck_class_health", "base_max_health > 0");
                    table.CheckConstraint("ck_class_name", "length(display_name) BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_class_speed_range", "move_speed_mps > 0 AND melee_range_m > 0");
                    table.CheckConstraint("ck_class_timing", "attack_cycle_ms >= 100 AND impact_ms BETWEEN 0 AND attack_cycle_ms");
                    table.CheckConstraint("ck_class_visual", "length(visual_key) BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_character_classes_catalog_releases_catalog_release_id",
                        column: x => x.catalog_release_id,
                        principalTable: "catalog_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "enemy_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    visual_key = table.Column<string>(type: "text", nullable: false),
                    max_health = table.Column<int>(type: "integer", nullable: false),
                    fixed_damage = table.Column<int>(type: "integer", nullable: false),
                    base_strength = table.Column<int>(type: "integer", nullable: true),
                    base_dexterity = table.Column<int>(type: "integer", nullable: true),
                    base_vitality = table.Column<int>(type: "integer", nullable: true),
                    base_armor = table.Column<int>(type: "integer", nullable: true),
                    attack_range_m = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false),
                    windup_ms = table.Column<int>(type: "integer", nullable: false),
                    recovery_ms = table.Column<int>(type: "integer", nullable: false),
                    impact_after_execute_ms = table.Column<int>(type: "integer", nullable: false),
                    chase_speed_mps = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false),
                    return_speed_mps = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false),
                    detection_radius_m = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false),
                    disengagement_radius_m = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false),
                    max_spawn_distance_m = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false),
                    stopping_distance_m = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false),
                    resume_distance_m = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enemy_definitions", x => x.id);
                    table.CheckConstraint("ck_enemy_attributes", "(base_strength IS NULL OR base_strength >= 0) AND (base_dexterity IS NULL OR base_dexterity >= 0) AND (base_vitality IS NULL OR base_vitality >= 0) AND (base_armor IS NULL OR base_armor >= 0)");
                    table.CheckConstraint("ck_enemy_code", "code ~ '^[a-z][a-z0-9_]{1,63}$'");
                    table.CheckConstraint("ck_enemy_health_damage", "max_health > 0 AND fixed_damage >= 0");
                    table.CheckConstraint("ck_enemy_movement", "chase_speed_mps >= 0 AND return_speed_mps >= 0 AND detection_radius_m > 0 AND disengagement_radius_m > detection_radius_m AND max_spawn_distance_m > 0 AND stopping_distance_m > 0 AND resume_distance_m > stopping_distance_m");
                    table.CheckConstraint("ck_enemy_name_visual", "length(display_name) BETWEEN 1 AND 100 AND length(visual_key) BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_enemy_timing", "attack_range_m > 0 AND windup_ms >= 50 AND recovery_ms >= 50 AND impact_after_execute_ms >= 0");
                    table.ForeignKey(
                        name: "FK_enemy_definitions_catalog_releases_catalog_release_id",
                        column: x => x.catalog_release_id,
                        principalTable: "catalog_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    rarity = table.Column<string>(type: "text", nullable: false),
                    stackable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    max_stack = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    equip_slot = table.Column<string>(type: "text", nullable: true),
                    hand_requirement = table.Column<short>(type: "smallint", nullable: true),
                    visual_key = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_definitions", x => x.id);
                    table.CheckConstraint("ck_item_category", "category IN ('weapon', 'armor', 'consumable', 'quest')");
                    table.CheckConstraint("ck_item_category_slot", "(category IN ('weapon', 'armor') AND equip_slot IS NOT NULL) OR (category IN ('consumable', 'quest') AND equip_slot IS NULL)");
                    table.CheckConstraint("ck_item_code", "code ~ '^[a-z][a-z0-9_]{1,63}$'");
                    table.CheckConstraint("ck_item_hands", "(category = 'weapon' AND hand_requirement IS NOT NULL AND hand_requirement IN (1, 2) AND equip_slot IN ('main_hand', 'off_hand') AND (hand_requirement = 1 OR equip_slot = 'main_hand')) OR (category <> 'weapon' AND hand_requirement IS NULL)");
                    table.CheckConstraint("ck_item_name", "length(display_name) BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_item_rarity", "rarity IN ('common', 'uncommon', 'rare', 'epic', 'legendary')");
                    table.CheckConstraint("ck_item_slot", "equip_slot IS NULL OR equip_slot IN ('main_hand', 'off_hand', 'head', 'chest', 'hands', 'legs', 'feet')");
                    table.CheckConstraint("ck_item_stack", "max_stack >= 1 AND ((stackable AND max_stack > 1) OR (NOT stackable AND max_stack = 1))");
                    table.CheckConstraint("ck_item_visual", "visual_key IS NULL OR length(visual_key) BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_item_definitions_catalog_releases_catalog_release_id",
                        column: x => x.catalog_release_id,
                        principalTable: "catalog_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "catalog_one_published",
                table: "catalog_releases",
                column: "status",
                unique: true,
                filter: "status = 'published'");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_releases_version_key",
                table: "catalog_releases",
                column: "version_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_classes_catalog_release_id_code",
                table: "character_classes",
                columns: new[] { "catalog_release_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_enemy_definitions_catalog_release_id_code",
                table: "enemy_definitions",
                columns: new[] { "catalog_release_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_definitions_catalog_release_id_code",
                table: "item_definitions",
                columns: new[] { "catalog_release_id", "code" },
                unique: true);

            // Reviewed catalog guards. The runtime API role must never own these objects.
            migrationBuilder.Sql("""
                CREATE FUNCTION catalog_guard_release() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'draft' THEN
                            RAISE EXCEPTION 'catalog release must begin as draft';
                        END IF;
                        RETURN NEW;
                    END IF;
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.status <> 'draft' THEN
                            RAISE EXCEPTION 'published or retired catalog release is immutable';
                        END IF;
                        RETURN OLD;
                    END IF;
                    IF NEW.id IS DISTINCT FROM OLD.id
                        OR NEW.version_key IS DISTINCT FROM OLD.version_key
                        OR NEW.schema_major IS DISTINCT FROM OLD.schema_major
                        OR NEW.created_at IS DISTINCT FROM OLD.created_at THEN
                        RAISE EXCEPTION 'catalog release identity is immutable';
                    END IF;
                    IF OLD.status = 'draft' AND NEW.status IN ('draft', 'published') THEN
                        RETURN NEW;
                    END IF;
                    IF OLD.status = 'published' AND NEW.status = 'retired'
                        AND NEW.content_sha256 IS NOT DISTINCT FROM OLD.content_sha256
                        AND NEW.published_at IS NOT DISTINCT FROM OLD.published_at THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'invalid catalog release transition';
                END;
                $$;
                CREATE TRIGGER catalog_release_guard
                    BEFORE INSERT OR UPDATE OR DELETE ON catalog_releases
                    FOR EACH ROW EXECUTE FUNCTION catalog_guard_release();

                CREATE FUNCTION catalog_guard_definition() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    release_id uuid;
                    release_status text;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        release_id := OLD.catalog_release_id;
                    ELSE
                        release_id := NEW.catalog_release_id;
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF NEW.catalog_release_id IS DISTINCT FROM OLD.catalog_release_id THEN
                            RAISE EXCEPTION 'definition cannot change catalog release';
                        END IF;
                    END IF;
                    SELECT status INTO release_status FROM catalog_releases
                        WHERE id = release_id FOR UPDATE;
                    IF release_status IS DISTINCT FROM 'draft' THEN
                        RAISE EXCEPTION 'published or retired catalog definition is immutable';
                    END IF;
                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER character_class_catalog_guard
                    BEFORE INSERT OR UPDATE OR DELETE ON character_classes
                    FOR EACH ROW EXECUTE FUNCTION catalog_guard_definition();
                CREATE TRIGGER enemy_definition_catalog_guard
                    BEFORE INSERT OR UPDATE OR DELETE ON enemy_definitions
                    FOR EACH ROW EXECUTE FUNCTION catalog_guard_definition();
                CREATE TRIGGER item_definition_catalog_guard
                    BEFORE INSERT OR UPDATE OR DELETE ON item_definitions
                    FOR EACH ROW EXECUTE FUNCTION catalog_guard_definition();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER item_definition_catalog_guard ON item_definitions;
                DROP TRIGGER enemy_definition_catalog_guard ON enemy_definitions;
                DROP TRIGGER character_class_catalog_guard ON character_classes;
                DROP TRIGGER catalog_release_guard ON catalog_releases;
                DROP FUNCTION catalog_guard_definition();
                DROP FUNCTION catalog_guard_release();
                """);
            migrationBuilder.DropTable(
                name: "character_classes");

            migrationBuilder.DropTable(
                name: "enemy_definitions");

            migrationBuilder.DropTable(
                name: "item_definitions");

            migrationBuilder.DropTable(
                name: "catalog_releases");
        }
    }
}
