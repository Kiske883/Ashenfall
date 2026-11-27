-- Test-only conditional rollback; refuses to erase authored V2 data.
\set ON_ERROR_STOP on
BEGIN;
DO $$ BEGIN
  IF current_database() <> 'ashenfall_09b3_test' OR current_user <> 'postgres'
     OR inet_server_port() IS DISTINCT FROM 5432 OR inet_server_addr() IS NULL
     OR NOT (inet_server_addr() <<= '127.0.0.0/8'::inet OR inet_server_addr()='::1'::inet)
     OR (SELECT system_identifier::text FROM pg_control_system()) <> '7708337984335018212'
  THEN RAISE EXCEPTION 'wrong isolated test database or cluster'; END IF;
  IF EXISTS (SELECT 1 FROM character_classes WHERE min_damage IS NOT NULL OR max_damage IS NOT NULL OR critical_chance_percent IS NOT NULL OR critical_damage_bonus_percent IS NOT NULL)
     OR EXISTS (SELECT 1 FROM class_level_rules WHERE min_damage_bonus IS NOT NULL OR max_damage_bonus IS NOT NULL OR critical_chance_bonus_percent IS NOT NULL OR critical_damage_bonus_percent IS NOT NULL)
     OR EXISTS (SELECT 1 FROM item_stat_modifiers WHERE stat_code IN ('min_damage','max_damage'))
  THEN RAISE EXCEPTION 'authored V2 data exists; rollback would destroy it'; END IF;
END $$;
ALTER TABLE public.item_stat_modifiers DROP CONSTRAINT item_stat_modifiers_stat_code_check;
ALTER TABLE public.item_stat_modifiers ADD CONSTRAINT item_stat_modifiers_stat_code_check CHECK (stat_code IN
 ('max_health','fixed_damage','strength','dexterity','vitality','armor','attack_speed','critical_chance','critical_multiplier'));
ALTER TABLE public.class_level_rules DROP CONSTRAINT ck_combat_level_v2,
  DROP COLUMN min_damage_bonus, DROP COLUMN max_damage_bonus, DROP COLUMN critical_chance_bonus_percent, DROP COLUMN critical_damage_bonus_percent;
ALTER TABLE public.character_classes DROP CONSTRAINT ck_combat_class_v2,
  DROP COLUMN min_damage, DROP COLUMN max_damage, DROP COLUMN critical_chance_percent, DROP COLUMN critical_damage_bonus_percent;
COMMIT;
