# LoanTracker

Internal equipment loan tracking — Podium 2 pilot build.

Track **who currently has this item, and since when**, as an authoritative fact rather
than a derived guess.

---

## Stack

| Layer | Technology |
|---|---|
| Frontend | Angular 18 · TypeScript · Angular Material 18 |
| Backend | C# .NET 8 REST API · EF Core 8 |
| Database | PostgreSQL 17 |
| Local infra | Docker Compose (Postgres only) |
| Tests | xUnit · Jasmine/Karma · Playwright + `@axe-core/playwright` |

Single service, single schema, single tenant. No gateway, no microservices —
three aggregates do not need them.

---

## Running it locally

Three terminals, after a one-time credential step. Ports are committed configuration,
not conventions.

### 0. Local credentials — once per machine

No credential is committed to this repository, including throwaway local ones
(`LoanTracker_Stack_Rules.md` §STACK_RULES makes no exception for them). So set yours
up first:

```bash
cp .env.example .env          # then pick a password in .env — it is gitignored

cd backend/Api
dotnet user-secrets set "ConnectionStrings:LoanTracker" \
  "Host=localhost;Port=5433;Database=loantracker_dev_20260919_pilot;Username=loantracker;Password=<the password from .env>"
```

Both steps fail loudly rather than silently if you skip them: `docker compose` refuses
to start without `.env`, and the API throws a named error on boot without the secret.

> **Running the test suites while the app is running.** `dotnet test` fails to rebuild
> while `dotnet run` holds `Api.exe` *and* `Api.dll` — `-p:UseAppHost=false` is not
> enough. Redirect the build out of the locked tree instead:
> `dotnet test Api.Tests/Api.Tests.csproj -o "$env:TEMP/lt-verify"`.
> For Playwright, note that `ng build` writes `dist/` and does **not** wait for
> `ng serve` to finish recompiling — a run started too soon fails against a stale
> bundle with errors that look like real defects.

> **If you cloned this repo before the credential was moved out**, the old password is
> still in git history and your existing Postgres volume was created with it. Changing
> `.env` alone does not change the running database's password — drop the volume once:
> `docker compose down -v`, then `docker compose up -d`. Local data is synthetic seed
> data and is recreated on the next API start.

### 1. Database

```bash
docker compose up -d
```

Postgres 17 on **host port 5433** (not 5432, so it cannot collide with an existing
local install). Credentials come from your `.env`.

### 2. API — http://localhost:5080

```bash
cd backend/Api
dotnet run
```

On first run in Development this applies the generated migrations and seeds
synthetic reference and demo data. It is idempotent — restarting does not re-seed.

Swagger UI: http://localhost:5080/swagger

### 3. Frontend — http://localhost:4200

```bash
cd frontend
npm install     # first time only
npm start
```

`proxy.conf.json` forwards `/api` to the API on 5080, so the browser sees one origin.

---

## Tests

```bash
# Backend unit + integration (Testcontainers spins up a throwaway Postgres)
cd backend && dotnet test

# Frontend unit
cd frontend && npm run test:ci

# End-to-end — requires the API and frontend to already be running
cd frontend && npm run e2e
```

Playwright runs **headed, single-worker and slowed** via `playwright.config.ts`, so a
validation run is watchable and reproducible from committed config.

---

## The rule this system exists to enforce

> An item already on an open loan cannot be loaned again.

This is **not** an application-layer check. It is a filtered unique index in Postgres:

```sql
CREATE UNIQUE INDEX ux_loans_item_open
  ON loans (item_id)
  WHERE returned_at IS NULL;
```

The checkout path deliberately does **not** read availability before inserting. A
read-then-write leaves a gap where two staff both see an item as available, both pass
the check, and both insert. Instead the insert is attempted and the database decides:
one wins, the other raises a unique violation that becomes a `409 Conflict` with a
specific, user-facing reason. Nothing partial is persisted.

Related rules, all enforced server-side:

- An item's availability is **derived** from whether an open loan exists — never a
  stored status column that could drift from the ledger.
- An item with an open loan **cannot be retired**; a borrower with an open loan
  **cannot be deactivated**. Both are hard blocks, not warnings.
- A loan can only be closed by a **terminal** status. Which statuses are terminal is
  maintained reference data, so a new one can be added without a code change.
- Nothing is ever hard-deleted. Every table carries `is_active`.

---

## Project layout

```
backend/
  Api/
    Controllers/     REST endpoints — no business logic
    Services/        business rules, including the checkout guarantee
    Data/            EF Core context, entities, repositories, migrations, seed
    Middleware/      stub auth, RFC 7807 exception handling, audit stamping
    Dtos/            API boundary types — entities never serialize directly
  Api.Tests/
frontend/
  src/app/
    core/            API clients, models, HTTP interceptor
    shared/          confirm dialog, empty states, skeletons, guard banner
    features/        items · borrowers · checkout · loans · reference
    styles/theme.scss
  e2e/
design/ui-spec/      the design package this build was written against
requirements/        the requirement hierarchy this build realizes
```

---

## Authentication

There is none. A stub middleware treats every request as an authenticated Staff user —
a deliberate, approved scope decision for this pilot, not an oversight. **This build is
safe on Local only.** Promoting it anywhere else means replacing that middleware first.

That is enforced, not just documented: `UseStubAuthentication` **throws on startup** if
the host environment is anything other than Development, and logs a warning on every
Development boot. The application fails to start rather than quietly serving every
anonymous caller as Staff.
