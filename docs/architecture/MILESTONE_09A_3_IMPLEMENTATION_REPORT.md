# Milestone 09A.3 — trusted catalog publication

**Status (2026-11-15): initial catalog published and validated.** The owner configured the publisher credential privately. No password or secret-bearing connection string was read, stored or printed.

## Files in this milestone

Created: `backend/Ashenfall.CatalogPublisher/Ashenfall.CatalogPublisher.csproj`, `Program.cs`, `CatalogPublication.cs`, `backend/Ashenfall.Tests/CatalogPublicationTests.cs`, and `backend/Ashenfall.Tests/CatalogPublicationDatabaseTests.cs`. Modified: `backend/Ashenfall.sln`, `backend/Ashenfall.Tests/Ashenfall.Tests.csproj`, and this plan/report. Existing gameplay, seed, hash, schema, migration, API routes and Godot scenes remain unchanged.

## Publisher and database access

The new .NET 8 console tool accepts `publish-initial`. It defaults to the non-secret local connection identity `localhost:5432/ashenfall_dev` as `ashenfall_publisher`; `ASHENFALL_PUBLISHER_CONNECTION` may supply non-secret connection configuration. It rejects another database, another role, or an inline password. PostgreSQL's private passfile supplies the credential. The tool never prints a connection string or an exception message, which may contain sensitive connection data. No HTTP publication endpoint was added.

The dedicated `ashenfall_publisher` role has LOGIN and no SUPERUSER/CREATEDB/CREATEROLE/REPLICATION/BYPASSRLS or role memberships. Its privileges are CONNECT on `ashenfall_dev`, USAGE on `public`, SELECT on the four catalog tables and EF migration history (for required migration preflight), INSERT and UPDATE on `catalog_releases`, and INSERT on the three definition tables. It has no DELETE/TRUNCATE, definition UPDATE, database/schema CREATE or database TEMPORARY. There are no sequences in this migration. The original `ashenfall_app` runtime grants were not changed. The project owner configured the password and private passfile locally; neither was inspected or copied into the repository.

## Transaction design

The publisher validates the approved seed with `CatalogValidator` and the exact SHA-256 V1 vector `a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17`. It verifies database/user identity, migration, four tables, two guard functions, four enabled triggers and effective grants before writing. Within one transaction it obtains `pg_advisory_xact_lock(0x41534846, 0x43415431)` (the `ASHF`/`CAT1` lock), checks the release version, compares any existing published content, creates or resumes only a matching draft, inserts missing definitions, validates and hashes the complete draft, locks its release row, retires an older active release, publishes `prototype-1` and commits. Mismatches raise and roll back. The existing partial unique index remains a second concurrency guard. The tool does not call `Migrate()`.

## Publication and validation

- Preflight as `ashenfall_publisher`: authentication succeeded to exactly `ashenfall_dev`; applied `20261115111756_InitialRpgCatalog`; four catalog tables; two guard functions; four enabled user triggers; zero catalog releases. Effective grants and absence of publisher role memberships were inspected before publication. The approved seed hash unit test passed.
- `dotnet run --no-build --project backend/Ashenfall.CatalogPublisher -- publish-initial` ran as `ashenfall_publisher` and committed `prototype-1`. Read-only SQL verified one published release, zero drafts, one `dwarf_warrior`, one `fallen_guardian`, two common items, and hash `a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17`. Dwarf values are 100 HP / 25 fixed damage / 2.500 m/s / 1.450 m / 1700/940 ms; Guardian values are 100 HP / 10 fixed damage / 600/1000/100 ms / chase 1.000 / return 0.700 m/s.
- A second CLI publication returned the approved-content no-op without new rows. Two concurrent no-op publication calls in the database test also passed; both returned `AlreadyPublished` and the published-release count remained one. The initial create race was not replayed against an empty database because the approved release must remain intact.
- Four opt-in live database tests passed: complete snapshot/hash, idempotent and concurrent replay, invalid draft and CHECK failure inside an explicitly rolled-back transaction (no residual test release), and rejected attempt to reopen a published release inside a rolled-back transaction.
- The existing `ValidateRuntimePermissions.sql` ran as `ashenfall_app` inside `BEGIN`/`ROLLBACK`: SELECT returned 1 release, 1 class, 1 enemy, 2 items; INSERT, UPDATE, DELETE, TRUNCATE, permanent DDL and temporary DDL were denied.
- The real ASP.NET Core API was started locally with a non-secret connection configuration using `ashenfall_app`. `GET /api/v1/catalog/version` returned HTTP 200 and `prototype-1` with the approved hash. The versioned snapshot returned HTTP 200, 1 class, 1 enemy, 2 items and the same hash, with a strong ETag. Matching `If-None-Match` returned 304; unknown version returned 404; missing version returned 400. The temporary API process was stopped after validation.
- `dotnet build backend/Ashenfall.sln`: passed, zero warnings/errors after project restore.
- `dotnet test backend/Ashenfall.sln --no-restore` with `ASHENFALL_RUN_DB_TESTS=1`: **17 passed, 0 failed, 0 skipped**. Four new unit tests cover the approved hash, invalid seed, resumable matching draft and changed/unreviewed draft rejection; four new live database tests cover the cases above. An initial unit test contained a test-code `Single()` error and was corrected before the passing run.
- `dotnet build Ashenfall.csproj`: passed, zero warnings/errors.
- Godot 4.7.2 Mono headless editor import and first-adventure startup: passed after publication. Existing environment/navigation initialized; no Godot gameplay files were changed in this milestone.

## Remaining limitations

Historical retired-release retention and a simultaneous first-publication race were not exercised: there is only one approved release, and creating another persistent release solely for testing would violate scope. The publisher code retires a previous release in the same transaction and does not delete history; the database's partial unique index guards against two published releases. No Godot catalog loader or gameplay cutover was added; the first adventure continues using its existing scene values.

**MILESTONE 09A.3 — INITIAL CATALOG PUBLISHED AND VALIDATED**
