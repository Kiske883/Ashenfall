-- Test-only additive transition metadata. Does not change existing catalog rows.
\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout = '5s';
DO $$ BEGIN
  IF current_database()<>'ashenfall_09b3_test' OR current_user<>'postgres' OR inet_server_port() IS DISTINCT FROM 5432
     OR inet_server_addr() IS NULL OR NOT (inet_server_addr() <<= '127.0.0.0/8'::inet OR inet_server_addr()='::1'::inet)
     OR (SELECT system_identifier::text FROM pg_control_system())<>'7708337984335018212'
  THEN RAISE EXCEPTION 'wrong test database or cluster'; END IF;
  IF to_regclass('public.catalog_v2_manifest') IS NOT NULL OR to_regclass('public.catalog_class_successors') IS NOT NULL
  THEN RAISE EXCEPTION 'transition metadata already exists'; END IF;
END $$;
CREATE TABLE public.catalog_v2_manifest (
 release_id uuid PRIMARY KEY REFERENCES public.catalog_releases(id) ON DELETE RESTRICT,
 hash_format text NOT NULL CHECK (hash_format='ASHENFALL-CATALOG-CONTENT-V2'),
 content_sha256 text NOT NULL CHECK (content_sha256 ~ '^[0-9a-f]{64}$')
);
CREATE TABLE public.catalog_class_successors (
 source_class_id uuid NOT NULL REFERENCES public.character_classes(id) ON DELETE RESTRICT,
 target_class_id uuid NOT NULL REFERENCES public.character_classes(id) ON DELETE RESTRICT,
 PRIMARY KEY (source_class_id,target_class_id),
 CHECK (source_class_id<>target_class_id)
);
CREATE FUNCTION public.catalog_guard_v2_manifest() RETURNS trigger LANGUAGE plpgsql SET search_path=public,pg_catalog AS $$
DECLARE release_id uuid; release_status text;
BEGIN
 IF TG_OP='UPDATE' AND NEW.release_id IS DISTINCT FROM OLD.release_id THEN RAISE EXCEPTION 'manifest release is immutable'; END IF;
 IF TG_OP='DELETE' THEN release_id=OLD.release_id; ELSE release_id=NEW.release_id; END IF;
 SELECT status INTO release_status FROM catalog_releases WHERE id=release_id FOR UPDATE;
 IF release_status IS DISTINCT FROM 'draft' THEN RAISE EXCEPTION 'published or retired V2 manifest is immutable'; END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; END IF; RETURN NEW;
END $$;
CREATE TRIGGER catalog_v2_manifest_guard BEFORE INSERT OR UPDATE OR DELETE ON public.catalog_v2_manifest
 FOR EACH ROW EXECUTE FUNCTION public.catalog_guard_v2_manifest();
CREATE FUNCTION public.catalog_guard_class_successor() RETURNS trigger LANGUAGE plpgsql SET search_path=public,pg_catalog AS $$
DECLARE source_id uuid; target_id uuid; source_code text; target_code text; source_release uuid; target_release uuid; source_status text; target_status text;
BEGIN
 IF TG_OP='UPDATE' THEN RAISE EXCEPTION 'replace a draft successor instead of updating it'; END IF;
 IF TG_OP='DELETE' THEN source_id=OLD.source_class_id; target_id=OLD.target_class_id;
 ELSE source_id=NEW.source_class_id; target_id=NEW.target_class_id; END IF;
 SELECT c.code,c.catalog_release_id,r.status INTO source_code,source_release,source_status FROM character_classes c JOIN catalog_releases r ON r.id=c.catalog_release_id WHERE c.id=source_id FOR UPDATE OF r;
 SELECT c.code,c.catalog_release_id,r.status INTO target_code,target_release,target_status FROM character_classes c JOIN catalog_releases r ON r.id=c.catalog_release_id WHERE c.id=target_id FOR UPDATE OF r;
 IF source_code IS NULL OR target_code IS NULL OR source_code<>target_code OR source_release=target_release OR
    source_status NOT IN ('published','retired') OR target_status IS DISTINCT FROM 'draft'
 THEN RAISE EXCEPTION 'invalid class successor or immutable published mapping'; END IF;
 IF TG_OP='INSERT' AND EXISTS (SELECT 1 FROM catalog_class_successors s JOIN character_classes c ON c.id=s.target_class_id
   WHERE s.source_class_id=source_id AND c.catalog_release_id=target_release)
 THEN RAISE EXCEPTION 'duplicate successor for source and target release'; END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; END IF; RETURN NEW;
END $$;
CREATE TRIGGER catalog_class_successor_guard BEFORE INSERT OR UPDATE OR DELETE ON public.catalog_class_successors
 FOR EACH ROW EXECUTE FUNCTION public.catalog_guard_class_successor();
REVOKE ALL ON public.catalog_v2_manifest, public.catalog_class_successors FROM PUBLIC;
GRANT SELECT ON public.catalog_v2_manifest, public.catalog_class_successors TO ashenfall_app;
REVOKE ALL ON FUNCTION public.catalog_guard_v2_manifest(), public.catalog_guard_class_successor() FROM PUBLIC;
COMMIT;
