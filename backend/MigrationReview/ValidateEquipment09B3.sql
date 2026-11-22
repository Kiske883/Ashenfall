\set ON_ERROR_STOP on
BEGIN;
DO $test$
DECLARE a uuid:=gen_random_uuid(); b uuid:=gen_random_uuid(); c uuid:=gen_random_uuid(); d uuid:=gen_random_uuid(); twoh uuid:=gen_random_uuid(); oneh uuid:=gen_random_uuid(); offh uuid:=gen_random_uuid(); stack uuid:=gen_random_uuid(); foreignitem uuid:=gen_random_uuid(); caught boolean;
BEGIN
 IF current_database()<>'ashenfall_09b3_test' THEN RAISE EXCEPTION 'wrong database'; END IF;
 INSERT INTO accounts(id,identity_provider,provider_subject) VALUES(a,'test',a::text),(b,'test',b::text);
 INSERT INTO player_characters(id,account_id,class_definition_id,character_name) VALUES(c,a,'90000000-0000-0000-0000-000000000002','Test One'),(d,b,'90000000-0000-0000-0000-000000000002','Test Two');
 INSERT INTO owned_items(id,character_id,item_definition_id,quantity) VALUES
 (twoh,c,'90000000-0000-0000-0000-000000000012',1),(oneh,c,'90000000-0000-0000-0000-000000000011',1),
 (offh,c,'90000000-0000-0000-0000-000000000013',1),(stack,c,'90000000-0000-0000-0000-000000000015',2),
 (foreignitem,d,'90000000-0000-0000-0000-000000000013',1);
 INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES(c,'main_hand',twoh);
 caught:=false; BEGIN INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES(c,'off_hand',offh); EXCEPTION WHEN raise_exception THEN caught:=true; END;
 IF NOT caught OR (SELECT count(*) FROM character_equipment WHERE character_id=c)<>1 THEN RAISE EXCEPTION 'two-hand first failed'; END IF;
 DELETE FROM character_equipment WHERE character_id=c;
 INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES(c,'off_hand',offh);
 caught:=false; BEGIN INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES(c,'main_hand',twoh); EXCEPTION WHEN raise_exception THEN caught:=true; END;
 IF NOT caught OR (SELECT count(*) FROM character_equipment WHERE character_id=c)<>1 THEN RAISE EXCEPTION 'off-hand first failed'; END IF;
 INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES(c,'main_hand',oneh);
 IF (SELECT count(*) FROM character_equipment WHERE character_id=c)<>2 THEN RAISE EXCEPTION 'one hand and off hand failed'; END IF;
 DELETE FROM character_equipment WHERE character_id=c AND slot_code='main_hand';
 INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES(c,'main_hand',oneh);
 caught:=false; BEGIN INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES(c,'head',foreignitem); EXCEPTION WHEN raise_exception OR foreign_key_violation OR unique_violation THEN caught:=true; END;
 IF NOT caught THEN RAISE EXCEPTION 'foreign item accepted'; END IF;
 caught:=false; BEGIN INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES(c,'head',stack); EXCEPTION WHEN raise_exception THEN caught:=true; END;
 IF NOT caught THEN RAISE EXCEPTION 'stack accepted'; END IF;
 caught:=false; BEGIN INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES(c,'head',oneh); EXCEPTION WHEN raise_exception OR unique_violation THEN caught:=true; END;
 IF NOT caught THEN RAISE EXCEPTION 'duplicate item accepted'; END IF;
 IF (SELECT row_revision FROM player_characters WHERE id=c)<>1 OR (SELECT count(*) FROM owned_items WHERE character_id=c)<>4 THEN RAISE EXCEPTION 'failed operation changed inventory or revision'; END IF;
 RAISE NOTICE 'Equipment order, ownership, slots, stack, inventory and revision checks passed';
END $test$;
ROLLBACK;
