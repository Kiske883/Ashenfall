-- Ashenfall 09B.1 EMPTY-DATA rollback proposal. REVIEW ONLY; NEVER RUN AUTOMATICALLY.
-- This procedure refuses to remove character, inventory or authored extension rows.
\set ON_ERROR_STOP on
BEGIN;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SET LOCAL search_path = public, pg_catalog;
DO $preflight$
BEGIN
    IF current_database() <> 'ashenfall_dev' OR
        to_regclass('public.accounts') IS NULL OR
        to_regclass('public.player_characters') IS NULL OR
        to_regclass('public.class_level_rules') IS NULL OR
        to_regclass('public.item_stat_modifiers') IS NULL OR
        to_regclass('public.owned_items') IS NULL OR
        to_regclass('public.character_equipment') IS NULL OR
        to_regprocedure('public.catalog_guard_derived_definition()') IS NULL OR
        to_regprocedure('public.character_guard_mutation()') IS NULL OR
        to_regprocedure('public.owned_item_guard_mutation()') IS NULL OR
        to_regprocedure('public.equipment_guard_mutation()') IS NULL THEN
        RAISE EXCEPTION '09B.1 rollback prerequisites not met';
    END IF;
END;
$preflight$;
-- Parent tables first, then dependent inventory/equipment and authored extensions.
-- DROP needs ACCESS EXCLUSIVE anyway; taking it now avoids a later lock upgrade.
-- PostgreSQL keeps these locks until COMMIT/ROLLBACK. A timeout aborts the script.
LOCK TABLE accounts, player_characters, owned_items, character_equipment,
    class_level_rules, item_stat_modifiers IN ACCESS EXCLUSIVE MODE;
DO $empty_data$
BEGIN
    IF EXISTS (SELECT 1 FROM accounts) OR
        EXISTS (SELECT 1 FROM player_characters) OR
        EXISTS (SELECT 1 FROM class_level_rules) OR
        EXISTS (SELECT 1 FROM item_stat_modifiers) OR
        EXISTS (SELECT 1 FROM owned_items) OR
        EXISTS (SELECT 1 FROM character_equipment) THEN
        RAISE EXCEPTION '09B.1 rollback refused: persistent or authored data exists';
    END IF;
END;
$empty_data$;
DROP TABLE character_equipment;
DROP TABLE owned_items;
DROP TABLE player_characters;
DROP TABLE class_level_rules;
DROP TABLE item_stat_modifiers;
DROP TABLE accounts;
DROP FUNCTION equipment_guard_mutation();
DROP FUNCTION owned_item_guard_mutation();
DROP FUNCTION character_guard_mutation();
DROP FUNCTION catalog_guard_derived_definition();
COMMIT;
