# LoanTracker — Run Ledger

**Created:** 2026-09-19, first cost capture of this run.
**Created from:** `Podium2_RunLedger_Template.md` v1.0 (Podium 2, read-only).
**Append-only.** Never recreated — every later session in this run appends here.

---

## Section 1 — Cost log

| Date | Session / batch label | Model | Cost output (total / API time / wall time) | Items covered | Notes |
|---|---|---|---|---|---|
| 2026-09-19 | Session 1 — greenfield build, all 5 phases | `claude-opus-5` | **Not captured — see note below** | REQ-1.1 … REQ-5.2 (all 14 children) | Developer ran as a background CLI session. `/cost` is an interactive slash command and is not invocable from a non-interactive agent turn, so no figure was produced. Recorded as not captured rather than left blank or estimated. |

**Running total:** not established, and **no longer owed**.

> **RESOLVED at Gate 4 — the requirement itself was eliminated, not satisfied.**
>
> This block previously read "⚠ Capture gap — needs the Orchestrator," and asked the
> Orchestrator to run `/cost` and append the figure. **That ask is withdrawn.** The
> Orchestrator ruled `/cost` out of Podium 2 entirely as a standing methodology change,
> not specific to this build, and confirmed this row needs no follow-up.
>
> The original reasoning still stands as the *finding* that prompted the change: a hard
> gate that a background Developer session structurally cannot satisfy is a gap in the
> flow, not in the run. The honest "not captured" above is the correct permanent record.
> It is not an outstanding action.

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
| QA Engineer | `claude-opus-5` | *(not captured)* | *(not captured)* | *(not captured)* |
| Compliance/Security Engineer (4 rounds + sign-off) | `claude-opus-5` | ~383,211 | 157 | ~38m |

Developer's own turn usage is not included — it is not exposed to the agent.

**Two honesty notes on the table above.** The QA Engineer row was left as *(pending)* during
the build and the figures were never recovered afterwards — the run completed before they
were read back, and they are not reconstructible now, so the row says "not captured"
rather than carrying an estimate. The Compliance/Security figure is the **final cumulative
total** reported across five invocations of one resumed agent; the per-invocation token
counts rose monotonically, which is consistent with a cumulative counter, but that is an
inference from the reported numbers rather than something documented. The tool-use count
is a sum of per-invocation figures. Treated as indicative, not authoritative.

---

## Section 2 — Resolution evidence

For every item marked resolved **with no committed code change**, and for anything
resolved by static proof only.

| Item | Disposition | Evidence | Notes |
|---|---|---|---|
| Angular 18 advisory family (10 advisories, incl. `GHSA-hh8m-fm6v-7cvg`) | `live-owed` | `npm audit --omit=dev` recorded in `docs/Podium-SecurityFindings.md` Scan 3, with the full advisory table; plus direct source evidence that the app never opts out of Angular's escaping — zero `innerHTML` / `bypassSecurityTrust*` / `DomSanitizer` / `eval`, no i18n, no authored SVG or MathML, no SSR | **Ruled Tier 2 (ordinary) by the Orchestrator at Gate 4** — logged and tracked informally, no further action on this build, stack stays on Angular 18 as Gate 1 approved. Scan 1 originally described this as **one** advisory; that was wrong and is corrected in Scan 3. **`live-owed` still stands** — tiering did not discharge it, and neither did the Compliance sign-off. All of it is static verification, never exercised against a running system |
| Unauthorized-call negative test per endpoint | `resolved-static` | Auth is a stub by ruling (trigger spec §4) — every request is an authenticated Staff user, so there is no unauthorized state to construct | A genuine unauthorized case is untestable under the approved auth scope. Recorded rather than faked |
| Stub auth confined to Development | `live-auth-owed` → **discharged** | `UseStubAuthentication` resolves `IWebHostEnvironment` and throws on boot outside Development; the Development-boot warning was observed in the live API log during Runtime Validation | Compliance finding F1. The guard is exercised live on the Development path only — the **throw** path is verified by reading, not by booting a Production host. Narrower than it sounds, so stated |
| SonarQube — hotspots / vulnerabilities / smells / duplication / complexity | **not executed** | Probed across two Compliance rounds: no `sonar-scanner`/`dotnet-sonarscanner` on PATH, absent from `dotnet tool list --global`, no `sonar-project.properties`, no sonar reference in any project file, no local image. Nothing installed, no output fabricated | Declined by explicit Orchestrator ruling (trigger spec §4). **A recorded decision, not a pass** — that checklist line of the Compliance audit has no evidence behind it and should not be read as one |
| BR-1 under genuine concurrency | **not executed** | `Podium2_Template.md:246` bars concurrency and fault-injection testing | BR-1 is proven **sequentially** — a real simultaneous double-checkout was never run. The guarantee rests on the filtered unique index being physically present, which *is* asserted from `pg_indexes` including its `WHERE` clause |

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
