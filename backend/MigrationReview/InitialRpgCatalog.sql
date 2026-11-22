-- DESIGN PROPOSAL — NOT APPROVED FOR EXECUTION
-- Generated EF Core 8.0.11 PostgreSQL migration script from 0 to InitialRpgCatalog.
-- Review only. Do not run against ashenfall_dev without separate owner approval.
-- EF migration-history metadata plus four RPG catalog tables follow.
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

CREATE TABLE catalog_releases (
    id uuid NOT NULL,
    version_key text NOT NULL,
    schema_major smallint NOT NULL,
    content_sha256 char(64),
    status text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT (now()),
    published_at timestamptz,
    CONSTRAINT "PK_catalog_releases" PRIMARY KEY (id),
    CONSTRAINT ck_catalog_hash CHECK (content_sha256 IS NULL OR content_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_catalog_publication CHECK ((status = 'draft' AND published_at IS NULL) OR (status IN ('published', 'retired') AND published_at IS NOT NULL AND content_sha256 IS NOT NULL)),
    CONSTRAINT ck_catalog_schema_major CHECK (schema_major >= 1),
    CONSTRAINT ck_catalog_status CHECK (status IN ('draft', 'published', 'retired')),
    CONSTRAINT ck_catalog_version_length CHECK (length(version_key) BETWEEN 1 AND 64)
);

CREATE TABLE character_classes (
    id uuid NOT NULL,
    catalog_release_id uuid NOT NULL,
    code text NOT NULL,
    display_name text NOT NULL,
    visual_key text NOT NULL,
    base_max_health integer NOT NULL,
    fixed_damage integer NOT NULL,
    base_strength integer,
    base_dexterity integer,
    base_vitality integer,
    base_armor integer,
    move_speed_mps numeric(7,3) NOT NULL,
    melee_range_m numeric(7,3) NOT NULL,
    attack_cycle_ms integer NOT NULL,
    impact_ms integer NOT NULL,
    CONSTRAINT "PK_character_classes" PRIMARY KEY (id),
    CONSTRAINT ck_class_attributes CHECK ((base_strength IS NULL OR base_strength >= 0) AND (base_dexterity IS NULL OR base_dexterity >= 0) AND (base_vitality IS NULL OR base_vitality >= 0) AND (base_armor IS NULL OR base_armor >= 0)),
    CONSTRAINT ck_class_code CHECK (code ~ '^[a-z][a-z0-9_]{1,63}$'),
    CONSTRAINT ck_class_damage CHECK (fixed_damage >= 0),
    CONSTRAINT ck_class_health CHECK (base_max_health > 0),
    CONSTRAINT ck_class_name CHECK (length(display_name) BETWEEN 1 AND 100),
    CONSTRAINT ck_class_speed_range CHECK (move_speed_mps > 0 AND melee_range_m > 0),
    CONSTRAINT ck_class_timing CHECK (attack_cycle_ms >= 100 AND impact_ms BETWEEN 0 AND attack_cycle_ms),
    CONSTRAINT ck_class_visual CHECK (length(visual_key) BETWEEN 1 AND 100),
    CONSTRAINT "FK_character_classes_catalog_releases_catalog_release_id" FOREIGN KEY (catalog_release_id) REFERENCES catalog_releases (id) ON DELETE RESTRICT
);

CREATE TABLE enemy_definitions (
    id uuid NOT NULL,
    catalog_release_id uuid NOT NULL,
    code text NOT NULL,
    display_name text NOT NULL,
    visual_key text NOT NULL,
    max_health integer NOT NULL,
    fixed_damage integer NOT NULL,
    base_strength integer,
    base_dexterity integer,
    base_vitality integer,
    base_armor integer,
    attack_range_m numeric(7,3) NOT NULL,
    windup_ms integer NOT NULL,
    recovery_ms integer NOT NULL,
    impact_after_execute_ms integer NOT NULL,
    chase_speed_mps numeric(7,3) NOT NULL,
    return_speed_mps numeric(7,3) NOT NULL,
    detection_radius_m numeric(7,3) NOT NULL,
    disengagement_radius_m numeric(7,3) NOT NULL,
    max_spawn_distance_m numeric(7,3) NOT NULL,
    stopping_distance_m numeric(7,3) NOT NULL,
    resume_distance_m numeric(7,3) NOT NULL,
    CONSTRAINT "PK_enemy_definitions" PRIMARY KEY (id),
    CONSTRAINT ck_enemy_attributes CHECK ((base_strength IS NULL OR base_strength >= 0) AND (base_dexterity IS NULL OR base_dexterity >= 0) AND (base_vitality IS NULL OR base_vitality >= 0) AND (base_armor IS NULL OR base_armor >= 0)),
    CONSTRAINT ck_enemy_code CHECK (code ~ '^[a-z][a-z0-9_]{1,63}$'),
    CONSTRAINT ck_enemy_health_damage CHECK (max_health > 0 AND fixed_damage >= 0),
    CONSTRAINT ck_enemy_movement CHECK (chase_speed_mps >= 0 AND return_speed_mps >= 0 AND detection_radius_m > 0 AND disengagement_radius_m > detection_radius_m AND max_spawn_distance_m > 0 AND stopping_distance_m > 0 AND resume_distance_m > stopping_distance_m),
    CONSTRAINT ck_enemy_name_visual CHECK (length(display_name) BETWEEN 1 AND 100 AND length(visual_key) BETWEEN 1 AND 100),
    CONSTRAINT ck_enemy_timing CHECK (attack_range_m > 0 AND windup_ms >= 50 AND recovery_ms >= 50 AND impact_after_execute_ms >= 0),
    CONSTRAINT "FK_enemy_definitions_catalog_releases_catalog_release_id" FOREIGN KEY (catalog_release_id) REFERENCES catalog_releases (id) ON DELETE RESTRICT
);

CREATE TABLE item_definitions (
    id uuid NOT NULL,
    catalog_release_id uuid NOT NULL,
    code text NOT NULL,
    display_name text NOT NULL,
    category text NOT NULL,
    rarity text NOT NULL,
    stackable boolean NOT NULL DEFAULT FALSE,
    max_stack integer NOT NULL DEFAULT 1,
    equip_slot text,
    hand_requirement smallint,
    visual_key text,
    CONSTRAINT "PK_item_definitions" PRIMARY KEY (id),
    CONSTRAINT ck_item_category CHECK (category IN ('weapon', 'armor', 'consumable', 'quest')),
    CONSTRAINT ck_item_category_slot CHECK ((category IN ('weapon', 'armor') AND equip_slot IS NOT NULL) OR (category IN ('consumable', 'quest') AND equip_slot IS NULL)),
    CONSTRAINT ck_item_code CHECK (code ~ '^[a-z][a-z0-9_]{1,63}$'),
    CONSTRAINT ck_item_hands CHECK ((category = 'weapon' AND hand_requirement IS NOT NULL AND hand_requirement IN (1, 2) AND equip_slot IN ('main_hand', 'off_hand') AND (hand_requirement = 1 OR equip_slot = 'main_hand')) OR (category <> 'weapon' AND hand_requirement IS NULL)),
    CONSTRAINT ck_item_name CHECK (length(display_name) BETWEEN 1 AND 100),
    CONSTRAINT ck_item_rarity CHECK (rarity IN ('common', 'uncommon', 'rare', 'epic', 'legendary')),
    CONSTRAINT ck_item_slot CHECK (equip_slot IS NULL OR equip_slot IN ('main_hand', 'off_hand', 'head', 'chest', 'hands', 'legs', 'feet')),
    CONSTRAINT ck_item_stack CHECK (max_stack >= 1 AND ((stackable AND max_stack > 1) OR (NOT stackable AND max_stack = 1))),
    CONSTRAINT ck_item_visual CHECK (visual_key IS NULL OR length(visual_key) BETWEEN 1 AND 100),
    CONSTRAINT "FK_item_definitions_catalog_releases_catalog_release_id" FOREIGN KEY (catalog_release_id) REFERENCES catalog_releases (id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX catalog_one_published ON catalog_releases (status) WHERE status = 'published';

CREATE UNIQUE INDEX "IX_catalog_releases_version_key" ON catalog_releases (version_key);

CREATE UNIQUE INDEX "IX_character_classes_catalog_release_id_code" ON character_classes (catalog_release_id, code);

CREATE UNIQUE INDEX "IX_enemy_definitions_catalog_release_id_code" ON enemy_definitions (catalog_release_id, code);

CREATE UNIQUE INDEX "IX_item_definitions_catalog_release_id_code" ON item_definitions (catalog_release_id, code);

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

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115111756_InitialRpgCatalog', '8.0.11');

COMMIT;