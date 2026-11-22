-- DESIGN PROPOSAL — NOT APPROVED FOR EXECUTION
-- Milestone 09A.2 final review. Do not run this file or treat it as an EF Core
-- migration. The first four tables and guards require owner approval; the seven
-- future tables below remain illustrative and must not enter the initial migration.
-- UUID values are supplied by the future backend.

-- IMMEDIATE CATALOG: 4 tables for 09A.2–09A.5.
CREATE TABLE catalog_releases (
    id uuid PRIMARY KEY,
    version_key text NOT NULL UNIQUE CHECK (length(version_key) BETWEEN 1 AND 64),
    schema_major smallint NOT NULL CHECK (schema_major >= 1),
    content_sha256 char(64) CHECK (content_sha256 IS NULL OR content_sha256 ~ '^[0-9a-f]{64}$'),
    status text NOT NULL CHECK (status IN ('draft', 'published', 'retired')),
    created_at timestamptz NOT NULL DEFAULT now(),
    published_at timestamptz,
    CHECK ((status = 'draft' AND published_at IS NULL)
        OR (status IN ('published', 'retired')
            AND published_at IS NOT NULL AND content_sha256 IS NOT NULL))
);
CREATE UNIQUE INDEX catalog_one_published ON catalog_releases (status)
    WHERE status = 'published';

CREATE TABLE character_classes (
    id uuid PRIMARY KEY,
    catalog_release_id uuid NOT NULL REFERENCES catalog_releases(id) ON DELETE RESTRICT,
    code text NOT NULL CHECK (code ~ '^[a-z][a-z0-9_]{1,63}$'),
    display_name text NOT NULL CHECK (length(display_name) BETWEEN 1 AND 100),
    visual_key text NOT NULL CHECK (length(visual_key) BETWEEN 1 AND 100),
    base_max_health integer NOT NULL CHECK (base_max_health > 0),
    fixed_damage integer NOT NULL CHECK (fixed_damage >= 0),
    base_strength integer CHECK (base_strength >= 0),
    base_dexterity integer CHECK (base_dexterity >= 0),
    base_vitality integer CHECK (base_vitality >= 0),
    base_armor integer CHECK (base_armor >= 0),
    move_speed_mps numeric(7,3) NOT NULL CHECK (move_speed_mps > 0),
    melee_range_m numeric(7,3) NOT NULL CHECK (melee_range_m > 0),
    attack_cycle_ms integer NOT NULL CHECK (attack_cycle_ms >= 100),
    impact_ms integer NOT NULL CHECK (impact_ms BETWEEN 0 AND attack_cycle_ms),
    UNIQUE (catalog_release_id, code)
);

CREATE TABLE enemy_definitions (
    id uuid PRIMARY KEY,
    catalog_release_id uuid NOT NULL REFERENCES catalog_releases(id) ON DELETE RESTRICT,
    code text NOT NULL CHECK (code ~ '^[a-z][a-z0-9_]{1,63}$'),
    display_name text NOT NULL CHECK (length(display_name) BETWEEN 1 AND 100),
    visual_key text NOT NULL CHECK (length(visual_key) BETWEEN 1 AND 100),
    max_health integer NOT NULL CHECK (max_health > 0),
    fixed_damage integer NOT NULL CHECK (fixed_damage >= 0),
    base_strength integer CHECK (base_strength >= 0),
    base_dexterity integer CHECK (base_dexterity >= 0),
    base_vitality integer CHECK (base_vitality >= 0),
    base_armor integer CHECK (base_armor >= 0),
    attack_range_m numeric(7,3) NOT NULL CHECK (attack_range_m > 0),
    windup_ms integer NOT NULL CHECK (windup_ms >= 50),
    recovery_ms integer NOT NULL CHECK (recovery_ms >= 50),
    impact_after_execute_ms integer NOT NULL CHECK (impact_after_execute_ms >= 0),
    chase_speed_mps numeric(7,3) NOT NULL CHECK (chase_speed_mps >= 0),
    return_speed_mps numeric(7,3) NOT NULL CHECK (return_speed_mps >= 0),
    detection_radius_m numeric(7,3) NOT NULL CHECK (detection_radius_m > 0),
    disengagement_radius_m numeric(7,3) NOT NULL CHECK (disengagement_radius_m > detection_radius_m),
    max_spawn_distance_m numeric(7,3) NOT NULL CHECK (max_spawn_distance_m > 0),
    stopping_distance_m numeric(7,3) NOT NULL CHECK (stopping_distance_m > 0),
    resume_distance_m numeric(7,3) NOT NULL CHECK (resume_distance_m > stopping_distance_m),
    UNIQUE (catalog_release_id, code)
);

CREATE TABLE item_definitions (
    id uuid PRIMARY KEY,
    catalog_release_id uuid NOT NULL REFERENCES catalog_releases(id) ON DELETE RESTRICT,
    code text NOT NULL CHECK (code ~ '^[a-z][a-z0-9_]{1,63}$'),
    display_name text NOT NULL CHECK (length(display_name) BETWEEN 1 AND 100),
    category text NOT NULL CHECK (category IN ('weapon', 'armor', 'consumable', 'quest')),
    rarity text NOT NULL CHECK (rarity IN ('common', 'uncommon', 'rare', 'epic', 'legendary')),
    stackable boolean NOT NULL DEFAULT false,
    max_stack integer NOT NULL DEFAULT 1 CHECK (max_stack >= 1),
    equip_slot text CHECK (equip_slot IN ('main_hand', 'off_hand', 'head', 'chest', 'hands', 'legs', 'feet')),
    hand_requirement smallint,
    visual_key text CHECK (visual_key IS NULL OR length(visual_key) BETWEEN 1 AND 100),
    UNIQUE (catalog_release_id, code),
    CHECK ((stackable AND max_stack > 1) OR (NOT stackable AND max_stack = 1)),
    CHECK ((category IN ('weapon', 'armor') AND equip_slot IS NOT NULL)
        OR (category IN ('consumable', 'quest') AND equip_slot IS NULL)),
    CHECK ((category = 'weapon' AND hand_requirement IS NOT NULL
            AND hand_requirement IN (1, 2)
            AND equip_slot IN ('main_hand', 'off_hand')
            AND (hand_requirement = 1 OR equip_slot = 'main_hand'))
        OR (category <> 'weapon' AND hand_requirement IS NULL))
);

-- Proposed database guard. A future migration should install these functions and
-- triggers with the four tables. The runtime application role receives SELECT,
-- never DML, TRUNCATE or DDL. A trusted publisher may edit draft rows only.
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
    -- Serialize draft edits with the parent publication UPDATE.
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

-- FUTURE PERSISTENCE: 7 tables. Not part of initial catalog implementation.
CREATE TABLE accounts (
    id uuid PRIMARY KEY,
    identity_provider text NOT NULL CHECK (length(identity_provider) BETWEEN 1 AND 64),
    provider_subject text NOT NULL CHECK (length(provider_subject) BETWEEN 1 AND 255),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (identity_provider, provider_subject)
);

CREATE TABLE player_characters (
    id uuid PRIMARY KEY,
    account_id uuid NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT,
    class_definition_id uuid NOT NULL REFERENCES character_classes(id) ON DELETE RESTRICT,
    character_name text NOT NULL CHECK (length(character_name) BETWEEN 3 AND 32),
    experience bigint NOT NULL DEFAULT 0 CHECK (experience >= 0),
    allocated_strength integer NOT NULL DEFAULT 0 CHECK (allocated_strength >= 0),
    allocated_dexterity integer NOT NULL DEFAULT 0 CHECK (allocated_dexterity >= 0),
    allocated_vitality integer NOT NULL DEFAULT 0 CHECK (allocated_vitality >= 0),
    row_revision bigint NOT NULL DEFAULT 1 CHECK (row_revision >= 1),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    deleted_at timestamptz
);
CREATE UNIQUE INDEX character_active_name_per_account
    ON player_characters (account_id, lower(character_name))
    WHERE deleted_at IS NULL;

CREATE TABLE class_level_rules (
    class_definition_id uuid NOT NULL REFERENCES character_classes(id) ON DELETE RESTRICT,
    level integer NOT NULL CHECK (level >= 1),
    required_total_xp bigint NOT NULL CHECK (required_total_xp >= 0),
    health_bonus integer NOT NULL DEFAULT 0,
    damage_bonus integer NOT NULL DEFAULT 0,
    attribute_points integer NOT NULL DEFAULT 0 CHECK (attribute_points >= 0),
    PRIMARY KEY (class_definition_id, level),
    UNIQUE (class_definition_id, required_total_xp),
    CHECK (level <> 1 OR required_total_xp = 0)
);

CREATE TABLE item_stat_modifiers (
    item_definition_id uuid NOT NULL REFERENCES item_definitions(id) ON DELETE RESTRICT,
    stat_code text NOT NULL CHECK (stat_code IN
        ('max_health', 'fixed_damage', 'strength', 'dexterity', 'vitality',
         'armor', 'attack_speed', 'critical_chance', 'critical_multiplier')),
    modifier_kind text NOT NULL CHECK (modifier_kind IN ('flat', 'percent')),
    amount numeric(12,4) NOT NULL CHECK (amount BETWEEN -100000 AND 100000),
    PRIMARY KEY (item_definition_id, stat_code, modifier_kind)
);

CREATE TABLE owned_items (
    id uuid PRIMARY KEY,
    character_id uuid NOT NULL REFERENCES player_characters(id) ON DELETE CASCADE,
    item_definition_id uuid NOT NULL REFERENCES item_definitions(id) ON DELETE RESTRICT,
    quantity integer NOT NULL CHECK (quantity >= 1),
    row_revision bigint NOT NULL DEFAULT 1 CHECK (row_revision >= 1),
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (id, character_id)
);
CREATE INDEX owned_items_by_character ON owned_items (character_id);
CREATE INDEX owned_items_by_definition ON owned_items (item_definition_id);

CREATE TABLE character_equipment (
    character_id uuid NOT NULL REFERENCES player_characters(id) ON DELETE CASCADE,
    slot_code text NOT NULL CHECK (slot_code IN
        ('main_hand', 'off_hand', 'head', 'chest', 'hands', 'legs', 'feet')),
    owned_item_id uuid NOT NULL UNIQUE,
    equipped_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (character_id, slot_code),
    FOREIGN KEY (owned_item_id, character_id)
        REFERENCES owned_items(id, character_id) ON DELETE CASCADE
);

CREATE TABLE exploration_chunks (
    character_id uuid NOT NULL REFERENCES player_characters(id) ON DELETE CASCADE,
    region_key text NOT NULL CHECK (length(region_key) BETWEEN 1 AND 100),
    map_revision integer NOT NULL CHECK (map_revision >= 1),
    chunk_x integer NOT NULL,
    chunk_z integer NOT NULL,
    grid_width smallint NOT NULL CHECK (grid_width BETWEEN 1 AND 64),
    grid_height smallint NOT NULL CHECK (grid_height BETWEEN 1 AND 64),
    cell_size_m numeric(7,3) NOT NULL CHECK (cell_size_m > 0),
    encoding text NOT NULL CHECK (encoding IN ('raw_bits', 'deflate')),
    discovered_mask bytea NOT NULL CHECK (octet_length(discovered_mask) > 0),
    row_revision bigint NOT NULL DEFAULT 1 CHECK (row_revision >= 1),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (character_id, region_key, map_revision, chunk_x, chunk_z)
);
