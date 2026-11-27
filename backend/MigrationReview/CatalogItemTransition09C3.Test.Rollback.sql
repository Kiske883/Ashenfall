\set ON_ERROR_STOP on
BEGIN;
DO $$ BEGIN
 IF current_database()<>'ashenfall_09b3_test' OR current_user<>'postgres' OR inet_server_port() IS DISTINCT FROM 5432
    OR inet_server_addr() IS NULL OR NOT (inet_server_addr() <<= '127.0.0.0/8'::inet OR inet_server_addr()='::1'::inet)
    OR (SELECT system_identifier::text FROM pg_control_system())<>'7708337984335018212'
 THEN RAISE EXCEPTION 'wrong test database or cluster'; END IF;
 IF EXISTS (SELECT 1 FROM public.catalog_item_successors) THEN RAISE EXCEPTION 'item mappings exist; rollback would destroy them'; END IF;
END $$;
DROP TABLE public.catalog_item_successors;
DROP FUNCTION public.catalog_guard_item_successor();
COMMIT;
