# LoanTracker — patterns-applied.md
Stage ③ deliverable — records which pattern each screen uses, the selection logic, deviations, and the two standing decisions. Restates (never re-decides) the control library and WCAG target already set in `LoanTracker_UI_Standard.md`.

---

## Control library — restated

**Angular Material (v18)**, matching `LoanTracker_UI_Standard.md` §2. Every mockup in this folder is authored against that decision (plain HTML/CSS evoking Material's visual language — elevation, rounded controls, the brand palette — since these are review mockups, not build code; the real build uses actual `mat-*` components against the same tokens in `assets/mockups.css`). No screen here contradicts this.

## WCAG 2.2 conformance target — restated

**Level AA**, matching `LoanTracker_UI_Standard.md` §4. No per-screen exceptions recorded. Every mockup includes a skip link, visible focus affordances via the shared stylesheet, and `aria-label`s on icon-only or ambiguous controls (see each screen's a11y note).

---

## Selection logic

| Screen shape | Driver | Pattern |
|---|---|---|
| Root entity, no sub-entities, browse-and-select | Item, Borrower, Loan are all root-level, uniform collections | **Pattern 08** (Simple flat grid) for their list screens |
| Single entity, no child collection at create time | Item and Borrower both create without any related-entity step | **Pattern 04** (Single record form) for Add screens |
| Root entity with a read-only related collection | Item/Borrower detail needs to show loan history without owning it as a true parent-child aggregate | **Pattern 12** (One-to-many list) |
| Entity with a real two-state lifecycle and one gated transition | Loan moves Checked Out → a terminal status, once, via one action | **Pattern 15** (Approval/status workflow), adapted to a two-node lifecycle |
| Multi-step creation where step 1 must complete before step 2 is meaningful | Checkout: borrower and item are both required, in order, before commit | **Pattern 06** (Wizard/stepped form) |
| Simple code/description reference data, bulk-maintained | Item Category and Loan Status are both small, Staff-editable lookup sets | **Pattern 18** (Simple lookup table) |

## Screen → pattern map

| Screen id | Pattern | Driver |
|---|---|---|
| `scr-item-list` | 08 — Simple flat grid | Uniform Item collection |
| `scr-item-add` | 04 — Single record form | No sub-entity at create time |
| `scr-item-detail` | 12 — One-to-many list | Item + read-only Loan history |
| `scr-borrower-list` | 08 — Simple flat grid | Uniform Borrower collection |
| `scr-borrower-add` | 04 — Single record form | No sub-entity at create time |
| `scr-borrower-detail` | 12 — One-to-many list | Borrower + read-only Loan history |
| `scr-loan-list` | 08 — Simple flat grid | Uniform Loan collection |
| `scr-loan-detail` | 15 — Approval/status workflow | Loan's Checked Out → terminal-status transition |
| `scr-checkout-wizard` | 06 — Wizard/stepped form | Borrower → Item → Confirm, each step gating the next |
| `ref-item-category` | 18 — Simple lookup table | Bulk-maintained reference data |
| `ref-loan-status` | 18 — Simple lookup table | Bulk-maintained reference data, extended with Is Terminal |

## Deviations

None. All 11 screens use a pattern from `Podium2_UI_Patterns.html` as-is (Pattern 15 is described as adaptable to "any entity with a StatusID FK where valid transitions are defined" — Loan's two-node Checked-Out/terminal lifecycle is a direct, not a deviant, application of it).

## Approved deviations log

*(empty — none recorded)*

---
*LoanTracker — `Pre-Build/design/ui-spec/patterns-applied.md`. Companion: `ui-spec.md`, `LoanTracker_UI_Standard.md`.*
