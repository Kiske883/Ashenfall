-- Live PostgreSQL 09B.2 checks. All test rows are rolled back.
\set ON_ERROR_STOP on
BEGIN;
SET LOCAL statement_timeout = '30s';
SET LOCAL search_path = public, pg_catalog;
DO $test$
DECLARE
    a uuid := gen_random_uuid(); b uuid := gen_random_uuid();
    c uuid := gen_random_uuid(); d uuid := gen_random_uuid();
    axe uuid := gen_random_uuid(); sword uuid := gen_random_uuid();
    class_id uuid; axe_id uuid; sword_id uuid;
    caught boolean;
BEGIN
    SELECT id INTO class_id FROM character_classes WHERE code='dwarf_warrior';
    SELECT id INTO axe_id FROM item_definitions WHERE code='training_battle_axe';
    SELECT id INTO sword_id FROM item_definitions WHERE code='guardian_sword';
    IF class_id IS NULL OR axe_id IS NULL OR sword_id IS NULL THEN RAISE EXCEPTION 'seed missing'; END IF;
    INSERT INTO accounts(id,identity_provider,provider_subject) VALUES
        (a,'09b2_disposable_test',a::text),(b,'09b2_disposable_test',b::text);
    INSERT INTO player_characters(id,account_id,class_definition_id,character_name)
        VALUES(c,a,class_id,'Test Dwarf'),(d,b,class_id,'Test Rival');
    IF (SELECT row_revision FROM player_characters WHERE id=c) <> 1 THEN RAISE EXCEPTION 'create revision'; END IF;
    UPDATE player_characters SET experience=12,row_revision=2 WHERE id=c AND row_revision=1;
    IF NOT FOUND OR (SELECT experience FROM player_characters WHERE id=c)<>12 THEN RAISE EXCEPTION 'save failed'; END IF;
    UPDATE player_characters SET experience=20,row_revision=3 WHERE id=c AND row_revision=1;
    IF FOUND THEN RAISE EXCEPTION 'stale revision accepted'; END IF;
    caught:=false;
    BEGIN
      INSERT INTO player_characters(id,account_id,class_definition_id,character_name)
        VALUES(gen_random_uuid(),a,class_id,'Duplicate');
    EXCEPTION WHEN unique_violation THEN caught:=true; END;
    IF NOT caught THEN RAISE EXCEPTION 'duplicate active character accepted'; END IF;
    INSERT INTO owned_items(id,character_id,item_definition_id,quantity) VALUES
        (axe,c,axe_id,1),(sword,d,sword_id,1);
    caught:=false;
    BEGIN
      INSERT INTO owned_items(id,character_id,item_definition_id,quantity)
        VALUES(gen_random_uuid(),c,axe_id,2);
    EXCEPTION WHEN OTHERS THEN caught:=true; END;
    IF NOT caught THEN RAISE EXCEPTION 'over-stack accepted'; END IF;
    caught:=false;
    BEGIN
      UPDATE owned_items SET quantity=0,row_revision=2 WHERE id=axe;
    EXCEPTION WHEN OTHERS THEN caught:=true; END;
    IF NOT caught THEN RAISE EXCEPTION 'zero quantity accepted'; END IF;
    INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES(c,'main_hand',axe);
    caught:=false;
    BEGIN
      DELETE FROM owned_items WHERE id=axe;
    EXCEPTION WHEN OTHERS THEN caught:=true; END;
    IF NOT caught THEN RAISE EXCEPTION 'equipped item deleted'; END IF;
    caught:=false;
    BEGIN
      INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES(c,'off_hand',sword);
    EXCEPTION WHEN OTHERS THEN caught:=true; END;
    IF NOT caught THEN RAISE EXCEPTION 'foreign item equipped'; END IF;
    caught:=false;
    BEGIN
      INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES(c,'head',axe);
    EXCEPTION WHEN OTHERS THEN caught:=true; END;
    IF NOT caught THEN RAISE EXCEPTION 'incompatible slot accepted'; END IF;
    caught:=false;
    BEGIN
      INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES(c,'off_hand',axe);
    EXCEPTION WHEN OTHERS THEN caught:=true; END;
    IF NOT caught THEN RAISE EXCEPTION 'duplicate equipment accepted'; END IF;
    DELETE FROM character_equipment WHERE character_id=c AND slot_code='main_hand';
    DELETE FROM owned_items WHERE id=axe;
    UPDATE player_characters SET deleted_at=now(),row_revision=3 WHERE id=c AND row_revision=2;
    INSERT INTO player_characters(id,account_id,class_definition_id,character_name)
      VALUES(gen_random_uuid(),a,class_id,'Replacement');
    caught:=false;
    BEGIN
      INSERT INTO class_level_rules(class_definition_id,level,required_total_xp) VALUES(class_id,1,0);
    EXCEPTION WHEN OTHERS THEN caught:=true; END;
    IF NOT caught THEN RAISE EXCEPTION 'published extension modified'; END IF;
    RAISE NOTICE '09B.2 transaction-local lifecycle, revision, inventory, equipment and catalog guard checks passed';
END;
$test$;
ROLLBACK;
