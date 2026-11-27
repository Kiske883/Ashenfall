\set ON_ERROR_STOP on
BEGIN;
DO $$ BEGIN
 IF current_database()<>'ashenfall_09b3_test' OR current_user<>'postgres' OR inet_server_port() IS DISTINCT FROM 5432
    OR inet_server_addr() IS NULL OR NOT (inet_server_addr() <<= '127.0.0.0/8'::inet OR inet_server_addr()='::1'::inet)
    OR (SELECT system_identifier::text FROM pg_control_system())<>'7708337984335018212'
 THEN RAISE EXCEPTION 'wrong test database or cluster'; END IF;
 IF EXISTS(SELECT 1 FROM public.catalog_v2_manifest) OR EXISTS(SELECT 1 FROM public.catalog_class_successors)
 THEN RAISE EXCEPTION 'transition metadata exists; rollback would destroy it'; END IF;
END $$;
DROP TABLE public.catalog_class_successors;
DROP TABLE public.catalog_v2_manifest;
DROP FUNCTION public.catalog_guard_class_successor();
DROP FUNCTION public.catalog_guard_v2_manifest();
COMMIT;
