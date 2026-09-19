# LoanTracker — Requirements Hierarchy
**Delivered to:** `requirements/loantracker/` at Stage ④. **Parents** = in-scope BR capability areas; **children** = Functional Spec elements + Checkout process tasks. Status column: Developer flips `Resolved` at task-complete; Orchestrator flips `Resolved → Closed` post-merge.

**Stage ⑧ close-out, 2026-09-19 — all 19 rows (5 parents, 14 children) set to `Closed`**, on
Orchestrator authorization after Gate 5 sign-off and the live walkthrough.

> **Recorded honestly:** these rows were never flipped to `Resolved` during the build. The
> `Resolved` state was tracked in `Podium-ModuleStatus.md` instead, and this file was left
> at `Open` throughout — so the two artifacts disagreed for the whole run and nothing
> caught it until close-out. They went `Open → Closed` in one step. The end state is
> correct; the intermediate state was never recorded here, and a reader reconstructing the
> run from this file alone would have seen nothing happen until the very end.
> Worth a methodology note: the status lives in two places with no check that they agree.

---

## Parents

| Path | Title | Source BR | Status |
|---|---|---|---|
| REQ-1 | Item Management | BR §5.1 | Closed |
| REQ-2 | Borrower Management | BR §5.2 | Closed |
| REQ-3 | Checkout | BR §5.3, §6 (BR-1) | Closed |
| REQ-4 | Loan Tracking | BR §5.4 | Closed |
| REQ-5 | Reference Data | BR §5.5 | Closed |

## Children

| Path | Parent | Title | Type | Process | Module | Screen / pattern | Acceptance source | intake_class | Status |
|---|---|---|---|---|---|---|---|---|---|
| REQ-1.1 | REQ-1 | Item list/search | Task | — | LoanTracker | `scr-item-list` / Pattern 08 | FunctionalSpec `scr-item-list` | new-build | Closed |
| REQ-1.2 | REQ-1 | Add item | Task | — | LoanTracker | `scr-item-add` / Pattern 04 | FunctionalSpec `scr-item-add` | new-build | Closed |
| REQ-1.3 | REQ-1 | Item detail (incl. retire guard) | Task | — | LoanTracker | `scr-item-detail` / Pattern 12 | FunctionalSpec `scr-item-detail` | new-build | Closed |
| REQ-2.1 | REQ-2 | Borrower list/search | Task | — | LoanTracker | `scr-borrower-list` / Pattern 08 | FunctionalSpec `scr-borrower-list` | new-build | Closed |
| REQ-2.2 | REQ-2 | Add borrower | Task | — | LoanTracker | `scr-borrower-add` / Pattern 04 | FunctionalSpec `scr-borrower-add` | new-build | Closed |
| REQ-2.3 | REQ-2 | Borrower detail (incl. deactivate guard) | Task | — | LoanTracker | `scr-borrower-detail` / Pattern 12 | FunctionalSpec `scr-borrower-detail` | new-build | Closed |
| REQ-3.1 | REQ-3 | Select borrower | Task | LoanTracker-checkout | LoanTracker | `scr-checkout-wizard` / Pattern 06 | process task `t1` | new-build | Closed |
| REQ-3.2 | REQ-3 | Select available item | Task | LoanTracker-checkout | LoanTracker | `scr-checkout-wizard` | process task `t2` | new-build | Closed |
| REQ-3.3 | REQ-3 | Commit Loan — BR-1 atomic enforcement | Task | LoanTracker-checkout | LoanTracker | `scr-checkout-wizard` | process task `t3` | new-build | Closed |
| REQ-3.4 | REQ-3 | Reject, show reason (negative path) | Task | LoanTracker-checkout | LoanTracker | `scr-checkout-wizard` | process task `t4` | new-build | Closed |
| REQ-4.1 | REQ-4 | Loan list/search | Task | — | LoanTracker | `scr-loan-list` / Pattern 08 | FunctionalSpec `scr-loan-list` | new-build | Closed |
| REQ-4.2 | REQ-4 | Loan detail — return action | Task | — | LoanTracker | `scr-loan-detail` / Pattern 15 | FunctionalSpec `scr-loan-detail` | new-build | Closed |
| REQ-5.1 | REQ-5 | Item Category maintenance | Task | — | LoanTracker | `ref-item-category` / Pattern 18 | FunctionalSpec `ref-item-category` | new-build | Closed |
| REQ-5.2 | REQ-5 | Loan Status maintenance | Task | — | LoanTracker | `ref-loan-status` / Pattern 18 | FunctionalSpec `ref-loan-status` | new-build | Closed |

**14 child requirements**, matching the 11 screens + the 3 non-wizard-shared Checkout tasks (t1/t2 share the wizard with t3/t4 but are distinct requirement lines since each has its own acceptance).

---
*LoanTracker — staged in `Pre-Build/`, delivered to `requirements/loantracker/LoanTracker_Requirements.md` at Stage ④.*
