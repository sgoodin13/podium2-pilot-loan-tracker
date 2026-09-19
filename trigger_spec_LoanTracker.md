# LoanTracker — Trigger Spec v1
**Authored:** Stage ③, 2026-09-19. **Author:** Claude (Business Analyst role, per template). Validated/consumed at build-prep — not silently re-authored.

```
Product:          LoanTracker
Orchestrator:     Scott
Git host:         GitHub
Git org:          sgoodin13
Requirements:     requirements/loantracker
Repo:             https://github.com/sgoodin13/podium2-pilot-loan-tracker.git
Branch:           feature/20260919-loantracker-pilot
Session type:     greenfield
Intake:           new-build
Standard:         Podium2_Modeling_Standard v1.0
```

---

## 0. Intake

`new-build` — modeled from BRs through the full Stage ③ package. Sections §1, §2, §3, §5, §6 apply. §1b (grounding record) and §2b (scoped change set) are change-run-only — omitted here.

## 1. Gate 2 status and delivery

```yaml
gate2_complete: true
intake: new-build

package_approved:
  scope: full system
  features_and_functions: FeaturesAndFunctions_LoanTracker.html
  level1_process_flow: Level1ProcessFlow_LoanTracker.html
  functional_specs:
    - FunctionalSpec_LoanTracker_LoanTracker.md
  processes:
    - id: LoanTracker-checkout
      doc: BusinessProcess_LoanTracker_Checkout
      status: approved
  traceability_matrix: TraceabilityMatrix_LoanTracker.md
  architecture_approved: true

architecture:
  folder: "C:/Users/sgood/OneDrive/Documents/Loan Tracker Pilot Test/Architecture/"
  product_architecture:   LoanTracker_Product_Architecture_v1.html
  technical_architecture: LoanTracker_Technical_Architecture_v1.html
  logical_data_model:     LoanTracker_Logical_Data_Model_v1.md
  physical_data_model:    LoanTracker_PDM_v1.html
  diagram:                LoanTracker_Architecture_Diagram_v1.html
  effort_estimate:        LoanTracker_Effort_Estimate_v1.html

delivery:
  requirements_folder: requirements/loantracker
  repo: https://github.com/sgoodin13/podium2-pilot-loan-tracker.git
  ui_spec_delivered: true    # delivered in this commit
  status: populated          # delivered in this commit
```

**No `in_flight_fix` block** — this is the pilot's first pass, not a Gate 5 finding.

## 2. Requirements hierarchy — new-build

Full parent/child table delivered as `requirements/loantracker/LoanTracker_Requirements.md` at Stage ④ (staged now at `Pre-Build/LoanTracker_Requirements_Hierarchy.md`). 5 parents (REQ-1 through REQ-5), 14 children — see that file for the complete table; not duplicated here per the template's own guidance against restating source artifacts.

## 3. Approved process slice — Checkout

```json
{
  "process": "LoanTracker-checkout",
  "pattern_reference": "Podium2_UI_Patterns v1.0",
  "tasks": [
    {
      "id": "t1",
      "label": "Select borrower",
      "lane": "Staff",
      "ui_touchpoint": true,
      "screen": "scr-checkout-wizard",
      "screen_binding": "declare",
      "module": "LoanTracker",
      "pattern": "Pattern 06 — Wizard/stepped form",
      "ui_spec_mockup": "design/ui-spec/scr-checkout-wizard.html",
      "requirement_ref": "REQ-3.1",
      "acceptance": [
        "Only active borrowers are selectable; a borrower is required to advance to step 2."
      ]
    },
    {
      "id": "t2",
      "label": "Select available item",
      "lane": "Staff",
      "ui_touchpoint": true,
      "screen": "scr-checkout-wizard",
      "screen_binding": "reuse",
      "module": "LoanTracker",
      "pattern": "Pattern 06 — Wizard/stepped form",
      "ui_spec_mockup": "design/ui-spec/scr-checkout-wizard.html",
      "requirement_ref": "REQ-3.2",
      "acceptance": [
        "Only items with no open loan are shown as selectable at this step."
      ]
    },
    {
      "id": "t3",
      "label": "Commit Loan",
      "lane": "Staff",
      "ui_touchpoint": true,
      "screen": "scr-checkout-wizard",
      "screen_binding": "reuse",
      "module": "LoanTracker",
      "pattern": "Pattern 06 — Wizard/stepped form",
      "ui_spec_mockup": "design/ui-spec/scr-checkout-wizard.html",
      "requirement_ref": "REQ-3.3",
      "acceptance": [
        "The Loan is created only if the item still has zero open loans at the moment of commit — enforced by the filtered unique index (Physical Data Model), not only the UI's earlier availability check.",
        "The confirmation names the specific item instance (asset tag), never just its category."
      ]
    },
    {
      "id": "t4",
      "label": "Reject, show reason",
      "lane": "Staff",
      "ui_touchpoint": true,
      "screen": "scr-checkout-wizard",
      "screen_binding": "reuse",
      "module": "LoanTracker",
      "pattern": "Pattern 06 — Wizard/stepped form",
      "ui_spec_mockup": "design/ui-spec/scr-checkout-wizard.html",
      "requirement_ref": "REQ-3.4",
      "acceptance": [
        "A rejected checkout shows a specific reason (item no longer available) and confirms nothing was persisted."
      ]
    }
  ],
  "gateways": [
    { "id": "g1", "label": "Still available at commit?", "type": "exclusive" }
  ]
}
```

## 4. Orchestrator rulings (pre-loaded)

- `[RULING: topology]` Single service, single schema — not microservices/schema-per-tenant. Do not re-derive a different topology from the ASIM-derived stack (`Products/LoanTracker.json` domain_rulings; Gate 1).
- `[RULING: schema]` Primary key type `uuid` (Postgres native), no tenancy mechanism (single-tenant), soft-delete via `is_active` on every table. Decided at Gate 1 (Physical Data Model) — do not re-derive.
- `[RULING: auth]` Stub middleware only — every request treated as authenticated Staff. No login flow, no second role.
- `[RULING: retire/deactivate guard]` Hard-block, not a warning, when retiring an item or deactivating a borrower with an open loan — per SME (Priya Anand) recommendation, `Business_Requirements/LoanTracker_Business_Requirements_v1.md` §8, resolved.
- `[RULING: BR-1 enforcement]` The item-already-on-loan rule is enforced at the database layer (filtered unique index `ux_loans_item_open`), never only in application code or the UI. This is not negotiable at Gate 3.

## 5. Product context

```yaml
tenant_key: N/A — single-tenant (confirms Products/LoanTracker.json declaration)
stack: "Angular 18 + TypeScript + Angular Material (frontend) · C# .NET 8 REST API + EF Core (backend) · PostgreSQL 17 · Docker for local dev"
  # VERIFIES the declaration in Products/LoanTracker.json against the actual repo at Stage ④.
  # Greenfield — new repo (podium2-pilot-loan-tracker), nothing inherited from ASIM_Agentic_Pilot_Test
  # or any other product. Say so plainly per the template's own instruction.
repo_url: https://github.com/sgoodin13/podium2-pilot-loan-tracker.git
branch_base: main
kb_root: "C:/Users/sgood/OneDrive/Documents/Loan Tracker Pilot Test"
model: claude-opus-5
ui_standard: LoanTracker_UI_Standard.md
pattern_reference: Podium2_UI_Patterns v1.0
source_process: BusinessProcess_LoanTracker_Checkout
source_ui_design: design/ui-spec/
```

## 6. Build prerequisites

**Both intakes**
- ✅ Gate 2 signed off, this spec assembled.
- ✅ Gate 1 architecture approved — `architecture_approved: true`, `architecture:` block carries real paths.
- ⏳ **Requirements delivered** — pending Stage ④ commit.
- ⏳ **Stage ④ git delivery** — pending (next stage). `stack` above to be confirmed against the actual repo once delivered.
- ✅ Launch-time environment confirmed: branch label `feature/20260919-loantracker-pilot`, tenant key N/A, KB root fully qualified, model `claude-opus-5`.
- ✅ Open rulings carried in §4.

**New-build only**
- ✅ `design/ui-spec/` complete — 11 mockups, `ui-spec.md`, `patterns-applied.md`, `index.html`, `assets/mockups.css`. ⏳ Delivery into the repo (`ui_spec_delivered` flips to `true` at Stage ④).
- ✅ Traceability matrix present, `Unmapped: 0`.

---
*LoanTracker — staged in `Pre-Build/`, delivered to repo root as `trigger_spec_LoanTracker.md` at Stage ④.*
