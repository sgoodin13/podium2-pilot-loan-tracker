# LoanTracker — Session Notes

**Session:** 1 — greenfield build (Stage ⑥) · **Date:** 2026-09-19
**Agent:** Developer · **Model:** `claude-opus-5` · **Intake:** `new-build`
**Branch:** `worktree-loantracker-pilot-build` → delivers to `feature/20260919-loantracker-pilot`

---

## Binding facts carried from the KB and Gate 1 artifacts

Echoed here as the session's source of truth, per CLAUDE.md startup step 2a. These were
read once, from `Products/LoanTracker.json`, the Physical Data Model and the trigger
spec — and were **not re-derived** at any point during the build.

| Fact | Value |
|---|---|
| Tenant key | `null` — single-tenant. No discriminator column, no global query filter |
| DB engine | PostgreSQL 17, single schema `public` |
| Stack | Angular 18 + Angular Material 18 · .NET 8 Web API + EF Core 8 · Docker for local dev |
| Topology | Single service, single schema — explicitly not ASIM's 7-microservice shape |
| Primary key | `uuid`, default `gen_random_uuid()` |
| Soft delete | `is_active` on every table. No hard delete, no exceptions recorded |
| Audit columns | `created_at`/`created_by` (not null), `modified_at`/`modified_by` (null), plain `text` |
| BR-1 | Enforced by `ux_loans_item_open`, a filtered unique index — never app-layer only |
| Auth | Stub middleware, one Staff role, no login flow |
| Retire / deactivate | Hard block, not a warning (SME Priya Anand, BR §8, resolved) |
| Availability | Derived from open-loan existence, never a stored column |
| Domain amplification | N/A — low-stakes internal tool; findings default to Tier 2 |
| Platform inheritance | none — greenfield |

---

## Gate 3 rulings applied

| Question | Orchestrator ruling | How it landed |
|---|---|---|
| A1 — do C1/C2/C7 apply? | **N/A for this pilot**, recorded as an explicit ruling per Standards Guide §9 | The three list grids are plain `MatTable` + `MatSort` + `MatPaginator` with server-side paging. No grid preference wrapper, no `GridPreferenceService`, no session-timeout modal. C3 (destructive-action confirmation), C4, C5 and C6 **do** apply and are implemented |
| A2 — checkout confirmation copy | Approved as proposed | `Checked out — {Item Name} ({ASSET-TAG}) to {Borrower}.` as a `MatSnackBar`; step-3 button reads `Confirm checkout`. The asset-tag naming pattern is applied to every item, and to the return confirmation too |
| A3 — batching | **One continuous pass**, single Gate 4 | Phases 0–5 built end to end without an interim checkpoint |

---

## What was built

25 components (Technical Architecture §8), realizing 14 child requirements across
11 screens and 5 tables. See `Podium-ModuleStatus.md` for the per-requirement and
per-component breakdown.

**Decisions recorded per Stack Rules:**
- **Frontend unit framework: Jasmine/Karma** (Angular CLI default). `LoanTracker_Stack_Rules.md`
  §TEST_STACK_RULES defers this to Stage 5 kickoff and requires it be recorded — this is
  that record. Jest was not adopted; the two are not mixed.
- **Ports (committed config, no artifact decided them):** API `5080`, frontend `4200`,
  Postgres host `5433`. Committed in `launchSettings.json`, `angular.json`,
  `docker-compose.yml`; `proxy.conf.json` routes `/api` → 5080.
- **Run-scoped dev database:** `loantracker_dev_20260919_pilot`, not a fixed shared name.

---

## Subagents used

All at `claude-opus-5` — the locked model. None tiered down.

| Role | Scope | Verified against disk |
|---|---|---|
| Database Engineer | 5 entities, `AppDbContext`, configurations, 5 repository pairs, migration, seed | Yes — read the migration, confirmed the `filter:` argument, then confirmed the physical index in the live database via `pg_indexes` |
| Item Management screens | `scr-item-list` / `-add` / `-detail` | Yes — read the files, checked the testid contract, confirmed the retire guard is enforced in code and not only in the template |
| Borrower Management screens | `scr-borrower-list` / `-add` / `-detail` | Yes — same checks |
| QA Engineer | Three test layers + a11y | See "Test results" below |
| Methodology / architecture / UI-spec readers | Bulk KB reads at Gate 3 | Key claims spot-checked on disk before use |

**Two subagent findings were corroborated and acted on, not taken on trust:** the Item
and Borrower agents independently reported that list sorting was sent by the frontend and
silently discarded by the API. Confirmed on disk, then fixed.

---

## Bugs found and fixed in-session

Per Standards Guide #6 these are **bugs**, not defects — found during the build session,
before the work item was complete, so fixed in-session rather than tracked.

1. **Sorting was a visible lie.** Both list screens sent `sortBy`/`sortDir`; no controller
   bound them and no repository accepted them, so the sort arrow moved and the data did
   not. Threaded sort through controllers → services → repositories with a hard-coded
   allow-list per repository, so no caller-supplied string reaches SQL. Verified live:
   `sortBy=name&sortDir=desc` returns Tripod first, `asc` returns Circular Saw.
2. **N+1 on the borrower list.** The "Open loans" column ran one count query per row.
   Replaced with a single batched `GetOpenLoanCountsByBorrowerAsync`, the sibling of the
   existing item-availability batch query.
3. **Missing dirty-state guard.** `ui-spec.md` requires confirm-before-navigate on a
   touched unsaved form; only the in-screen Cancel buttons had it. Added a shared
   `unsavedChangesGuard` (`CanDeactivate`) wired to the add and detail routes.
4. **Material 18 M2 API rename.** `mat.define-palette` and friends are `mat.m2-*` in v18.
5. **`@else if (expr; as alias)` is invalid Angular** — the `as` alias is only legal on a
   leading `@if`. Hit in the loan detail template and, independently, in the item detail
   template. Both restructured to a nested `@if`.
6. **EF Core duplicate `HasIndex` silently erased the BR-1 filter.** Caught by the
   Database Engineer: calling `HasIndex(l => l.ItemId)` twice returns the *same* index
   builder, so the second call renamed the filtered unique index and dropped its filter.
   Fixed with the two-argument overload. This is why the index is asserted physically
   against `pg_indexes`, not just trusted from the model.

---

## Runtime validation

Run against the app started the way a developer starts it — `dotnet run` + `ng serve`,
not the test harness — with Playwright headed, single-worker and `slowMo: 250` from
committed config.

| CLAUDE.md step | Result |
|---|---|
| 1. Started as a real user would | ✅ `docker compose up -d` → `dotnet run` (5080) → `ng serve` (4200) |
| 2. Chained spec end to end against live servers | ✅ `checkout-flow.spec.ts` passes in 11.6s, headed |
| 3. Created through the UI, not by seeding | ✅ Item and Borrower both created through their real forms |
| 4. Checked-out Item status and Loan render live | ✅ Loan appears on the Loan list with a `Checked Out` chip; item flips to `On loan` |
| 5. Successful path demonstrated | ✅ Full checkout → return cycle; item returns to `Available` with 1 closed loan in history |
| 6. Blocked path demonstrated live | ✅ Second checkout of the same item is visibly rejected, and an out-of-band fetch confirms **exactly 1 loan** persisted |
| 7. As-run ports match committed config | ✅ verified below |
| 8. Recorded here | ✅ this section |

**Port reconciliation (step 7), checked rather than assumed:**

| | Committed | As-run |
|---|---|---|
| API | `launchSettings.json` → `http://localhost:5080` | HTTP 200 on 5080 |
| Proxy target | `proxy.conf.json` → `http://localhost:5080` | `/api` through 4200 returns 200 |
| Frontend | `angular.json` → port `4200` | HTTP 200 on 4200 |
| Postgres | `docker-compose.yml` → `5433:5432`; connection string `Port=5433` | `docker port` → `0.0.0.0:5433` |

**Seeded data drives the canonical path:** the seed provides the four loan statuses and
three categories the wizard and return panel depend on; the Item and Borrower used in the
flow are created through the UI during the run, not seeded.

**The E2E suite genuinely ran** — 35 passed, 0 failed, 35 skipped (project guards:
desktop flows skip on `mobile-chrome`, the mobile floor skips on desktop).

---

## Test results — all three layers green

| Layer | Framework | Result |
|---|---|---|
| Backend unit + integration | xUnit + Testcontainers (real Postgres 17) | **92 passed, 0 failed** |
| Frontend unit | Jasmine/Karma | **29 passed, 0 failed** |
| E2E | Playwright (headed, 1 worker) | **35 passed, 0 failed**, 35 skipped by project guard |
| Accessibility | `@axe-core/playwright`, WCAG 2.2 AA | **18 scans, 0 violations** — shell, dialog, snackbar + all 11 screens |

**Performance:** loan list at 500 seeded loans returns a 25-row page within a stated
1500 ms threshold; every row carries its joined item/borrower/status, proving no N+1.

**BR-1 is asserted four ways:** the physical index definition from `pg_indexes` (so a
dropped filter cannot slip through), the `23505` violation carrying
`ConstraintName == ux_loans_item_open`, that exactly one loan survives the rejected
insert, and that a re-loan succeeds once the first is returned.

**Concurrency caveat, stated plainly:** `Podium2_Template.md:246` bars concurrency and
fault-injection testing, so BR-1 is proven **sequentially** — at the database, through
the service, and through the wizard's stale-snapshot path. A genuinely simultaneous
double-checkout was not executed. The guarantee rests on the filtered unique index being
real, which is asserted directly.

---

## QA defects found and fixed in this session

QA Engineer surfaced 9 real defects. All 9 were fixed by Developer and re-verified;
the tests QA had written to pin the defective behaviour were inverted to assert the
correct behaviour rather than deleted.

| # | Defect | Fix |
|---|---|---|
| D4 | **Add-borrower created duplicate records.** `save()` navigated without `markAsPristine()`, so the unsaved-changes guard blocked navigation away from an already-saved form; pressing Save again wrote a second row. Reproduced, not theorised | `this.form.markAsPristine()` on success |
| D9 | **Two critical a11y violations on the loan list.** `role="button"` on `<tr mat-row>` destroyed the table's ARIA structure (`aria-required-children`, `aria-required-parent`) | Removed the role; `tabindex` + `aria-label` + keydown already gave keyboard operability |
| D6 | **Contrast failures on all 11 screens.** 5 distinct pairs below 4.5:1 | `--muted` body copy moved to `--mid` (the UI Standard already restricted `--muted` to decorative use); darker `--good-on-tint` and `--danger-fill` variants added for tinted/filled surfaces; snackbar action set to white |
| D7 | **No reflow at 393px** — `/items` overflowed the page by 197px | Tables scroll inside their own card; filters stack full-width; page no longer scrolls horizontally |
| D8 | **Checkout wizard unusable on a phone** — step-2 Select buttons were intercepted by neighbouring elements | Same root cause as D7; wizard nav stacks and controls go full width |
| D5 | **Item list "Current borrower" always blank.** The page projection hard-coded null | Added `GetOpenLoanHoldersByItemAsync` — one query per page, sibling of the existing availability batch, so the column is filled without an N+1 |
| D2 | **Two guard branches were unreachable dead code.** Lookups defaulted to `includeInactive: false`, filtering a retired item / deactivated borrower out one line before its own guard, so callers got a misleading 404 | `includeInactive: true` on both lookups; the real 422 reasons now fire |
| D3 | **The two error paths disagreed on media type.** Validation failures left as `application/json` | `ProblemJsonResult` writes the response explicitly instead of negotiating it; both paths now emit `application/problem+json` |
| D1 | **`[Required]` on a non-nullable `Guid` could never fail**, so an empty checkout body surfaced as a confusing 404 | Ids made nullable so the attribute works; empty body now returns 400 with field errors |

**QA's own two test bugs** (a Material radio host that isn't the click target, and a
debounced search read before it settled) were corrected by QA as test faults, not by
weakening assertions. No test was re-run until green. No flaky markers were needed.

---

## Findings carried to Gate 4

1. **Angular 18 production CVE** — `GHSA-hh8m-fm6v-7cvg`, sanitization bypass, present in
   every Angular 18 release. npm's only fix is `@angular/core@22`, four majors from the
   Gate 1-approved stack, which is an architecture decision Developer does not make.
   Built on Angular 18 as approved. `docs/Podium-SecurityFindings.md`, marker `live-owed`.
2. **`/cost` could not be captured** — it is an interactive slash command and a background
   agent turn cannot invoke it. `Podium-RunLedger.md` records "not captured" rather than a
   blank or an invented figure, and needs the Orchestrator to append the real number. A
   hard gate a background Developer session structurally cannot satisfy is itself a
   methodology finding.
3. **Frontend dependency re-check incomplete** — the npm advisory endpoint was returning
   503 during Phase 6. Recorded as not-re-checked, not as clean.
4. **PDM editorial gap** — the `loans` column table omits `is_active`, while the PDM's own
   Gate 1 decisions block requires it on every table. Built per the ruling; the artifact
   may want correcting so it and the schema read the same.
5. **`Podium2_Standards_Guide.md` cross-reference is stale** — CLAUDE.md points there for
   the test volume floor/ceiling; the rule actually lives at `Podium2_Template.md:244`.
6. **CLAUDE.md component count** — says "≈14 components"; Technical Architecture §8 says
   25. Built to 25; the 14 is the child-requirement count.
7. **C1/C2/C7 have no "no-identity product" escape hatch** — ruled N/A here (Gate 3 A1),
   but a stub-auth product will hit the same three conventions on every future run.
8. **The UI Standard's contrast table only checked colours against white.** Three pairings
   this build actually uses — tinted chip backgrounds, filled destructive buttons, and
   muted text on the page background — were never in the table and failed AA. Worth
   extending §1 rather than leaving each build to rediscover it.
9. **Test-only dependency vulnerabilities** — 3 high in `Api.Tests` via
   `Testcontainers.PostgreSql`. The shipped `Api` project has none. Recommend accepting:
   Testcontainers is what makes BR-1 testable against real Postgres.
