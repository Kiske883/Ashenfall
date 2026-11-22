-- VALIDATION ONLY. Run as ashenfall_app; every attempted mutation rolls back.
\set ON_ERROR_STOP on
BEGIN;
SELECT current_user, current_database();
SELECT (SELECT count(*) FROM catalog_releases) AS releases,
       (SELECT count(*) FROM character_classes) AS classes,
       (SELECT count(*) FROM enemy_definitions) AS enemies,
       (SELECT count(*) FROM item_definitions) AS items;
DO $test$
DECLARE
    rejected boolean;
BEGIN
    rejected := false;
    BEGIN
        INSERT INTO catalog_releases (id, version_key, schema_major, status)
            VALUES (gen_random_uuid(), 'permission-probe', 1, 'draft');
    EXCEPTION WHEN insufficient_privilege THEN rejected := true;
    END;
    IF NOT rejected THEN RAISE EXCEPTION 'INSERT permission unexpectedly granted'; END IF;

    rejected := false;
    BEGIN UPDATE catalog_releases SET status='draft' WHERE false;
    EXCEPTION WHEN insufficient_privilege THEN rejected := true; END;
    IF NOT rejected THEN RAISE EXCEPTION 'UPDATE permission unexpectedly granted'; END IF;

    rejected := false;
    BEGIN DELETE FROM catalog_releases WHERE false;
    EXCEPTION WHEN insufficient_privilege THEN rejected := true; END;
    IF NOT rejected THEN RAISE EXCEPTION 'DELETE permission unexpectedly granted'; END IF;

    rejected := false;
    BEGIN TRUNCATE catalog_releases;
    EXCEPTION WHEN insufficient_privilege THEN rejected := true; END;
    IF NOT rejected THEN RAISE EXCEPTION 'TRUNCATE permission unexpectedly granted'; END IF;

    rejected := false;
    BEGIN CREATE TABLE public.permission_probe (id integer);
    EXCEPTION WHEN insufficient_privilege THEN rejected := true; END;
    IF NOT rejected THEN RAISE EXCEPTION 'schema CREATE permission unexpectedly granted'; END IF;

    rejected := false;
    BEGIN CREATE TEMP TABLE permission_probe_temp (id integer);
    EXCEPTION WHEN insufficient_privilege THEN rejected := true; END;
    IF NOT rejected THEN RAISE EXCEPTION 'TEMP permission unexpectedly granted'; END IF;

    RAISE NOTICE 'runtime SELECT works; INSERT/UPDATE/DELETE/TRUNCATE/DDL/TEMP denied';
END
$test$;
ROLLBACK;
