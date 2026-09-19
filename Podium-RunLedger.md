# LoanTracker — Run Ledger

**Created:** 2026-09-19, first cost capture of this run.
**Created from:** `Podium2_RunLedger_Template.md` v1.0 (Podium 2, read-only).
**Append-only.** Never recreated — every later session in this run appends here.

---

## Section 1 — Cost log

| Date | Session / batch label | Model | Cost output (total / API time / wall time) | Items covered | Notes |
|---|---|---|---|---|---|
| 2026-09-19 | Session 1 — greenfield build, all 5 phases | `claude-opus-5` | **Not captured — see note below** | REQ-1.1 … REQ-5.2 (all 14 children) | Developer ran as a background CLI session. `/cost` is an interactive slash command and is not invocable from a non-interactive agent turn, so no figure was produced. Recorded as not captured rather than left blank or estimated. |

**Running total:** not established — 0 of 1 sessions have a captured cost figure as of 2026-09-19.

> **⚠ Capture gap — needs the Orchestrator.** CLAUDE.md Session Completion makes the
> `/cost` capture a hard gate, and Developer could not satisfy it: the figure lives in
> the interactive CLI session, not in anything the agent can read. **The Orchestrator
> should run `/cost` against this session and append the figure to the row above
> before accepting the handoff.**
>
> Per the template's own rule, an honest "not captured" is correct here; an invented
> or extrapolated total is not. This is also worth logging as a methodology finding —
> a hard gate that a background Developer session structurally cannot satisfy is a
> gap in the flow, not in this run.

**Subagent token usage (what Developer *could* measure).** Not a cost figure and not a
substitute for one, but recorded because it is real and attributable:

| Subagent | Model | Tokens | Tool uses | Wall time |
|---|---|---|---|---|
| Methodology read | `claude-opus-5` | 81,348 | 14 | ~2m49s |
| Architecture read | `claude-opus-5` | 87,791 | 18 | ~2m50s |
| UI spec read | `claude-opus-5` | 71,984 | 20 | ~3m28s |
| Database Engineer | `claude-opus-5` | 97,661 | 54 | ~8m17s |
| Item Management screens | `claude-opus-5` | 121,650 | 43 | ~7m56s |
| Borrower Management screens | `claude-opus-5` | 110,154 | 40 | ~8m09s |
| QA Engineer | `claude-opus-5` | *(pending)* | | |

Developer's own turn usage is not included — it is not exposed to the agent.

---

## Section 2 — Resolution evidence

For every item marked resolved **with no committed code change**, and for anything
resolved by static proof only.

| Item | Disposition | Evidence | Notes |
|---|---|---|---|
| Angular 18 production CVE `GHSA-hh8m-fm6v-7cvg` | `live-owed` | `npm audit --omit=dev` output recorded in `docs/Podium-SecurityFindings.md`; advisory affects every Angular 18 release; npm's only fix is `@angular/core@22`, a four-major jump from the Gate 1-approved stack | **Not resolved.** Surfaced for an Orchestrator ruling at Gate 4. Statically verified from the advisory database only — never exercised against a running system |
| Unauthorized-call negative test per endpoint | `resolved-static` | Auth is a stub by ruling (trigger spec §4) — every request is an authenticated Staff user, so there is no unauthorized state to construct | A genuine unauthorized case is untestable under the approved auth scope. Recorded rather than faked |

**For items resolved WITH code, the commit reference is the evidence** — see
`32d526f` and the commits that follow it.

---

## Section 3 — Environment-skew tracking

Populated only when a schema or configuration change is applied to a shared
environment while the covering PR is still open.

| Date applied | Change | Target environment | Covering PR | Reconciliation owner | Horizon |
|---|---|---|---|---|---|
| — | *(none)* | — | — | — | — |

**No skew.** The only database this run touched is the run-scoped local Postgres
(`loantracker_dev_20260919_pilot`) in a throwaway Docker volume on the Orchestrator's
own machine. Shared Validation, UAT and Prod are out of scope for this pilot
(CLAUDE.md Environments), and the migration was generated, never applied anywhere
shared.
