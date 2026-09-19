# LoanTracker — Module Status

**Product:** LoanTracker (Podium 2 pilot) · **Module:** LoanTracker (single module)
**Session:** 1 — greenfield build · **Date:** 2026-09-19 · **Model:** `claude-opus-5`
**Branch:** `worktree-loantracker-pilot-build` → delivers to `feature/20260919-loantracker-pilot`

Status values: Developer sets `Resolved` at task-complete. The Orchestrator flips
`Resolved → Closed` post-merge.

---

## Requirement status

| Path | Title | Screen / surface | Status |
|---|---|---|---|
| REQ-1.1 | Item list/search | `scr-item-list` | Resolved |
| REQ-1.2 | Add item | `scr-item-add` | Resolved |
| REQ-1.3 | Item detail (incl. retire guard) | `scr-item-detail` | Resolved |
| REQ-2.1 | Borrower list/search | `scr-borrower-list` | Resolved |
| REQ-2.2 | Add borrower | `scr-borrower-add` | Resolved |
| REQ-2.3 | Borrower detail (incl. deactivate guard) | `scr-borrower-detail` | Resolved |
| REQ-3.1 | Select borrower | `scr-checkout-wizard` step 1 | Resolved |
| REQ-3.2 | Select available item | `scr-checkout-wizard` step 2 | Resolved |
| REQ-3.3 | Commit Loan — BR-1 atomic enforcement | `scr-checkout-wizard` step 3 | Resolved |
| REQ-3.4 | Reject, show reason (negative path) | `scr-checkout-wizard` rejection panel | Resolved |
| REQ-4.1 | Loan list/search | `scr-loan-list` | Resolved |
| REQ-4.2 | Loan detail — return action | `scr-loan-detail` | Resolved |
| REQ-5.1 | Item Category maintenance | `ref-item-category` | Resolved |
| REQ-5.2 | Loan Status maintenance | `ref-loan-status` | Resolved |

**14 of 14 child requirements Resolved.** None Closed — that flip is the Orchestrator's,
post-merge.

---

## Component inventory — against Technical Architecture §8 (25 components)

| # | Component | Status |
|---|---|---|
| 1 | PostgreSQL database / schema | Built — 5 tables, 9 indexes, migration generated |
| 2 | Auth stub middleware | Built — `StubAuthenticationMiddleware` |
| 3 | App shell — nav, theming, layout | Built — sidenav, 18 theme tokens, skip link |
| 4 | Item entity | Built |
| 5 | `ItemService` (CRUD + retire guard) | Built |
| 6 | Item list/search screen | Built |
| 7 | Item add screen | Built |
| 8 | Item edit/detail screen | Built |
| 9 | Borrower entity | Built |
| 10 | `BorrowerService` (CRUD + deactivate guard) | Built |
| 11 | Borrower list/search screen | Built |
| 12 | Borrower add screen | Built |
| 13 | Borrower edit/detail screen | Built |
| 14 | Loan entity (incl. filtered-unique-index constraint) | Built |
| 15 | `LoanService` (checkout atomic validation + return) | Built |
| 16 | BR-1 — item-already-on-loan enforcement (DB-level) | Built — `ux_loans_item_open`, verified live |
| 17 | Checkout wizard screen | Built |
| 18 | Loan list/search screen | Built |
| 19 | Loan detail screen (incl. return action) | Built |
| 20 | `ItemCategory` entity | Built |
| 21 | `ItemCategoryService` (CRUD) | Built |
| 22 | Item Category maintenance surface | Built |
| 23 | `LoanStatus` entity (incl. `is_terminal`) | Built |
| 24 | `LoanStatusService` (CRUD) | Built |
| 25 | Loan Status maintenance surface | Built |

**25 of 25 built. Unmatched: 0.**

---

## Gate 1 rulings — honoured, not re-derived

| Ruling | How it lands in code |
|---|---|
| `uuid` PK, `gen_random_uuid()` | `EntityBase.Id`, confirmed in live DDL |
| No tenancy | No discriminator column, no global query filter anywhere |
| Soft delete `is_active` everywhere | On all 5 tables; the audit interceptor converts any stray hard delete into a deactivation |
| snake_case via `UseSnakeCaseNamingConvention()` | No column hand-named |
| Audit columns, plain `text` | Stamped centrally by `AuditStampingInterceptor` |
| Single service, single schema | One API project, one `AppDbContext`, schema `public` |
| BR-1 at the database | `CREATE UNIQUE INDEX ux_loans_item_open ON loans (item_id) WHERE returned_at IS NULL` |
| Auth = stub | `StubAuthenticationMiddleware`, one Staff role |
| Retire/deactivate = hard block | 422 + specific reason; UI disables and states why |
| Availability derived, never stored | No status column on `items`; computed from open loans |
| Migrations generated, never applied to a shared env | One migration; only the Development-only local bootstrap applies it |

---

## Known gaps carried to Gate 4

1. **Angular 18 production CVE** (`GHSA-hh8m-fm6v-7cvg`) — needs an Orchestrator ruling;
   the only fix is a major-version jump away from the approved stack.
   See `docs/Podium-SecurityFindings.md`.
2. **PDM editorial gap** — the `loans` column table omits `is_active` while the PDM's own
   Gate 1 decisions block requires it on every table. Built per the ruling; the artifact
   may want correcting.
3. **Methodology cross-reference** — CLAUDE.md points at the Standards Guide for the
   test volume floor/ceiling; that rule actually lives in `Podium2_Template.md:244`.
4. **CLAUDE.md component count** — says "≈14 components"; the architecture says 25. Built
   to 25.

---

## Post-Gate-4 — Compliance/Security closure

**Signed clean at `b18ac9f`** after four adversarial rounds (13 findings + 1 environment
gap, then 2, then 3, then 1). All closed, ruled on, or carried with an Orchestrator
decision. Gaps 1–4 above are all now ruled — see `trigger_spec_LoanTracker.md` §4.

Final verification, each gate re-run independently by the auditor and matched:

| Gate | Result |
|---|---|
| Backend (xUnit + Testcontainers) | 101 / 101 |
| Frontend unit (Jasmine/Karma) | 29 / 29 |
| E2E + a11y (Playwright, headed, 1 worker) | 44 / 44 — run under concurrent DB mutation by both parties |
| axe WCAG 2.2 AA | 20 scans, 0 violations |

**Two markers that this sign-off does NOT discharge:**

- `live-owed` on the Angular advisory family — Scan 3 is static verification against the
  advisory database and the source tree only, never exercised against a running system.
- **SonarQube unexecuted** — declined by Orchestrator ruling, so a recorded decision
  rather than an omission, but still a real gap in the audit's coverage rather than a pass.

**Two observations carried to the next session, deliberately not fixed** (changing code
after sign-off would invalidate the verification it rests on): `BorrowerRepository`'s
unreachable `department` filter parameter, and a confirmed-clean re-check that the list
sort allow-lists still match the UI's sort headers. Detail in `Podium-SessionNotes.md`.
