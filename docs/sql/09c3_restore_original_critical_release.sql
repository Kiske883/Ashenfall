-- REVIEW ONLY / NO-GO: diagnostic preflight, NOT a working activation script.
-- DBeaver PostgreSQL SQL script; execute the entire file, stop on first error.
-- Contains no database mutations. No trigger or hash bypass.
BEGIN READ ONLY;
SET LOCAL statement_timeout = '10s';
DO $guard$
BEGIN
  IF current_database() <> 'ashenfall_09b3_test'
     OR inet_server_port() IS DISTINCT FROM 5432
     OR inet_server_addr() IS NULL
     OR NOT (inet_server_addr() = '127.0.0.1'::inet OR inet_server_addr() = '::1'::inet)
     OR (SELECT system_identifier::text FROM pg_control_system()) <> '7708337984335018212'
  THEN RAISE EXCEPTION 'Wrong database or local cluster; stop'; END IF;
END
$guard$;
SELECT r.id, r.version_key, r.status, m.hash_format, m.content_sha256 AS v2_hash
FROM public.catalog_releases r
LEFT JOIN public.catalog_v2_manifest m ON m.release_id=r.id
WHERE r.id='91000000-0000-0000-0000-000000000001' OR r.status='published';
SELECT p.id,p.class_definition_id,p.experience,p.row_revision,
       c.id AS resolved_class_id,c.critical_chance_percent,
       c.critical_damage_bonus_percent,l.level,l.health_bonus,l.damage_bonus,
       l.critical_chance_bonus_percent,l.critical_damage_bonus_percent AS level_critical_bonus,
       (c.base_max_health+l.health_bonus) AS unequipped_max_health,
       (c.min_damage+l.min_damage_bonus+l.damage_bonus) AS unequipped_min_damage,
       (c.max_damage+l.max_damage_bonus+l.damage_bonus) AS unequipped_max_damage,
       LEAST(100,c.critical_chance_percent+l.critical_chance_bonus_percent)
         AS unequipped_critical_chance
FROM public.player_characters p
JOIN public.catalog_class_successors s ON s.source_class_id=p.class_definition_id
JOIN public.character_classes c ON c.id=s.target_class_id
JOIN public.catalog_releases r ON r.id=c.catalog_release_id AND r.status='published'
JOIN LATERAL (SELECT * FROM public.class_level_rules q
              WHERE q.class_definition_id=c.id AND q.required_total_xp<=p.experience
              ORDER BY q.level DESC LIMIT 1) l ON true
WHERE p.id='d9a62a3c-4996-4276-8ab8-2c4d6f2ec988'
  AND NOT EXISTS(SELECT 1 FROM public.character_equipment e WHERE e.character_id=p.id);
-- The arithmetic above verifies only the specified unequipped fixture.
-- The authenticated backend snapshot remains the authoritative effective calculation.
DO $baseline$
BEGIN
  IF (SELECT count(*) FROM public.catalog_releases WHERE status='published')<>1
     OR NOT EXISTS (
       SELECT 1 FROM public.catalog_releases r JOIN public.catalog_v2_manifest m ON m.release_id=r.id
       WHERE r.id='91000000-0000-0000-0000-000000000001'
         AND r.version_key='snapshot-09c3-v2' AND r.status='published'
         AND m.hash_format='ASHENFALL-CATALOG-CONTENT-V2'
         AND m.content_sha256='d1ef0a212b83df17300088210d081a63951621b2602e7ff35cf2a711c4de7d4d')
     OR NOT EXISTS (
       SELECT 1 FROM public.player_characters p
       JOIN public.catalog_class_successors s ON s.source_class_id=p.class_definition_id
       JOIN public.character_classes c ON c.id=s.target_class_id
       JOIN public.class_level_rules l ON l.class_definition_id=c.id AND l.level=2
       WHERE p.id='d9a62a3c-4996-4276-8ab8-2c4d6f2ec988'
         AND p.class_definition_id='90000000-0000-0000-0000-000000000002'
         AND p.experience=100 AND p.row_revision=1 AND p.deleted_at IS NULL
         AND c.catalog_release_id='91000000-0000-0000-0000-000000000001'
         AND c.base_max_health=100 AND c.fixed_damage=25
         AND c.min_damage=25 AND c.max_damage=25
         AND c.critical_chance_percent=5 AND c.critical_damage_bonus_percent=100
         AND l.required_total_xp=100 AND l.health_bonus=5 AND l.damage_bonus=2
         AND l.min_damage_bonus=0 AND l.max_damage_bonus=0
         AND l.critical_chance_bonus_percent=0 AND l.critical_damage_bonus_percent=0
         AND NOT EXISTS (SELECT 1 FROM public.character_equipment e WHERE e.character_id=p.id))
  THEN RAISE EXCEPTION 'Original baseline differs; stop and review, do not modify data'; END IF;
END
$baseline$;
ROLLBACK;
DO $stop$ BEGIN
  RAISE EXCEPTION 'NO-GO: retired-to-published transition is forbidden. Original release identity/hash cannot be reactivated through current supported mechanisms. A separately reviewed forward restoration release and C# tooling are required.';
END $stop$;
