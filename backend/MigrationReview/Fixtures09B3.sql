\set ON_ERROR_STOP on
BEGIN;
DO $$ BEGIN IF current_database() <> 'ashenfall_09b3_test' THEN RAISE EXCEPTION 'wrong database'; END IF; END $$;
GRANT USAGE ON SCHEMA public TO ashenfall_app;
INSERT INTO catalog_releases(id,version_key,schema_major,status) VALUES('90000000-0000-0000-0000-000000000001','prototype-1',1,'draft');
INSERT INTO character_classes(id,catalog_release_id,code,display_name,visual_key,base_max_health,fixed_damage,move_speed_mps,melee_range_m,attack_cycle_ms,impact_ms)
VALUES('90000000-0000-0000-0000-000000000002','90000000-0000-0000-0000-000000000001','test_class','Test class','test',100,10,1,1,1000,500);
INSERT INTO item_definitions(id,catalog_release_id,code,display_name,category,rarity,stackable,max_stack,equip_slot,hand_requirement) VALUES
('90000000-0000-0000-0000-000000000011','90000000-0000-0000-0000-000000000001','test_one_hand','One hand','weapon','common',false,1,'main_hand',1),
('90000000-0000-0000-0000-000000000012','90000000-0000-0000-0000-000000000001','test_two_hand','Two hand','weapon','common',false,1,'main_hand',2),
('90000000-0000-0000-0000-000000000013','90000000-0000-0000-0000-000000000001','test_off_hand','Off hand','armor','common',false,1,'off_hand',null),
('90000000-0000-0000-0000-000000000014','90000000-0000-0000-0000-000000000001','test_helmet','Helmet','armor','common',false,1,'head',null),
('90000000-0000-0000-0000-000000000015','90000000-0000-0000-0000-000000000001','test_stack','Stack','consumable','common',true,5,null,null);
-- Synthetic test marker only: not the approved prototype hash or production content.
UPDATE catalog_releases SET status='published',published_at=now(),content_sha256=repeat('0',64) WHERE version_key='prototype-1';
COMMIT;
