# ASP.NET Core API contract proposal

**Contract baseline.** The two public GET routes now have a backend implementation described in [the 09A.2 implementation report](MILESTONE_09A_2_IMPLEMENTATION_REPORT.md). It has not been exercised against a migrated PostgreSQL database. The backend is the sole DB client. Route major is `/api/v1`; the catalog's `schemaMajor` and `catalogVersion` are separate from the HTTP route version. All responses are UTF-8 JSON over HTTPS in deployed environments. The published catalog is public read-only data; mutable character state is future authenticated scope.

## Initial public catalog endpoints (09A.3)

| Method/path | Purpose | Response/cache |
| --- | --- | --- |
| `GET /api/v1/catalog/version` | Discover one published release | `{ schemaMajor, catalogVersion, contentSha256, publishedAt }`; short-lived public cache or no-cache revalidation |
| `GET /api/v1/catalog/snapshot?version={catalogVersion}` | Fetch one complete immutable, internally consistent release | `{ schemaMajor, catalogVersion, contentSha256, classes[], enemies[], items[] }`; strong `ETag` for the exact serialized response representation; `If-None-Match` → 304; public immutable cache for a versioned URL |

Do not expose a draft or combine rows from different releases. `version` is required for the snapshot endpoint; an unknown/retired version is returned only if the retention policy permits it for pinned clients, otherwise a documented 404/410. The backend validates every definition at publication time and verifies the snapshot hash before serving. The initial small catalog does not need separate class/enemy/item requests; category endpoints can be introduced if snapshot size warrants them. No output cache may expose authenticated character data. [ASP.NET Core output caching](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/output?view=aspnetcore-8.0) documents cache policy and middleware ordering.

Representative DTO (names are contract candidates):

```json
{
  "schemaMajor": 1,
  "catalogVersion": "2026-11-15.1",
  "contentSha256": "<64 lowercase hex digits>",
  "classes": [{
    "id": "<uuid>", "code": "dwarf_warrior", "visualKey": "dwarf_berserker",
    "baseMaxHealth": 100, "fixedDamage": 25, "moveSpeedMps": 2.5,
    "meleeRangeM": 1.45, "attackCycleMs": 1700, "impactMs": 940
  }],
  "enemies": [{
    "id": "<uuid>", "code": "fallen_guardian", "visualKey": "fallen_guardian_v3",
    "maxHealth": 100, "fixedDamage": 10, "windupMs": 600,
    "recoveryMs": 1000, "impactAfterExecuteMs": 100,
    "chaseSpeedMps": 1.0, "returnSpeedMps": 0.7
  }],
  "items": [{
    "id": "<uuid>", "code": "training_battle_axe",
    "category": "weapon", "equipSlot": "main_hand", "handRequirement": 2,
    "visualKey": "dwarf_battle_axe"
  }]
}
```

The real DTO includes the other checked catalog fields from the SQL proposal and the proposed Guardian sword item; the example omits them only for readability. The items carry no active damage or speed data in 09A.2. UUIDs are server-assigned; `visualKey` resolves through a **client allowlist** and is never treated as an executable scene path. Distances are decimal JSON numbers and timing is integer milliseconds. Validate finite, bounded numbers, unique IDs/codes, known visual keys, and cross references at both API publication and client load. See the [09A.2 final review](MILESTONE_09A_2_FINAL_REVIEW.md) for the proposed seed and publication guards.

## Future authenticated endpoints (not implemented in 09A.3)

| Method/path | Future behavior |
| --- | --- |
| `GET /api/v1/me/characters` | List characters owned by authenticated account |
| `POST /api/v1/me/characters` | Create class/name under `MaxCharactersPerAccount`; server derives owner from validated identity, never request body |
| `GET /api/v1/characters/{id}` | Return character only when authenticated owner matches; otherwise 404 or 403 under a consistent disclosure policy |
| `GET /api/v1/characters/{id}/inventory` | Future owned items and equipment, owner-authorized |
| `PUT /api/v1/characters/{id}/exploration/{region}` | Future host-authorized discovered-mask merge/update; never trust a raw client claim in online multiplayer |

These routes must **not** be deployed before authentication, account binding and ownership authorization exist. A development-only offline account fixture may be enabled only by an explicit Development environment setting and must fail application startup if enabled in Production. Production online requests require validated identity and an ownership check on every mutable resource; [ASP.NET Core distinguishes authentication and authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/?view=aspnetcore-7.0).

Character creation transaction: resolve the account from auth claims, `SELECT ... FOR UPDATE` its account row, count non-deleted characters, enforce configurable `MaxCharactersPerAccount=1`, validate class/version/name, insert and commit. A second concurrent request waits and observes the first insert before deciding. All writes use the backend role; no one-character database unique constraint freezes the future 1:N model. A client-generated idempotency key may later guard retries, but it cannot replace the transaction. [PostgreSQL row-level locking](https://www.postgresql.org/docs/current/explicit-locking.html) supports this pattern. Mutable updates compare/increment `row_revision` and return 409 on conflict; [EF Core concurrency guidance](https://learn.microsoft.com/en-us/ef/core/modeling/concurrency) explains the token pattern.

## Validation and errors

- `200` successful JSON; `304` unchanged versioned catalog; `400` malformed DTO/query; `401` unauthenticated future private request; `403` authenticated but forbidden when disclosing existence is acceptable; `404` missing/hidden resource; `409` character limit/name/revision conflict; `422` semantically invalid catalog draft or character request; `503` catalog store unavailable with no valid served snapshot.
- Use RFC-style Problem Details with stable machine `type`/`code`, a safe message and correlation ID. Do not echo connection strings, SQL or stack traces. [ASP.NET Core error-handling guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-8.0) covers Problem Details.
- Public catalog cache keys include route major, schema major and catalog version; no mutable state is publicly cached. Versioned immutable snapshots can be cached at the API and client. Publication switches the active release only after a validated snapshot/hash exists. Older pinned releases remain available for a compatibility window.
- Reject unsupported schema majors explicitly rather than guessing field meanings. An additive DTO field in the same major can have a safe default only when documented; a breaking change increments the major and gets a migration plan.

## Persistence access choice

Use EF Core + `Npgsql.EntityFrameworkCore.PostgreSQL` for the API's small aggregate model and reviewed migrations. The DB schema proposal remains the design reference; a later migration must be reviewed against it. For the future creation transaction, a small parameterized `SELECT ... FOR UPDATE` via EF's connection/transaction or Npgsql is justified. Dapper is not needed until profiling identifies a query that benefits from it. Neither EF nor Npgsql is added to the current Godot project. [EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/) and [Npgsql's EF provider](https://www.npgsql.org/efcore/) describe these supported paths.
