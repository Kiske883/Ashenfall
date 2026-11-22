-- VALIDATION ONLY. All test rows and attempted mutations roll back.
\set ON_ERROR_STOP on
SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory";
BEGIN;
DO $test$
DECLARE
    r1 uuid := 'f42d46b6-80b6-4a09-89bb-eef61d7ef001';
    r2 uuid := 'f42d46b6-80b6-4a09-89bb-eef61d7ef002';
    c1 uuid := 'f42d46b6-80b6-4a09-89bb-eef61d7ef003';
    e1 uuid := 'f42d46b6-80b6-4a09-89bb-eef61d7ef004';
    i1 uuid := 'f42d46b6-80b6-4a09-89bb-eef61d7ef005';
    rejected boolean;
BEGIN
    rejected := false;
    BEGIN
        INSERT INTO catalog_releases (id, version_key, schema_major, status,
            published_at, content_sha256)
            VALUES (gen_random_uuid(), 'invalid-direct-publish', 1, 'published',
                now(), repeat('a',64));
    EXCEPTION WHEN SQLSTATE 'P0001' THEN rejected := true;
    END;
    IF NOT rejected THEN RAISE EXCEPTION 'non-draft release insert accepted'; END IF;

    INSERT INTO catalog_releases (id, version_key, schema_major, status)
        VALUES (r1, 'validation-only-1', 1, 'draft');

    rejected := false;
    BEGIN
        INSERT INTO character_classes (id, catalog_release_id, code, display_name, visual_key,
            base_max_health, fixed_damage, move_speed_mps, melee_range_m, attack_cycle_ms, impact_ms)
            VALUES (gen_random_uuid(), gen_random_uuid(), 'bad_fk', 'Bad FK', 'visual',
                100, 25, 2.5, 1.45, 1700, 940);
    -- The catalog guard runs before the FK check and rejects an unknown parent.
    EXCEPTION WHEN SQLSTATE 'P0001' THEN rejected := true;
    END;
    IF NOT rejected THEN RAISE EXCEPTION 'invalid release FK accepted'; END IF;

    INSERT INTO character_classes (id, catalog_release_id, code, display_name, visual_key,
        base_max_health, fixed_damage, move_speed_mps, melee_range_m, attack_cycle_ms, impact_ms)
        VALUES (c1, r1, 'test_class', 'Test Class', 'test_visual', 100, 25, 2.5, 1.45, 1700, 940);
    INSERT INTO enemy_definitions (id, catalog_release_id, code, display_name, visual_key,
        max_health, fixed_damage, attack_range_m, windup_ms, recovery_ms,
        impact_after_execute_ms, chase_speed_mps, return_speed_mps, detection_radius_m,
        disengagement_radius_m, max_spawn_distance_m, stopping_distance_m, resume_distance_m)
        VALUES (e1, r1, 'test_enemy', 'Test Enemy', 'test_enemy_visual',
            100, 10, 1.45, 600, 1000, 100, 1.0, 0.7, 6, 10, 12, 1.3, 1.7);
    INSERT INTO item_definitions (id, catalog_release_id, code, display_name, category,
        rarity, stackable, max_stack, equip_slot, hand_requirement, visual_key)
        VALUES (i1, r1, 'test_axe', 'Test Axe', 'weapon',
            'common', false, 1, 'main_hand', 2, 'test_axe_visual');
    UPDATE character_classes SET fixed_damage=26 WHERE id=c1;
    UPDATE enemy_definitions SET fixed_damage=11 WHERE id=e1;
    UPDATE item_definitions SET display_name='Edited Draft Axe' WHERE id=i1;
    IF (SELECT fixed_damage FROM character_classes WHERE id=c1) <> 26 THEN
        RAISE EXCEPTION 'draft class update failed';
    END IF;

    rejected := false;
    BEGIN
        INSERT INTO item_definitions (id, catalog_release_id, code, display_name, category,
            rarity, stackable, max_stack, equip_slot, hand_requirement)
            VALUES (gen_random_uuid(), r1, 'test_axe', 'Duplicate', 'weapon',
                'common', false, 1, 'main_hand', 2);
    EXCEPTION WHEN unique_violation THEN rejected := true;
    END;
    IF NOT rejected THEN RAISE EXCEPTION 'duplicate catalog code accepted'; END IF;

    rejected := false;
    BEGIN
        INSERT INTO item_definitions (id, catalog_release_id, code, display_name, category,
            rarity, stackable, max_stack, equip_slot, hand_requirement)
            VALUES (gen_random_uuid(), r1, 'invalid_hands', 'Invalid', 'weapon',
                'common', false, 1, 'off_hand', 2);
    EXCEPTION WHEN check_violation THEN rejected := true;
    END;
    IF NOT rejected THEN RAISE EXCEPTION 'two-handed off-hand item accepted'; END IF;

    rejected := false;
    BEGIN
        UPDATE character_classes SET fixed_damage=-1 WHERE id=c1;
    EXCEPTION WHEN check_violation THEN rejected := true;
    END;
    IF NOT rejected THEN RAISE EXCEPTION 'negative damage accepted'; END IF;

    rejected := false;
    BEGIN
        UPDATE catalog_releases SET status='published', published_at=now() WHERE id=r1;
    EXCEPTION WHEN check_violation THEN rejected := true;
    END;
    IF NOT rejected THEN RAISE EXCEPTION 'publication without hash accepted'; END IF;

    UPDATE catalog_releases SET status='published', published_at=now(),
        content_sha256=repeat('a',64) WHERE id=r1;

    rejected := false;
    BEGIN UPDATE character_classes SET fixed_damage=27 WHERE id=c1;
    EXCEPTION WHEN SQLSTATE 'P0001' THEN rejected := true; END;
    IF NOT rejected THEN RAISE EXCEPTION 'published class update accepted'; END IF;

    rejected := false;
    BEGIN UPDATE enemy_definitions SET fixed_damage=12 WHERE id=e1;
    EXCEPTION WHEN SQLSTATE 'P0001' THEN rejected := true; END;
    IF NOT rejected THEN RAISE EXCEPTION 'published enemy update accepted'; END IF;

    rejected := false;
    BEGIN UPDATE item_definitions SET display_name='Changed' WHERE id=i1;
    EXCEPTION WHEN SQLSTATE 'P0001' THEN rejected := true; END;
    IF NOT rejected THEN RAISE EXCEPTION 'published item update accepted'; END IF;

    rejected := false;
    BEGIN
        INSERT INTO item_definitions (id, catalog_release_id, code, display_name, category,
            rarity, stackable, max_stack, equip_slot, hand_requirement)
            VALUES (gen_random_uuid(), r1, 'late_item', 'Late Item', 'weapon',
                'common', false, 1, 'main_hand', 1);
    EXCEPTION WHEN SQLSTATE 'P0001' THEN rejected := true;
    END;
    IF NOT rejected THEN RAISE EXCEPTION 'published definition insert accepted'; END IF;

    rejected := false;
    BEGIN DELETE FROM item_definitions WHERE id=i1;
    EXCEPTION WHEN SQLSTATE 'P0001' THEN rejected := true; END;
    IF NOT rejected THEN RAISE EXCEPTION 'published item delete accepted'; END IF;

    rejected := false;
    BEGIN UPDATE catalog_releases SET content_sha256=repeat('b',64) WHERE id=r1;
    EXCEPTION WHEN SQLSTATE 'P0001' THEN rejected := true; END;
    IF NOT rejected THEN RAISE EXCEPTION 'published hash update accepted'; END IF;

    INSERT INTO catalog_releases (id, version_key, schema_major, status)
        VALUES (r2, 'validation-only-2', 1, 'draft');
    rejected := false;
    BEGIN UPDATE item_definitions SET catalog_release_id=r2 WHERE id=i1;
    EXCEPTION WHEN SQLSTATE 'P0001' THEN rejected := true; END;
    IF NOT rejected THEN RAISE EXCEPTION 'definition release reassignment accepted'; END IF;
    rejected := false;
    BEGIN
        UPDATE catalog_releases SET status='published', published_at=now(),
            content_sha256=repeat('c',64) WHERE id=r2;
    EXCEPTION WHEN unique_violation THEN rejected := true;
    END;
    IF NOT rejected THEN RAISE EXCEPTION 'second published release accepted'; END IF;

    rejected := false;
    BEGIN
        UPDATE catalog_releases SET status='retired',
            published_at=published_at + interval '1 second' WHERE id=r1;
    EXCEPTION WHEN SQLSTATE 'P0001' THEN rejected := true;
    END;
    IF NOT rejected THEN RAISE EXCEPTION 'retirement with changed timestamp accepted'; END IF;

    UPDATE catalog_releases SET status='retired' WHERE id=r1;
    rejected := false;
    BEGIN UPDATE catalog_releases SET published_at=now() WHERE id=r1;
    EXCEPTION WHEN SQLSTATE 'P0001' THEN rejected := true; END;
    IF NOT rejected THEN RAISE EXCEPTION 'retired publication timestamp mutation accepted'; END IF;
    rejected := false;
    BEGIN UPDATE character_classes SET fixed_damage=27 WHERE id=c1;
    EXCEPTION WHEN SQLSTATE 'P0001' THEN rejected := true; END;
    IF NOT rejected THEN RAISE EXCEPTION 'retired definition update accepted'; END IF;

    rejected := false;
    BEGIN UPDATE catalog_releases SET status='published' WHERE id=r1;
    EXCEPTION WHEN SQLSTATE 'P0001' THEN rejected := true; END;
    IF NOT rejected THEN RAISE EXCEPTION 'retired release republished'; END IF;

    RAISE NOTICE 'draft, constraint, publication, immutability and retirement checks passed';
END
$test$;
ROLLBACK;
SELECT 'test_rows_after_rollback' AS check_name,
    (SELECT count(*) FROM catalog_releases WHERE version_key LIKE 'validation-only-%') AS release_count,
    (SELECT count(*) FROM character_classes WHERE code='test_class') AS class_count,
    (SELECT count(*) FROM enemy_definitions WHERE code='test_enemy') AS enemy_count,
    (SELECT count(*) FROM item_definitions WHERE code='test_axe') AS item_count;
