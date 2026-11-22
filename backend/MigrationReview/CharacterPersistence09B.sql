-- Ashenfall 09B.1 persistence migration; REVIEW ONLY.
-- DO NOT EXECUTE without the project owner's explicit migration approval.
-- psql -X -w -v ON_ERROR_STOP=1 -f CharacterPersistence09B.sql
-- Apply with a schema-administrative identity, never as ashenfall_app.
\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SET LOCAL search_path = public, pg_catalog;
LOCK TABLE catalog_releases, character_classes, item_definitions IN SHARE MODE;
DO $preflight$
BEGIN
    IF current_database() <> 'ashenfall_dev' THEN
        RAISE EXCEPTION '09B.1 requires ashenfall_dev';
    END IF;
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL OR
        to_regclass('public.catalog_releases') IS NULL OR
        to_regclass('public.character_classes') IS NULL OR
        to_regclass('public.enemy_definitions') IS NULL OR
        to_regclass('public.item_definitions') IS NULL OR
        to_regprocedure('public.catalog_guard_release()') IS NULL OR
        to_regprocedure('public.catalog_guard_definition()') IS NULL THEN
        RAISE EXCEPTION '09B.1 catalog prerequisites are missing';
    END IF;
    IF (SELECT count(*) FROM public."__EFMigrationsHistory") <> 1 OR
        NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory"
            WHERE "MigrationId" = '20261115111756_InitialRpgCatalog') THEN
        RAISE EXCEPTION '09B.1 unexpected EF migration state';
    END IF;
    IF (SELECT count(*) FROM public.catalog_releases WHERE status = 'published') <> 1 OR
        NOT EXISTS (SELECT 1 FROM public.catalog_releases
            WHERE version_key = 'prototype-1' AND status = 'published'
              AND content_sha256 = 'a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17') THEN
        RAISE EXCEPTION '09B.1 unexpected published catalog';
    END IF;
    IF to_regrole('ashenfall_app') IS NULL OR to_regrole('ashenfall_publisher') IS NULL OR
        NOT has_schema_privilege(current_user, 'public', 'CREATE') OR
        NOT has_schema_privilege('ashenfall_app', 'public', 'USAGE') OR
        has_schema_privilege('ashenfall_app', 'public', 'CREATE') THEN
        RAISE EXCEPTION '09B.1 role or schema privilege prerequisite failed';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_class WHERE relnamespace = 'public'::regnamespace
        AND relname IN ('accounts','player_characters','class_level_rules',
            'item_stat_modifiers','owned_items','character_equipment')) OR
        to_regprocedure('public.catalog_guard_derived_definition()') IS NOT NULL THEN
        RAISE EXCEPTION '09B.1 persistence objects already exist';
    END IF;
END;
$preflight$;
CREATE TABLE accounts (
    id uuid PRIMARY KEY,
    identity_provider text NOT NULL CHECK (length(identity_provider) BETWEEN 1 AND 64),
    provider_subject text NOT NULL CHECK (length(provider_subject) BETWEEN 1 AND 255),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (identity_provider, provider_subject)
);
CREATE TABLE class_level_rules (
    class_definition_id uuid NOT NULL REFERENCES character_classes(id) ON DELETE RESTRICT,
    level integer NOT NULL CHECK (level BETWEEN 1 AND 1000),
    required_total_xp bigint NOT NULL CHECK (required_total_xp >= 0),
    health_bonus integer NOT NULL DEFAULT 0 CHECK (health_bonus BETWEEN 0 AND 100000),
    damage_bonus integer NOT NULL DEFAULT 0 CHECK (damage_bonus BETWEEN 0 AND 100000),
    attribute_points integer NOT NULL DEFAULT 0 CHECK (attribute_points BETWEEN 0 AND 1000),
    PRIMARY KEY (class_definition_id, level),
    UNIQUE (class_definition_id, required_total_xp),
    CHECK (level <> 1 OR required_total_xp = 0)
);
CREATE TABLE item_stat_modifiers (
    item_definition_id uuid NOT NULL REFERENCES item_definitions(id) ON DELETE RESTRICT,
    stat_code text NOT NULL CHECK (stat_code IN
        ('max_health','fixed_damage','strength','dexterity','vitality','armor',
         'attack_speed','critical_chance','critical_multiplier')),
    modifier_kind text NOT NULL CHECK (modifier_kind IN ('flat','percent')),
    amount numeric(12,4) NOT NULL CHECK (amount BETWEEN -100000 AND 100000),
    PRIMARY KEY (item_definition_id, stat_code, modifier_kind)
);
-- New authored rules/modifiers inherit the release lifecycle of their catalog
-- definitions. The existing prototype-1 published release remains immutable.
CREATE FUNCTION catalog_guard_derived_definition() RETURNS trigger LANGUAGE plpgsql
    SET search_path = public, pg_catalog AS $$
DECLARE definition_id uuid; release_status text;
BEGIN
    IF TG_TABLE_NAME = 'class_level_rules' THEN
        IF TG_OP = 'DELETE' THEN definition_id := OLD.class_definition_id;
        ELSE definition_id := NEW.class_definition_id; END IF;
        IF TG_OP = 'UPDATE' THEN
            IF NEW.class_definition_id IS DISTINCT FROM OLD.class_definition_id THEN
                RAISE EXCEPTION 'class level rule cannot change definition';
            END IF;
        END IF;
        SELECT r.status INTO release_status FROM catalog_releases r
            JOIN character_classes c ON c.catalog_release_id = r.id
            WHERE c.id = definition_id FOR UPDATE OF r;
    ELSE
        IF TG_OP = 'DELETE' THEN definition_id := OLD.item_definition_id;
        ELSE definition_id := NEW.item_definition_id; END IF;
        IF TG_OP = 'UPDATE' THEN
            IF NEW.item_definition_id IS DISTINCT FROM OLD.item_definition_id THEN
                RAISE EXCEPTION 'item modifier cannot change definition';
            END IF;
        END IF;
        SELECT r.status INTO release_status FROM catalog_releases r
            JOIN item_definitions i ON i.catalog_release_id = r.id
            WHERE i.id = definition_id FOR UPDATE OF r;
    END IF;
    IF release_status IS DISTINCT FROM 'draft' THEN
        RAISE EXCEPTION 'published or retired catalog extension is immutable';
    END IF;
    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER class_level_rule_catalog_guard
    BEFORE INSERT OR UPDATE OR DELETE ON class_level_rules
    FOR EACH ROW EXECUTE FUNCTION catalog_guard_derived_definition();
CREATE TRIGGER item_modifier_catalog_guard
    BEFORE INSERT OR UPDATE OR DELETE ON item_stat_modifiers
    FOR EACH ROW EXECUTE FUNCTION catalog_guard_derived_definition();
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
CREATE UNIQUE INDEX one_active_character_per_account ON player_characters(account_id)
    WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX character_active_name_per_account
    ON player_characters(account_id, lower(character_name)) WHERE deleted_at IS NULL;
CREATE TABLE owned_items (
    id uuid PRIMARY KEY,
    character_id uuid NOT NULL REFERENCES player_characters(id) ON DELETE CASCADE,
    item_definition_id uuid NOT NULL REFERENCES item_definitions(id) ON DELETE RESTRICT,
    quantity integer NOT NULL CHECK (quantity >= 1),
    row_revision bigint NOT NULL DEFAULT 1 CHECK (row_revision >= 1),
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (id, character_id)
);
CREATE INDEX owned_items_by_character ON owned_items(character_id);
CREATE INDEX owned_items_by_definition ON owned_items(item_definition_id);
CREATE TABLE character_equipment (
    character_id uuid NOT NULL REFERENCES player_characters(id) ON DELETE CASCADE,
    slot_code text NOT NULL CHECK (slot_code IN
        ('main_hand','off_hand','head','chest','hands','legs','feet')),
    owned_item_id uuid NOT NULL UNIQUE,
    equipped_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (character_id, slot_code),
    FOREIGN KEY (owned_item_id, character_id)
        REFERENCES owned_items(id, character_id) ON DELETE CASCADE
);
-- Player-owned rows are mutable only through revisioned writes. Locks on the
-- character serialize inventory/equipment changes made by the runtime role.
CREATE FUNCTION character_guard_mutation() RETURNS trigger LANGUAGE plpgsql
    SET search_path = public, pg_catalog AS $$
DECLARE release_status text;
BEGIN
    IF TG_OP = 'INSERT' THEN
        IF NEW.row_revision <> 1 OR NEW.deleted_at IS NOT NULL THEN
            RAISE EXCEPTION 'new character must start active at revision one';
        END IF;
        SELECT r.status INTO release_status FROM character_classes c
            JOIN catalog_releases r ON r.id = c.catalog_release_id
            WHERE c.id = NEW.class_definition_id;
        IF release_status IS DISTINCT FROM 'published' THEN
            RAISE EXCEPTION 'character class must be published';
        END IF;
    ELSE
        IF OLD.deleted_at IS NOT NULL OR NEW.id IS DISTINCT FROM OLD.id OR
            NEW.account_id IS DISTINCT FROM OLD.account_id OR
            NEW.class_definition_id IS DISTINCT FROM OLD.class_definition_id OR
            NEW.created_at IS DISTINCT FROM OLD.created_at OR
            NEW.row_revision <> OLD.row_revision + 1 OR
            NEW.experience < OLD.experience THEN
            RAISE EXCEPTION 'invalid character update or revision';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER character_mutation_guard
    BEFORE INSERT OR UPDATE ON player_characters
    FOR EACH ROW EXECUTE FUNCTION character_guard_mutation();

CREATE FUNCTION owned_item_guard_mutation() RETURNS trigger LANGUAGE plpgsql
    SET search_path = public, pg_catalog AS $$
DECLARE item_stackable boolean; item_max_stack integer; item_release_status text;
        character_deleted timestamptz;
BEGIN
    IF TG_OP = 'DELETE' THEN
        PERFORM 1 FROM player_characters WHERE id = OLD.character_id FOR UPDATE;
        IF EXISTS (SELECT 1 FROM character_equipment WHERE owned_item_id = OLD.id) THEN
            RAISE EXCEPTION 'unequip item before deleting it';
        END IF;
        RETURN OLD;
    END IF;
    SELECT deleted_at INTO character_deleted FROM player_characters
        WHERE id = NEW.character_id FOR UPDATE;
    IF NOT FOUND OR character_deleted IS NOT NULL THEN
        RAISE EXCEPTION 'owned item requires an active character';
    END IF;
    IF TG_OP = 'INSERT' THEN
        IF NEW.row_revision <> 1 THEN RAISE EXCEPTION 'new item revision must be one'; END IF;
    ELSE
        IF NEW.id IS DISTINCT FROM OLD.id OR
            NEW.character_id IS DISTINCT FROM OLD.character_id OR
            NEW.item_definition_id IS DISTINCT FROM OLD.item_definition_id OR
            NEW.created_at IS DISTINCT FROM OLD.created_at OR
            NEW.row_revision <> OLD.row_revision + 1 THEN
            RAISE EXCEPTION 'invalid owned-item update or revision';
        END IF;
    END IF;
    SELECT i.stackable, i.max_stack, r.status
        INTO item_stackable, item_max_stack, item_release_status
        FROM item_definitions i JOIN catalog_releases r ON r.id = i.catalog_release_id
        WHERE i.id = NEW.item_definition_id;
    IF NOT FOUND OR NEW.quantity < 1 OR NEW.quantity > item_max_stack OR
        (NOT item_stackable AND NEW.quantity <> 1) THEN
        RAISE EXCEPTION 'owned-item quantity violates its definition';
    END IF;
    IF TG_OP = 'INSERT' AND item_release_status <> 'published' THEN
        RAISE EXCEPTION 'new owned item must use a published definition';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER owned_item_mutation_guard
    BEFORE INSERT OR UPDATE OR DELETE ON owned_items
    FOR EACH ROW EXECUTE FUNCTION owned_item_guard_mutation();

CREATE FUNCTION equipment_guard_mutation() RETURNS trigger LANGUAGE plpgsql
    SET search_path = public, pg_catalog AS $$
DECLARE definition item_definitions%ROWTYPE; item owned_items%ROWTYPE;
        character_deleted timestamptz; other_hands smallint;
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.character_id IS DISTINCT FROM OLD.character_id OR
            NEW.slot_code IS DISTINCT FROM OLD.slot_code THEN
            RAISE EXCEPTION 'equipment identity cannot change';
        END IF;
    END IF;
    SELECT deleted_at INTO character_deleted FROM player_characters
        WHERE id = NEW.character_id FOR UPDATE;
    IF NOT FOUND OR character_deleted IS NOT NULL THEN
        RAISE EXCEPTION 'equipment requires an active character';
    END IF;
    SELECT * INTO item FROM owned_items
        WHERE id = NEW.owned_item_id AND character_id = NEW.character_id;
    IF NOT FOUND THEN RAISE EXCEPTION 'equipment must be owned by character'; END IF;
    SELECT * INTO definition FROM item_definitions WHERE id = item.item_definition_id;
    IF NOT FOUND OR definition.stackable OR item.quantity <> 1 OR
        definition.category NOT IN ('weapon','armor') OR definition.equip_slot <> NEW.slot_code THEN
        RAISE EXCEPTION 'item cannot be equipped in requested slot';
    END IF;
    IF NEW.slot_code = 'main_hand' AND definition.hand_requirement = 2 AND
        EXISTS (SELECT 1 FROM character_equipment
            WHERE character_id = NEW.character_id AND slot_code = 'off_hand') THEN
        RAISE EXCEPTION 'two-handed weapon excludes off-hand equipment';
    END IF;
    IF NEW.slot_code = 'off_hand' THEN
        SELECT d.hand_requirement INTO other_hands FROM character_equipment e
            JOIN owned_items o ON o.id = e.owned_item_id
            JOIN item_definitions d ON d.id = o.item_definition_id
            WHERE e.character_id = NEW.character_id AND e.slot_code = 'main_hand';
        IF other_hands = 2 THEN RAISE EXCEPTION 'off-hand excludes two-handed weapon'; END IF;
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER equipment_mutation_guard
    BEFORE INSERT OR UPDATE ON character_equipment
    FOR EACH ROW EXECUTE FUNCTION equipment_guard_mutation();
COMMENT ON TABLE accounts IS 'Ashenfall 09B.1 character persistence';
COMMENT ON TABLE player_characters IS 'Ashenfall 09B.1 character persistence';
COMMENT ON TABLE class_level_rules IS 'Ashenfall 09B.1 catalog progression extension';
COMMENT ON TABLE item_stat_modifiers IS 'Ashenfall 09B.1 catalog item extension';
COMMENT ON TABLE owned_items IS 'Ashenfall 09B.1 character inventory';
COMMENT ON TABLE character_equipment IS 'Ashenfall 09B.1 character equipment';
REVOKE ALL ON accounts, player_characters, class_level_rules,
    item_stat_modifiers, owned_items, character_equipment FROM PUBLIC;
REVOKE ALL ON FUNCTION catalog_guard_derived_definition(),
    character_guard_mutation(), owned_item_guard_mutation(),
    equipment_guard_mutation() FROM PUBLIC;
-- Runtime role requires only row access; catalog grants remain SELECT-only.
GRANT SELECT, INSERT ON accounts TO ashenfall_app;
GRANT SELECT, INSERT, UPDATE ON player_characters, owned_items, character_equipment
    TO ashenfall_app;
GRANT SELECT ON class_level_rules, item_stat_modifiers TO ashenfall_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON class_level_rules, item_stat_modifiers
    TO ashenfall_publisher;
GRANT DELETE ON character_equipment TO ashenfall_app;
GRANT DELETE ON owned_items TO ashenfall_app;
COMMIT;
