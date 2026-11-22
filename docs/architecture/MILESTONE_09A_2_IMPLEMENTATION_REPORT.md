# Milestone 09A.2 — backend and initial catalog migration

**Migration applied and validated under the owner's explicit authorization on 2026-11-15.** The exact generated EF Core script at `backend/MigrationReview/InitialRpgCatalog.sql` was executed once as `postgres` against the verified `ashenfall_dev` database using `psql -X -w -v ON_ERROR_STOP=1 -f`. Its historical review-only header predates this approval; the file was not edited for execution. `POSTGRESQL_SCHEMA_PROPOSAL.sql` was not run. No seed or published release was created, and no Godot gameplay change was made. No additional database or backup was created.

## Structure and pinned dependencies

`backend/Ashenfall.sln` contains `Ashenfall.Api` (.NET 8 minimal read-only API) and `Ashenfall.Tests`. The API has four domain entities, explicit EF Core configurations, `CatalogDbContext`, a design-time factory without a connection string, catalog validation/hash/seed preparation, and two GET endpoints. The migration lives under `Ashenfall.Api/Migrations`. The Godot project excludes `backend/**/*.cs` from its compilation; `backend/.gdignore` also keeps Godot's asset scanner out of the backend. Godot does not reference backend assemblies.

Pinned packages: `Npgsql.EntityFrameworkCore.PostgreSQL` **8.0.11**, `Microsoft.EntityFrameworkCore.Design` **8.0.11**, local `dotnet-ef` **8.0.11**, `Microsoft.AspNetCore.Mvc.Testing` **8.0.34**, `Microsoft.NET.Test.Sdk` **17.14.1**, xUnit **2.9.3** and VS runner **3.1.4**. The Npgsql provider targets .NET 8 and depends on EF Core 8; [NuGet's provider page](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/8.0.11) records that compatibility. Package restore affects the backend's local build only.

## Initial migration and permissions

`InitialRpgCatalog` generates exactly four RPG tables: `catalog_releases`, `character_classes`, `enemy_definitions`, `item_definitions`. It includes UUID keys, reviewed checks/FKs, `numeric(7,3)`, nullable inactive attributes, `hand_requirement`, the partial unique published index, two PL/pgSQL guard functions and four triggers. `Down` removes the triggers/functions before dropping the four tables. EF's own `__EFMigrationsHistory` table is tooling metadata and appears in the generated script. The seven deferred RPG tables are absent from this migration.

The reviewed runtime grants were applied as `postgres` **after** schema/lifecycle validation:

```sql
GRANT USAGE ON SCHEMA public TO ashenfall_app;
GRANT SELECT ON TABLE catalog_releases, character_classes, enemy_definitions, item_definitions TO ashenfall_app;
```

`ashenfall_app` has USAGE on `public` and SELECT on the four catalog tables only. It cannot SELECT `__EFMigrationsHistory`; INSERT, UPDATE, DELETE, TRUNCATE, schema/database CREATE and TEMPORARY remain denied. It does not own the database or tables. No role attributes or passwords were changed. A separate trusted publisher/deployment identity remains an open decision. Normal API operation never uses `postgres`; it reads an environment-provided `ASHENFALL_DB_CONNECTION`. Without it, catalog requests return 503. No connection string or secret is stored in the repository. The API does not call `Database.Migrate()`.

## Canonical catalog hash V1

`CatalogHash` uses SHA-256 over an explicitly ordered binary stream. It writes a length-prefixed UTF-8 NFC string `ASHENFALL-CATALOG-CONTENT-V1`, a little-endian 32-bit `schemaMajor`, then the length-prefixed `versionKey`. Classes, enemies and items follow in that fixed group order; each group starts with a little-endian 32-bit count and rows sorted by `code` using ordinal comparison. Every row writes its definition UUID as lowercase `D` text, then **all authored fields in the order in `CatalogHash.cs`**. Each string is a little-endian 32-bit byte length followed by UTF-8 bytes; NULL string is length `-1`. Integers are little-endian 32-bit. Nullable integers start with a one-byte presence marker (`0`/`1`), followed by the integer when present. Boolean is one byte. Decimal `numeric(7,3)` values are multiplied by 1000 and written as exact little-endian 32-bit integers, rejecting more than three fractional digits. This makes `2.5` and `2.500` identical without locale-dependent formatting. Unicode strings normalize to NFC before encoding.

Definition UUIDs are included because they are returned to clients and identify immutable definitions; the seed uses stable UUIDs. The release UUID, status, hash field and timestamps are excluded as publication metadata. `schemaMajor` and `versionKey` are included so changing either changes the hash. All definition fields, including nullable inactive attributes, visual keys and hand requirements, are included. A changed UUID, stat, visual key, NULL/zero, added/removed definition or version changes the digest. The same logical values in a different input-row order produce the same digest.

Test vectors from the fixed `InitialCatalogSeed` (draft status does not participate):

| Content | SHA-256 |
| --- | --- |
| Approved seed, `prototype-1` | `a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17` |
| Same seed, dwarf fixed damage changed from 25 to 26 | `26f978ea12411b35176f7b2ab335e026de67b9465d7dcf9fe65ef5162e3bd11b` |

This is a versioned content format, not an HTTP ETag. The API computes a separate ETag over the exact JSON response bytes. Any future change to the canonical binary field order or representation requires hash format V2 and a compatibility plan; old release hashes remain verifiable with V1.

## Seed and publication status

`InitialCatalogSeed.CreateDraft()` prepares stable IDs and the verified dwarf (100 HP, fixed 25 damage, 2.5 m/s, 1.45 m, 1700/940 ms), Guardian (100 HP, fixed 10 damage, 600/1000/100 ms, chase 1.0, return 0.7, all reviewed radii) and two visual common-rarity weapons. Their attribute/armor fields remain NULL and items carry no damage or speed. The first adventure's 1200 ms Guardian windup and West/East overrides remain scene values. `CompareExisting` treats the same version/hash as already present and a different hash as a conflict; it never mutates a published release. **No seed executor or publication endpoint exists.**

The publication design remains: a separately authorized publisher acquires a transaction-level lock, locks the draft, validates the complete catalog and visual keys, computes the canonical hash, retires the old active release and publishes the draft in one transaction. The partial unique index is the database backstop. Published/retired releases are retained indefinitely during development. An API snapshot query uses one `REPEATABLE READ` transaction with `SET TRANSACTION READ ONLY`, validates the catalog and hash, and permits published or historical retired versions. The exact publisher identity and privilege boundary must be approved before implementation.

## Read-only API status

- `GET /api/v1/catalog/version` returns one active version and metadata, or 503 when unavailable.
- `GET /api/v1/catalog/snapshot?version=...` returns one complete immutable published/retired version; invalid query 400, absent version 404, unavailable/invalid store 503. It emits a strong ETag for serialized response bytes and honors `If-None-Match` with 304.
- No POST/PUT/DELETE routes, account/authentication routes or gameplay coupling exist. No production offline fallback is implemented.
- The API was started once with the restricted application role and no secret on the command line. Against the migrated but empty database, the live `version` route returned **503** (no published release) and a requested nonexistent snapshot returned **404**. The temporary server was stopped. Stub-based HTTP tests still do not prove publication or a populated snapshot.

## Validation and future controlled execution

Executed after migration: `dotnet build` of the backend solution (zero warnings/errors), `dotnet test` (**9 passed, 0 failed**), and `dotnet build Ashenfall.csproj` (zero warnings/errors). Godot 4.7.2 headless import and startup passed during backend preparation; no scene was changed in this execution. The API tests cover version/snapshot/ETag/304/400/404/503; domain tests cover baseline seed, invalid equipment/timing, deterministic hash and test vectors, idempotent seed comparison, lifecycle rules, four-table EF model and migration guard presence.

PostgreSQL 18.7 catalog verification found **four RPG tables** plus EF's `__EFMigrationsHistory` with migration `20261115111756_InitialRpgCatalog` / EF 8.0.11. The four RPG tables have **4 primary keys, 3 validated foreign keys with ON DELETE RESTRICT, 28 validated CHECK constraints, and 9 valid/ready unique indexes** (including the partial `catalog_one_published`). Both guard functions exist and all four user triggers are enabled. No deferred table exists. There are **zero catalog release/definition rows**.

`backend/MigrationReview/ValidateInitialRpgCatalog.sql` ran as `postgres` inside `BEGIN`/`ROLLBACK`. It verified editable drafts; rejection of direct publication, missing hash, invalid hand requirement, negative damage, duplicate item code and invalid parent; immutable published class/enemy/item definitions; rejected published delete/late insert/hash mutation; rejected definition release reassignment; at most one published release; safe retirement; rejected retirement timestamp mutation, retired definition edits and republishing. A first attempt expected an FK error for a missing parent, but the definition trigger correctly rejected it earlier; that test connection rolled back automatically and the corrected script passed. The later explicit rollback left **zero test rows**. Catalog metadata separately confirms all three FKs exist and are validated. These transactional probes do not constitute a real seed or persistent publication.

`backend/MigrationReview/ValidateRuntimePermissions.sql` ran as `ashenfall_app` inside `BEGIN`/`ROLLBACK`: SELECT of all four tables worked; INSERT/UPDATE/DELETE/TRUNCATE, permanent table CREATE and TEMP table CREATE were rejected. Catalog ACL inspection confirmed SELECT true and DML false on each of the four tables, with no EF history access. Application role LOGIN/CONNECT remain available; schema CREATE, database CREATE/TEMP and administrative role attributes remain false.

**Still untested/deferred:** a fully populated published API snapshot and hash check, concurrent transactions from multiple sessions, seed replay, publisher identity/authorization, historical release retention under real publication, and a Down migration. These require separate authorization; no seed or publisher operation was run. The guard tests used synthetic rows only inside rolled-back transactions.

The reviewed script is now applied to `ashenfall_dev`; do not apply it again. A future rollback would be destructive and requires separate owner direction, even though the catalog is currently empty. If data is later published, preserve it and plan a reviewed forward/inverse migration; never automatically drop catalog tables. No rollback command or backup was run during this task.

## Remaining decisions

1. Choose the trusted publication identity and authorization mechanism; `ashenfall_app` remains read-only.
2. Approve the binary hash V1 contract/test vectors and the `prototype-1` seed version before any real seed publication.
3. Authorize a publisher service/CLI and a separate publication test plan before writing persistent catalog rows.

## Files changed in this preparation

Modified: `Ashenfall.csproj` (exclude backend C# from Godot compilation), `docs/architecture/API_CONTRACT_PROPOSAL.md`, `docs/architecture/MILESTONE_09A_IMPLEMENTATION_PLAN.md`, and `docs/architecture/POSTGRESQL_SCHEMA_PROPOSAL.sql` (nest the UPDATE-only trigger condition so DELETE does not reference an unset `NEW` row). This last change preserves the approved guard behavior while removing an implementation hazard.

Created: `backend/Ashenfall.sln`, `backend/.gdignore`, `backend/.config/dotnet-tools.json`; `backend/Ashenfall.Api/Ashenfall.Api.csproj`, `Program.cs`, `Catalog/Domain/CatalogEntities.cs`, `Catalog/Persistence/CatalogDbContext.cs`, `Catalog/Persistence/CatalogConfigurations.cs`, `Catalog/Persistence/CatalogDesignTimeFactory.cs`, `Catalog/Services/CatalogHash.cs`, `Catalog/Services/CatalogLifecycle.cs`, `Catalog/Services/CatalogReader.cs`, `Catalog/Services/CatalogValidator.cs`, `Catalog/Services/InitialCatalogSeed.cs`, `Catalog/Endpoints/CatalogEndpoints.cs`, `Migrations/20261115111756_InitialRpgCatalog.cs`, `Migrations/20261115111756_InitialRpgCatalog.Designer.cs`, `Migrations/CatalogDbContextModelSnapshot.cs`; `backend/Ashenfall.Tests/Ashenfall.Tests.csproj`, `CatalogTests.cs`, `CatalogApiTests.cs`; `backend/MigrationReview/InitialRpgCatalog.sql`; and this report. Build artifacts under ignored `bin/`, `obj/` and `.godot/` were generated by validation and are not source files.

## Files changed by migration execution

Created `backend/MigrationReview/ValidateInitialRpgCatalog.sql` and `backend/MigrationReview/ValidateRuntimePermissions.sql` as repeatable, explicitly rolled-back validation scripts. Updated this report and `docs/architecture/MILESTONE_09A_IMPLEMENTATION_PLAN.md` to reflect the applied migration. The existing migration source and generated SQL script were **not edited or regenerated**. No gameplay/source file changed in this execution.

**MILESTONE 09A.2 — DATABASE MIGRATION VALIDATED**
