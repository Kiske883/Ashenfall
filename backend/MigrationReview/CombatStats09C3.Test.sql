-- Test-only additive schema migration. Never target another database.
\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
DO $$ BEGIN
  IF current_database() <> 'ashenfall_09b3_test' OR current_user <> 'postgres'
     OR inet_server_port() IS DISTINCT FROM 5432 OR inet_server_addr() IS NULL
     OR NOT (inet_server_addr() <<= '127.0.0.0/8'::inet OR inet_server_addr()='::1'::inet)
     OR (SELECT system_identifier::text FROM pg_control_system()) <> '7708337984335018212'
  THEN RAISE EXCEPTION 'wrong isolated test database or cluster'; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public.item_stat_modifiers'::regclass AND conname='item_stat_modifiers_stat_code_check')
     OR EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='character_classes' AND column_name='min_damage')
  THEN RAISE EXCEPTION 'unexpected pre-migration schema'; END IF;
END $$;
ALTER TABLE public.character_classes
  ADD COLUMN min_damage integer,
  ADD COLUMN max_damage integer,
  ADD COLUMN critical_chance_percent numeric,
  ADD COLUMN critical_damage_bonus_percent numeric,
  ADD CONSTRAINT ck_combat_class_v2 CHECK (
    (min_damage IS NULL AND max_damage IS NULL AND critical_chance_percent IS NULL AND critical_damage_bonus_percent IS NULL)
    OR (min_damage IS NOT NULL AND max_damage IS NOT NULL AND critical_chance_percent IS NOT NULL AND critical_damage_bonus_percent IS NOT NULL
      AND min_damage >= 0 AND max_damage >= min_damage AND max_damage < 2147483647
      AND critical_chance_percent BETWEEN 0 AND 100 AND critical_damage_bonus_percent >= 0 AND critical_damage_bonus_percent < 'Infinity'::numeric));
ALTER TABLE public.class_level_rules
  ADD COLUMN min_damage_bonus integer,
  ADD COLUMN max_damage_bonus integer,
  ADD COLUMN critical_chance_bonus_percent numeric,
  ADD COLUMN critical_damage_bonus_percent numeric,
  ADD CONSTRAINT ck_combat_level_v2 CHECK (
    (min_damage_bonus IS NULL AND max_damage_bonus IS NULL AND critical_chance_bonus_percent IS NULL AND critical_damage_bonus_percent IS NULL)
    OR (min_damage_bonus IS NOT NULL AND max_damage_bonus IS NOT NULL AND critical_chance_bonus_percent IS NOT NULL AND critical_damage_bonus_percent IS NOT NULL
      AND min_damage_bonus >= 0 AND max_damage_bonus >= 0 AND critical_chance_bonus_percent >= 0 AND critical_chance_bonus_percent < 'Infinity'::numeric AND critical_damage_bonus_percent >= 0 AND critical_damage_bonus_percent < 'Infinity'::numeric));
ALTER TABLE public.item_stat_modifiers DROP CONSTRAINT item_stat_modifiers_stat_code_check;
ALTER TABLE public.item_stat_modifiers ADD CONSTRAINT item_stat_modifiers_stat_code_check CHECK (stat_code IN
 ('max_health','fixed_damage','strength','dexterity','vitality','armor','attack_speed','critical_chance','critical_multiplier','min_damage','max_damage'));
COMMIT;
