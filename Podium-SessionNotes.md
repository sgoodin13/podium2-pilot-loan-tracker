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
| A1 — do C1/C2/C7 apply? | **N/A for this pilot**, recorded as an explicit ruling per Standards Guide §9 | The three list grids are plain `MatTable` + `MatSort` + `MatPaginator` with server-side paging. No grid preference wrapper, no `GridPreferenceService`, no session-timeout modal. C3 (destructive-action confirmation) and C6 apply and are implemented. **C4 and C5 were claimed implemented here and are not — see the correction below** |

### Correction — C4 and C5 were claimed implemented and are not (Compliance findings F4, F13)

The row above originally read *"C3, C4, C5 and C6 **do** apply and are implemented."*
That was wrong for two of the four, and it is corrected rather than quietly amended
because a later run would otherwise trust it.

| Convention | Claimed | Actual | Needs |
|---|---|---|---|
| **C4 — internationalization** | implemented | **absent.** No `$localize`, no `i18n` attributes, no translation library, no locale files; `angular.json` carries only the CLI's default `extract-i18n` target with nothing marked for extraction. Every user-facing string is hardcoded in templates and in C# service code. C4's "no string concatenation for sentences" rule is also violated systematically in both languages. | An Orchestrator ruling: implement C4, or record it N/A for a single-locale internal pilot |
| **C5 — entity-local timestamps** | implemented | **not as specified.** Every timestamp renders through Angular's `date` pipe with no `timezone` argument, so the stored `timestamptz` is silently converted to the *viewing browser's* zone, and no timezone label appears anywhere. C5 requires the entity's own zone, always labelled, never silently converted. | An Orchestrator ruling. C5's own scope clause — *"in a multi-location system"* — makes N/A defensible for a single-location pilot |

Neither is a code defect introduced by the build; both are **inaccurate self-reporting**,
which is the more dangerous of the two because it is what the next run reads. Unlike
C1/C2/C7, neither carries a ruling — they were affirmatively asserted as done.

**Developer did not self-rule either one.** Recording an N/A for a convention is an
Orchestrator act under Standards Guide §9, and inventing one here would repeat the exact
error being corrected.
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

---

## Compliance/Security Engineer pass (post-Gate-4)

Spawned per CLAUDE.md after Gate 4. All nine checklist lines covered. **13 findings, one
environment gap.** Findings are surface-only: the agent reports, Developer fixes, the
agent re-verifies. Every claim below was checked against disk before it was acted on —
one did not survive that check, and it is recorded as rejected rather than dropped.

### Fixed and verified (9)

| # | Finding | Fix |
|---|---|---|
| F1 | Stub auth had **no environment guard at all** — its own remarks said promoting it beyond Local would be a genuine vulnerability, and nothing enforced that | `UseStubAuthentication` now resolves `IWebHostEnvironment` and **throws on boot** outside Development, plus a `LogWarning` on every Development start. Confirmed live in the API log. |
| F2 | Loan-status reference edits were **completely unguarded**: renaming or deactivating "Checked Out" from a maintenance screen breaks *every checkout in the product*, and deactivating the last terminal status leaves every open loan permanently uncloseable | `GuardReferencedStatusAsync` — the checkout status (matched on its **stable seeded id**, not its editable name) cannot be renamed, deactivated or marked terminal; a status held by an open loan cannot be deactivated; the last active terminal status cannot be removed. 8 new tests. |
| F3 | Working DB password committed in `appsettings.Development.json` **and hardcoded in C# source**, against an unconditional stack rule | Connection string moved to `dotnet user-secrets`; the design-time factory now **requires** its env var (the fallback bought nothing — the override already existed); compose credentials moved to a gitignored `.env` with a committed `.env.example`. No committed default, because a committed default is still a committed credential. |
| F5 | The category-deactivation effect was computed and then **discarded into a server log** the user never sees | `ActiveItemCount` returned on the response via one batched query; the UI confirms at Tier 2, naming the count, before the save. |
| F6 | Focus lost on three state changes that destroy the focused control — wizard steps, return panel, item edit mode — plus one `ConfirmDialog` call site missing `restoreFocus` | Focus moved deliberately on every transition; `restoreFocus: true` added. **axe cannot catch this** — it is exactly the portion of WCAG the automated gate does not cover. |
| F7 | Borrower **name** logged at Information on every checkout, and `DomainException.Detail` echoed at Warning — details are built from entity names, so PII reached the log by a second, less obvious path | Checkout logs `{BorrowerId}` only; the middleware logs Title + status code and routes Detail to `Debug`. |
| F8 | **Return is the product's one irreversible action and had its weakest confirmation** — a generic "Return this item" naming nothing, while the *reversible* retire and deactivate both got full dialogs naming the record | The panel now names the item and asset tag, names the borrower, and states that the loan cannot be reopened. |
| F9 | `docs/Podium-SecurityFindings.md` understated the production-reachable Angular advisories as **one**; there are ten | Corrected, with the full advisory table and — more usefully — exposure evidence that covers all ten. See Scan 3 in that file. |
| F4/F13 | C4 (i18n) and C5 (entity-local timestamps) **claimed implemented in these very notes and absent from the code** | Claims corrected above. The conventions themselves need an Orchestrator ruling — see below. |

### F12 — I rejected this, and I was wrong

I reported that a claimed fourth SSH.NET advisory "did not reproduce." It reproduces on
every run. `dotnet list package --vulnerable` prints a second advisory for the same
package on a **continuation line** with the package and version columns blank, and the
grep I used to read the output dropped that line. Counting package rows gives 3;
counting advisories gives 4.

The correct figure is **3 vulnerable packages carrying 4 high advisories**, all
test-only. `docs/Podium-SecurityFindings.md` is corrected in Scans 2 and 3, including a
note about the filtering mistake — the same grep would hide the same class of advisory
again. `Api`, the shipped service, remains clean.

### Re-verification round 2 — the fixes were audited, and two did not hold

The Compliance/Security Engineer re-verified every fix against disk rather than against
my description, re-ran all three test layers independently, and found two real problems
in my own work:

| # | What was wrong | Fix |
|---|---|---|
| **F5 regression** | `ItemCategoryResponse.ActiveItemCount` defaulted to `0`, and only `ListAsync` passed a real value. The frontend overwrites its row from the save response, so editing a category's *description* zeroed the count client-side and **silently disarmed the deactivation confirmation for the next edit of that row** — reintroducing exactly what F5 existed to prevent. My test missed it by asserting the list endpoint at both ends, exercising the one call site that was correct. | Removed the default so the compiler finds every call site; `CreateAsync` passes `0` explicitly, `UpdateAsync` resolves the real count. New test asserts the **update** response. |
| **F6 incomplete** | The checkout `confirm()` error handler had no focus move. The step does not change there, so the step-transition handler never fired — but the confirm button is still destroyed and replaced by the rejection panel. The single most important interaction in the product, and the one a keyboard user has just triggered. | Focus moved to the rejection panel itself, which carries the reason and contains the only recovery control. |

**And the F6 fixes themselves did not work.** Four `toBeFocused()` assertions were added
at the auditor's suggestion. **All four failed on first run.** The `queueMicrotask` +
`@ViewChild` pattern loses the race with Angular's change detection; the optional
chaining turned every miss into a silent no-op. The code read as correct, the markup was
faultless, and **axe reported a clean page throughout** — automated a11y scanning cannot
see this class of defect at all.

Two further rounds were needed before they passed:

1. `queueMicrotask` → a macrotask (`focusWhenRendered`), which fixed the return panel and
   item edit mode but not the wizard.
2. The wizard's three sibling `@if` branches all carried the same `#stepPanel` ref, and
   the query did not refresh across a branch swap — stepping backwards focused a detached
   element. Replaced with `ltFocusOnCreate`, a directive that binds focus to each
   element's own lifecycle, removing the timing question entirely. Step 1 binds it to a
   `navigated()` signal so it does not steal focus on initial render and defeat the skip
   link.

Three of the four focus fixes shipped in the previous commit did nothing. The only reason
that is known is the tests — which is the finding worth carrying: **an a11y fix with no
`toBeFocused()` assertion is an unverified claim**, and the green axe run says nothing
about it either way.

### One process note, recorded because it nearly caused a false conclusion

Two Playwright runs in this session produced failures that were **not** code defects: the
Angular dev server had died in one case, and was still recompiling in the other
(`ng build` writes `dist/` — it does not wait for `ng serve`). Both times the honest first
reading was "the change broke it." Re-running after confirming the server state is the
correct discipline; reporting the passing retry without stating why it was retried is not.

### Carried for the Orchestrator (3)

| # | Finding | Why Developer did not act |
|---|---|---|
| F4 / F13 | **C4 and C5 need a ruling** — implement, or record N/A for a single-locale, single-location internal pilot | Recording an N/A for a convention is an Orchestrator act under Standards Guide §9. Self-ruling here would repeat the exact error being corrected. |
| F10 | **No CI pipeline exists**, so Standard #13 — the a11y checker running as a build gate on every PR, *"not a separate manual audit phase bolted on after modules are done"* — is not met. The scans are thorough but run only when a person types the command. | Adding a workflow commits the repo to an outward-facing pipeline and to CI minutes. It also needs a headless override, which collides with the headed/`slowMo` config the stack rules require for the Runtime Validation watch-run. That collision is a real decision, not a mechanical one. |
| F11 | **The C1/C2/C7 N/A ruling lives in these session notes, not in the trigger spec** where `Podium2_Standards_Guide.md` requires it. The trigger spec's five `[RULING: …]` entries do not cover it. | Build agents do not edit delivered or methodology artifacts mid-run. This is the concrete instance of carried finding #7. |

### Environment gap — SonarQube/SonarCloud not provisioned

Probed before asserting unavailable: no `sonar-scanner`/`dotnet-sonarscanner` on PATH, no
global tool, no `sonar-project.properties`, no sonar reference in any project file, no
local image. `LoanTracker_Stack_Rules.md` §QUALITY_SCAN_TOOL required the SonarQube vs
SonarCloud choice be *"recorded at Stage 6 kickoff"* — it was never recorded and neither
was provisioned. **The hotspots / vulnerabilities / smells / duplication / complexity
checklist line is unexecuted.** Nothing was installed and no output was fabricated. This
needs an Orchestrator decision — provision it, or record an explicit deferral. It should
not be carried as silently satisfied.

### Re-verification after the fixes

| Layer | Result |
|---|---|
| `dotnet build` / `ng build` | clean, 0 warnings |
| Backend (xUnit + Testcontainers) | **101 passed, 0 failed** — 92 before, +9 new F2/F5 guard tests |
| Frontend unit (Jasmine/Karma) | **29 passed, 0 failed** |
| E2E + a11y (Playwright, headed, single worker) | **39 passed, 0 failed**; 39 skipped by project guard — 35 before, +4 new focus-management specs |
| a11y scans | 18 desktop + 2 mobile, **0 WCAG 2.2 AA violations** — count unchanged after the F6/F8 markup changes |

One honest note on that run: the first pass reported 13 failures. The cause was the
Angular dev server having died, not the changes — every failure was
`ERR_CONNECTION_REFUSED` at `page.goto`. Restarted and re-ran clean. Recorded because
"it passed on the retry" is only trustworthy when the reason for the retry is stated.

### Confirmed clean by the audit, with evidence

- **BR-1 cannot be bypassed.** `LoanService.CheckoutAsync` is the only insert path for a
  loan; no availability pre-check substitutes for the constraint; the catch matches on
  SQLSTATE **and** constraint name, never on message text.
- **No hard delete is reachable.** Zero `Remove`/`RemoveRange`/`ExecuteDelete`/raw SQL in
  `backend/Api`; no `[HttpDelete]` anywhere; the interceptor converts stray deletes.
- **Injection: clean.** No concatenated or interpolated SQL. Sort keys never reach SQL —
  each repository maps through a hard-coded switch with a safe default. Paging is clamped.
- **Output encoding: clean.** No `innerHTML`, `bypassSecurityTrust*`, `DomSanitizer` or
  `eval` in `frontend/src` — re-verified directly, and load-bearing for the F9 ruling.
- **Migration bootstrap guard is sufficient** — Development-only, and
  `ASPNETCORE_ENVIRONMENT` defaults to Production when unset.
- **Contrast pairings recomputed** rather than taken from the code comments: all five
  pass AA.
- **The nine QA defect fixes are real**, and the five inverted specs assert correct
  behaviour with out-of-band verification rather than merely passing.

### One latent observation, not currently a defect

A soft-deleted **open** loan would remain under `ux_loans_item_open` (the index filters on
`returned_at`, not `is_active`) while disappearing from every repository read — the item
would read "Available" while the database refused to check it out. **No code path can
soft-delete a loan today**, so this is not reachable. Recorded because it would become
reachable the moment loan soft-deletion is ever added.
