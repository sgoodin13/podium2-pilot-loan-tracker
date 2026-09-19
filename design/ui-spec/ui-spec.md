# LoanTracker — ui-spec.md
**Modules in scope:** LoanTracker (single module). **Source artifacts:** `FunctionalSpec_LoanTracker_LoanTracker.md`, `BusinessProcess_LoanTracker_Checkout.html`, `Business_Requirements/LoanTracker_Business_Requirements_v1.md`. **Visual language:** `LoanTracker_UI_Standard.md` (Angular Material, WCAG 2.2 AA, brand navy `#1a4d8f`).

**Key rulings carried in:** BR-1 enforced at commit time, not just on screen load (SME requirement); item availability is always derived from open-loan state, never a stored field; retire/deactivate hard-blocked while an open loan exists (SME recommendation, BR §8).

---

## Global shell

- **App frame:** left `lt-sidenav` (fixed, 220px) + main content area. Nav items: Items, Borrowers, Checkout, Loans, Item Categories, Loan Statuses.
- **Tenant scoping:** N/A — single-tenant product.
- **Soft delete:** every "delete" action in this product is actually deactivate (`is_active = false`); no hard delete exists anywhere in the UI.
- **Error/dialog handling:** inline at the point of action (per `LoanTracker_UI_Standard.md` §3) — never a generic top-of-page banner. See each screen's error states below.
- **Dirty-state guards:** any unsaved form (Add screens, inline-edit rows in reference data) prompts before navigation away if fields have been touched.
- **Skip link:** every screen provides "Skip to main content" as the first focusable element (WCAG 2.2 AA).

---

## Screens

### `scr-item-list` — Items (Pattern 08)
**Route:** `/items` · **Mockup:** `scr-item-list.html`

| Field | Required | Notes |
|---|---|---|
| Asset tag | — | display only |
| Name | — | display only |
| Category | — | display only, from Item Category |
| Availability | — | **derived**, not stored — Available / On loan |
| Current borrower | — | display only, blank if available |

**Behaviours / BR refs:** search filters by name; category and availability filters, both optional (BR §5.1). Row click → `scr-item-detail`.

### `scr-item-add` — Add item (Pattern 04)
**Route:** `/items/new` · **Mockup:** `scr-item-add.html`

| Field | Required | Notes |
|---|---|---|
| Name | yes | text |
| Description | no | textarea |
| Asset tag | yes | must be unique — server-validated (BR §5.1) |
| Category | yes | select, from Item Category reference data |

**Behaviours / BR refs:** duplicate asset tag rejected with an inline, specific error (BR §5.1).

### `scr-item-detail` — Item detail (Pattern 12)
**Route:** `/items/:id` · **Mockup:** `scr-item-detail.html`

| Field | Required | Notes |
|---|---|---|
| Name, Description, Asset tag, Category, Active | — | editable via Edit action |
| Current loan (borrower, checked-out date) | — | shown only if an open loan exists |
| Loan history (child grid) | — | read-only, most recent first |

**Behaviours / BR refs:** Retire button disabled with a stated reason while an open loan exists — hard block, per Priya's SME recommendation (BR §8, resolved).

### `scr-borrower-list` — Borrowers (Pattern 08)
**Route:** `/borrowers` · **Mockup:** `scr-borrower-list.html`

| Field | Required | Notes |
|---|---|---|
| Name, Department, Contact, Open loans | — | display only |

**Behaviours / BR refs:** search by name (BR §5.2). Row click → `scr-borrower-detail`.

### `scr-borrower-add` — Add borrower (Pattern 04)
**Route:** `/borrowers/new` · **Mockup:** `scr-borrower-add.html`

| Field | Required | Notes |
|---|---|---|
| Name | yes | text |
| Contact email | no | email |
| Contact phone | no | tel |
| Department/Group | no | free text |

### `scr-borrower-detail` — Borrower detail (Pattern 12)
**Route:** `/borrowers/:id` · **Mockup:** `scr-borrower-detail.html`

| Field | Required | Notes |
|---|---|---|
| Name, Contact email/phone, Department, Active | — | editable via Edit action |
| Loans (child grid) | — | read-only, current + historical |

**Behaviours / BR refs:** Deactivate button disabled with a stated reason while an open loan exists — hard block, per Priya's SME recommendation (BR §8, resolved).

### `scr-loan-list` — Loans (Pattern 08)
**Route:** `/loans` · **Mockup:** `scr-loan-list.html`

| Field | Required | Notes |
|---|---|---|
| Item, Borrower, Checked out, Returned, Status | — | display only |

**Behaviours / BR refs:** filterable by status (open/closed), borrower, item (BR §5.4). Row click → `scr-loan-detail`.

### `scr-loan-detail` — Loan detail (Pattern 15)
**Route:** `/loans/:id` · **Mockup:** `scr-loan-detail.html`

| Field | Required | Notes |
|---|---|---|
| Item, Borrower, Checked-out At | — | display only |
| Status | — | Checked Out, or a terminal status once closed |
| Resulting status (on return) | yes | select, **terminal statuses only** |

**Behaviours / BR refs:** Return action available only while open; requires a terminal Loan Status to confirm (BR §6). Once closed, Return action disappears and Returned At + resulting status display.

### `scr-checkout-wizard` — Checkout (Pattern 06)
**Route:** `/checkout` · **Mockup:** `scr-checkout-wizard.html` · **screen_binding:** declare (Checkout process, registers into the module registry)

| Field | Required | Notes |
|---|---|---|
| Borrower (step 1) | yes | select, active borrowers only |
| Item (step 2) | yes | select, items with zero open loans only, at time of listing |
| Resulting Loan | — | created on successful confirm |

**Behaviours / BR refs:** **step 2's list is a snapshot, not a guarantee** — availability is re-validated atomically at commit (BR-1, the filtered unique index in the Physical Data Model). On success: Loan created, confirmation names the specific asset tag. On failure: rejection panel shown with a specific reason, nothing persisted — the genuine negative-path case (BR §2 pilot scope, §8 SME note).

### `ref-item-category` — Item Categories (Pattern 18)
**Route:** `/reference/item-categories` · **Mockup:** `ref-item-category.html`

| Field | Required | Notes |
|---|---|---|
| Name | yes | must be unique |
| Description | no | text |
| Active | yes | toggle, default true |

**Behaviours / BR refs:** deactivate, never delete, if referenced by an active Item (BR §5.5).

### `ref-loan-status` — Loan Statuses (Pattern 18)
**Route:** `/reference/loan-statuses` · **Mockup:** `ref-loan-status.html`

| Field | Required | Notes |
|---|---|---|
| Name | yes | must be unique |
| Description | no | text |
| Is Terminal | yes | toggle — governs whether this status can close a loan |
| Active | yes | toggle, default true |

**Behaviours / BR refs:** deactivate, never delete, if referenced by an active Loan (BR §5.5). Staff can add new terminal statuses (e.g. "Returned – Needs Repair") without a code change (BR §4).

---

## Backend tasks with no UI

None beyond what's listed above — every backend operation in this product has a corresponding screen action (retire, deactivate, checkout commit, return commit, reference CRUD).

## Cross-screen states

- **Loading:** skeleton rows on the three list screens; a centered spinner on Item/Borrower/Loan detail and the Checkout wizard.
- **Empty:** "No items yet — add your first item" (zero data) vs. "No results match your filters" (filtered-to-zero) — visually and textually distinct, per `LoanTracker_UI_Standard.md` §3.
- **Error:** inline at the point of action — the checkout rejection panel is the canonical example.
- **Saving:** primary button shows a pending/disabled state during submit (per `LoanTracker_UI_Standard.md` §2 — disabled doubles as pending).
- **Validation:** inline under each field, per `LoanTracker_UI_Standard.md` §2.
- **Dirty-state guard:** confirm-before-navigate on any form/inline-edit row with unsaved changes.

---
*LoanTracker — `Pre-Build/design/ui-spec/ui-spec.md`. Companion: `patterns-applied.md`, `index.html`, 11 screen mockups, `assets/mockups.css`.*
