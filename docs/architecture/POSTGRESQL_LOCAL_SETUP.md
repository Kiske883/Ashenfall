# PostgreSQL local development setup

Completed on 2026-11-15 for Ashenfall development. This is infrastructure setup only; Milestone 09A.2 and the RPG schema remain unimplemented. `POSTGRESQL_SCHEMA_PROPOSAL.sql` was not executed.

## Configuration

| Setting | Value |
| --- | --- |
| PostgreSQL server | 18.7, Windows service `postgresql-x64-18` |
| Host / port | `localhost` / `5432` |
| Development database | `ashenfall_dev` |
| Database owner | `postgres` |
| Application role | `ashenfall_app` |

`ashenfall_app` has LOGIN and an explicit CONNECT grant on `ashenfall_dev`. It is not a superuser and has no CREATEDB, CREATEROLE, REPLICATION or BYPASSRLS capability. PUBLIC database privileges were revoked, so the application role cannot CREATE in the database or use temporary tables. PUBLIC privileges on the database's `public` schema were revoked; the application role has neither USAGE nor CREATE there. No role memberships, application tables, migrations or RPG objects were added.

The role was created with **no password**, at the owner's request. Therefore password-based application-role authentication remains untested until the owner sets a password locally. In an interactive `psql` session as `postgres`, use `\password ashenfall_app`; the prompt must remain local. Configure any local `pgpass.conf` entry privately, without copying its contents to this repository or a report. Do not pass passwords on the command line or distribute database credentials with the Godot client. No PostgreSQL authentication configuration was changed.

## Read-only validation

Run these commands from PowerShell. `-w` uses an already configured local credential source and fails rather than prompting. The commands contain no secrets.

```powershell
$psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
& $psql -X -w -h localhost -p 5432 -U postgres -d postgres -c "SELECT datname, pg_get_userbyid(datdba) AS owner, has_database_privilege('ashenfall_app',datname,'CONNECT') AS app_connect, has_database_privilege('ashenfall_app',datname,'CREATE') AS app_create, has_database_privilege('ashenfall_app',datname,'TEMPORARY') AS app_temp FROM pg_database WHERE datname='ashenfall_dev';"
& $psql -X -w -h localhost -p 5432 -U postgres -d postgres -c "SELECT rolname, rolcanlogin, rolsuper, rolcreatedb, rolcreaterole, rolreplication, rolbypassrls FROM pg_roles WHERE rolname='ashenfall_app';"
& $psql -X -w -h localhost -p 5432 -U postgres -d ashenfall_dev -c "SELECT has_schema_privilege('ashenfall_app','public','CREATE') AS app_schema_create; SELECT count(*) AS application_tables FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind IN ('r','p') AND n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname !~ '^pg_toast';"
```

Observed immediately after setup: database owner `postgres`; application CONNECT true, database CREATE false, TEMPORARY false; PUBLIC CONNECT false; role LOGIN true and all five restricted role attributes false; zero role memberships; `public` schema USAGE/CREATE false for the application role; zero application tables. The service was running and server version was 18.7.

After the owner sets the password and configures local credentials, validate authentication without exposing the password:

```powershell
& $psql -X -w -h localhost -p 5432 -U ashenfall_app -d ashenfall_dev -c 'SELECT current_user, current_database();'
```

## Rollback, only with separate authorization

First verify this is still the newly created, empty development database and that no work depends on the role. Then, as `postgres`, revoke the application's CONNECT grant, drop the database, and drop the role in that order. Database deletion is destructive and is **not** part of setup validation.

```sql
REVOKE CONNECT ON DATABASE ashenfall_dev FROM ashenfall_app;
DROP DATABASE ashenfall_dev;
DROP ROLE ashenfall_app;
```

No rollback statement has been executed.
