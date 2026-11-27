-- Test-only historical item mapping. Does not rewrite inventory or equipment.
\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout='5s';
DO $$ BEGIN
 IF current_database()<>'ashenfall_09b3_test' OR current_user<>'postgres' OR inet_server_port() IS DISTINCT FROM 5432
    OR inet_server_addr() IS NULL OR NOT (inet_server_addr() <<= '127.0.0.0/8'::inet OR inet_server_addr()='::1'::inet)
    OR (SELECT system_identifier::text FROM pg_control_system())<>'7708337984335018212'
 THEN RAISE EXCEPTION 'wrong test database or cluster'; END IF;
 IF to_regclass('public.catalog_item_successors') IS NOT NULL THEN RAISE EXCEPTION 'item mapping already exists'; END IF;
END $$;
CREATE TABLE public.catalog_item_successors (
 source_item_id uuid NOT NULL REFERENCES public.item_definitions(id) ON DELETE RESTRICT,
 target_item_id uuid NOT NULL REFERENCES public.item_definitions(id) ON DELETE RESTRICT,
 PRIMARY KEY (source_item_id,target_item_id), CHECK (source_item_id<>target_item_id)
);
CREATE FUNCTION public.catalog_guard_item_successor() RETURNS trigger LANGUAGE plpgsql SET search_path=public,pg_catalog AS $$
DECLARE source_id uuid; target_id uuid; source_item item_definitions%ROWTYPE; target_item item_definitions%ROWTYPE; source_status text; target_status text;
BEGIN
 IF TG_OP='UPDATE' THEN RAISE EXCEPTION 'replace a draft successor instead of updating it'; END IF;
 IF TG_OP='DELETE' THEN source_id=OLD.source_item_id; target_id=OLD.target_item_id; ELSE source_id=NEW.source_item_id; target_id=NEW.target_item_id; END IF;
 SELECT * INTO source_item FROM item_definitions WHERE id=source_id;
 SELECT * INTO target_item FROM item_definitions WHERE id=target_id;
 SELECT status INTO source_status FROM catalog_releases WHERE id=source_item.catalog_release_id FOR UPDATE;
 SELECT status INTO target_status FROM catalog_releases WHERE id=target_item.catalog_release_id FOR UPDATE;
 IF source_item.id IS NULL OR target_item.id IS NULL OR source_item.catalog_release_id=target_item.catalog_release_id OR
    source_status NOT IN ('published','retired') OR target_status IS DISTINCT FROM 'draft' OR
    source_item.code<>target_item.code OR source_item.category<>target_item.category OR
    source_item.equip_slot IS DISTINCT FROM target_item.equip_slot OR source_item.hand_requirement IS DISTINCT FROM target_item.hand_requirement OR
    source_item.stackable IS DISTINCT FROM target_item.stackable
 THEN RAISE EXCEPTION 'invalid item successor or immutable published mapping'; END IF;
 IF TG_OP='INSERT' AND EXISTS (SELECT 1 FROM catalog_item_successors s JOIN item_definitions i ON i.id=s.target_item_id
   WHERE s.source_item_id=source_id AND i.catalog_release_id=target_item.catalog_release_id)
 THEN RAISE EXCEPTION 'duplicate successor for source and target release'; END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; END IF; RETURN NEW;
END $$;
CREATE TRIGGER catalog_item_successor_guard BEFORE INSERT OR UPDATE OR DELETE ON public.catalog_item_successors
 FOR EACH ROW EXECUTE FUNCTION public.catalog_guard_item_successor();
REVOKE ALL ON public.catalog_item_successors FROM PUBLIC;
GRANT SELECT ON public.catalog_item_successors TO ashenfall_app;
REVOKE ALL ON FUNCTION public.catalog_guard_item_successor() FROM PUBLIC;
COMMIT;
