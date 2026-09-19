# LoanTracker — Dependency / CVE scan record

Per CLAUDE.md: *"After adding/updating any dependency, run the vulnerability scan before
considering the change done. Flag any known critical/high CVE to the Orchestrator rather
than silently introducing it."*

---

## Scan 1 — frontend, after initial scaffold (Phase 0)

Commands: `npm audit` · `npm audit --omit=dev`
Stack as installed: Angular 18.2.14, Angular Material 18.2.14, Angular CDK 18.2.14.

**Totals:** 58 vulnerabilities (7 low, 22 moderate, 28 high, 1 critical).

### Disposition

**The 1 critical and 27 of the 28 high findings are dev-toolchain transitives only.**
They live under `@angular/cli`, `@angular-devkit/build-angular`, `@playwright/test` and
their dependency trees (`tar` — the critical, plus `vite`, `rollup`, `postcss`,
`webpack-dev-server`, `node-gyp`, and the `sigstore`/`pacote` npm-internals chain). None
are reachable from the production bundle — they run only at build and test time on a
developer machine.

**One finding does affect production code and needs an Orchestrator ruling:**

| Advisory | Package | Severity | Note |
|---|---|---|---|
| [GHSA-hh8m-fm6v-7cvg](https://github.com/advisories/GHSA-hh8m-fm6v-7cvg) — Angular sanitization bypass via directive host bindings on concrete host elements | `@angular/core`, `@angular/compiler` (and `@angular/animations` transitively) | high | Present in **every** Angular 18 release |

`npm audit --omit=dev` reports 10 vulnerabilities (7 moderate, 3 high).

> **Corrected at Scan 3.** The original wording here said those 10 were *"all tracing to
> this single advisory."* That was wrong — they trace to a family of advisories, of which
> `GHSA-hh8m-fm6v-7cvg` is one. See Scan 3 below for the full list and for the exposure
> evidence that replaces it.

### Why Developer cannot resolve this

npm's only offered fix is `@angular/core@22.1.7` — a four-major-version jump. The Angular
18 stack is a **Gate 1 architecture ruling** (`Products/LoanTracker.json`,
`LoanTracker_Stack_Rules.md`, trigger spec §5). Changing it is an architectural decision,
and Developer makes none autonomously. Per `Podium2_Template.md:121`, where the
architecture and Developer's judgment differ, **the architecture governs and the
difference is a finding**.

**Build proceeds on Angular 18 as approved.** Carried to Gate 4 for the Orchestrator's
severity tiering.

### Practical exposure on this run — low

The advisory requires attacker-controlled input reaching a directive host binding.
LoanTracker is Local-only for this pilot (Shared Validation / UAT / Prod are explicitly
out of scope, CLAUDE.md Environments), has no real users, no authentication, and no
untrusted input source. `domain_amplification_criteria` is `N/A — low-stakes`. Recorded
for a ruling, not presented as a blocker.

**Marker:** `live-owed` — statically verified from the advisory database only; not
exercised against a running system.

---

## Scan 2 — full stack, end of build (Phase 6)

Commands: `dotnet list package --vulnerable --include-transitive` (per project) ·
`npm audit --omit=dev`

### Backend

| Project | Result |
|---|---|
| `Api` — **the shipped service** | **No vulnerable packages.** |
| `Api.Tests` — test-only, never deployed | 3 packages, 4 high advisories (corrected at Scan 3): `SSH.NET` 2023.0.0 ([GHSA-q939-rpr3-3284](https://github.com/advisories/GHSA-q939-rpr3-3284)), `System.Net.Http` 4.3.0 ([GHSA-7jgj-8wvc-jh57](https://github.com/advisories/GHSA-7jgj-8wvc-jh57)), `System.Text.RegularExpressions` 4.3.0 ([GHSA-cmhx-cq75-c4mj](https://github.com/advisories/GHSA-cmhx-cq75-c4mj)) |

All four test findings are transitive dependencies of `Testcontainers.PostgreSql`
3.10.0, which exists so BR-1 can be asserted against a **real** Postgres rather than
EF Core InMemory — InMemory cannot honour a filtered unique index, so dropping
Testcontainers would silently weaken the pilot's single most important test. These
packages run only on a developer machine during `dotnet test` and are not part of any
deployable artifact.

**Recommendation:** accept for this pilot; revisit if Testcontainers is ever carried
into a deployed context (it should not be).

### Frontend

**Re-check could not be completed.** The npm advisory endpoint returned
`503 Service Unavailable — We are currently performing maintenance` at the time of the
Phase 6 scan. Recorded as not-re-checked rather than reported as clean.

The Phase 0 result stands as the last successful reading: 58 findings, of which the 1
critical and 27 of the 28 highs are dev-toolchain transitives, and **one** high affects
production code — the Angular 18 sanitization-bypass advisory in Scan 1 above, still
open and still awaiting an Orchestrator ruling.

**Markers:** `live-owed` on the Angular finding; `resolved-static` on the backend
result — verified from the advisory database, not exercised live.

---

## Orchestrator ruling — Gate 4 (2026-09-19)

**Angular 18 sanitization-bypass advisory ([GHSA-hh8m-fm6v-7cvg](https://github.com/advisories/GHSA-hh8m-fm6v-7cvg)) — Tier 2, ordinary.**

Ruled by the Orchestrator at Gate 4 under the two-tier severity rule (CLAUDE.md §Gates).
Rationale as given: `domain_amplification_criteria` is `N/A — low-stakes` for this
product, and practical exposure is low — Local-only, with no untrusted input reaching
the sanitizer.

**Disposition:** logged, tracked informally, **no further action on this build.** The
stack stays on Angular 18 as the Gate 1 architecture approved; the advisory is not a
blocker and does not reopen the architecture.

The `live-owed` marker recorded in Scan 1 **stands**. Tiering a finding does not
discharge that marker — the advisory was verified statically from the advisory database
only, never exercised against a running system.

**Other Gate 4 dispositions, for the record:**

| Item | Ruling |
|---|---|
| `Api.Tests` transitive highs (`SSH.NET`, `System.Net.Http`, `System.Text.RegularExpressions` via `Testcontainers.PostgreSql`) | Accepted — test-only, never deployed. Revisit only if Testcontainers is ever carried into a deployed context. |
| Frontend re-check incomplete (npm advisory endpoint `503` during Phase 6) | Acknowledged — "not re-checked" is the correct honest record. No action required; retry opportunistically. **Retried successfully at Scan 3 below.** |

---

## Scan 3 — Compliance/Security pass, post-Gate-4 (2026-09-19)

Run independently by Compliance/Security Engineer and then re-verified directly, because
a scan result relayed from a subagent is a claim rather than evidence.

### Frontend — the re-check that 503'd in Phase 6 now completes

`npm audit --omit=dev` returns **10 vulnerabilities (7 moderate, 3 high)** — the same
count Scan 1 recorded. The **count** was right; the **attribution** was not.

They do not all trace to `GHSA-hh8m-fm6v-7cvg`. `@angular/core` carries eight distinct
advisories and `@angular/compiler` several, all fixed only in `@angular/core@22.1.7`:

| Advisory | Summary |
|---|---|
| [GHSA-hh8m-fm6v-7cvg](https://github.com/advisories/GHSA-hh8m-fm6v-7cvg) | Sanitization bypass via directive host bindings on concrete host elements |
| [GHSA-prjf-86w9-mfqv](https://github.com/advisories/GHSA-prjf-86w9-mfqv) | Angular i18n XSS |
| [GHSA-g93w-mfhg-p222](https://github.com/advisories/GHSA-g93w-mfhg-p222) | XSS in i18n attribute bindings |
| [GHSA-jj27-h5hq-8x99](https://github.com/advisories/GHSA-jj27-h5hq-8x99) | i18n XSS via event-handler attributes |
| [GHSA-jrmj-c5cx-3cw6](https://github.com/advisories/GHSA-jrmj-c5cx-3cw6) | XSS via unsanitized SVG script attributes |
| [GHSA-v4hv-rgfq-gp49](https://github.com/advisories/GHSA-v4hv-rgfq-gp49) | Stored XSS via SVG animation / SVG URL / MathML attributes |
| [GHSA-f3m7-gqxr-g87x](https://github.com/advisories/GHSA-f3m7-gqxr-g87x) | Template and attribute namespace sanitization bypass |
| [GHSA-692r-grfm-v8x7](https://github.com/advisories/GHSA-692r-grfm-v8x7) | Template and dynamic-component namespace bypass |
| [GHSA-rgjc-h3x7-9mwg](https://github.com/advisories/GHSA-rgjc-h3x7-9mwg) | Client hydration DOM clobbering & response-cache poisoning |
| [GHSA-58w9-8g37-x9v5](https://github.com/advisories/GHSA-58w9-8g37-x9v5) | `@angular/compiler` two-way property binding sanitization bypass |

**Why this matters to the Gate 4 ruling.** That ruling reasoned from a narrow exposure
argument — *"requires attacker-controlled input reaching a directive host binding."* That
is accurate for the one advisory it named and does not describe the other nine. The
conclusion still holds, but it needs to rest on evidence that covers the whole family.

### The exposure evidence that actually supports Tier 2

Verified directly against `frontend/src`, not inferred:

| Check | Result | What it rules out |
|---|---|---|
| `innerHTML`, `bypassSecurityTrust*`, `DomSanitizer`, `eval(` | **0 occurrences** | The application never opts out of Angular's default escaping — the precondition most of these advisories need |
| `$localize`, `i18n=` | **0 occurrences** | Removes the three i18n-family advisories from the reachable set (see also the C4 finding — i18n is not implemented) |
| `<svg>`, `<math>` authored in templates | **0 files** | Removes the two SVG/MathML advisories |
| SSR / hydration | not in use | Removes the hydration DOM-clobbering advisory |

Combined with Local-only scope and no untrusted input source, **Tier 2 remains the right
call** — now on grounds that cover all ten advisories rather than one.

**Marker:** `live-owed` still stands. Every line above is static verification against the
advisory database and the source tree; none of it was exercised against a running system.

### Backend — re-verified, and one claimed finding did not reproduce

`dotnet list package --vulnerable --include-transitive` re-run per project:

- **`Api`, the shipped service: no vulnerable packages.** Independently confirmed.
- `Api.Tests`: **3 vulnerable packages carrying 4 high advisories.**

| Package | Advisories |
|---|---|
| `SSH.NET` 2023.0.0 | [GHSA-q939-rpr3-3284](https://github.com/advisories/GHSA-q939-rpr3-3284) **and** [GHSA-mggc-4xg6-vcxf](https://github.com/advisories/GHSA-mggc-4xg6-vcxf) |
| `System.Net.Http` 4.3.0 | [GHSA-7jgj-8wvc-jh57](https://github.com/advisories/GHSA-7jgj-8wvc-jh57) |
| `System.Text.RegularExpressions` 4.3.0 | [GHSA-cmhx-cq75-c4mj](https://github.com/advisories/GHSA-cmhx-cq75-c4mj) |

**Scan 2's "3 high" was a miscount, and so was this scan's first attempt to correct it.**
`dotnet list package --vulnerable` prints a second advisory for the same package on a
*continuation line* with the package and version columns left blank. Counting package
rows gives 3; counting advisories gives 4. A first pass here filtered the output with a
grep that dropped the continuation line entirely and concluded the fourth advisory "did
not reproduce" — it reproduces on every run, and the filter was at fault, not the tool.
Recorded rather than quietly amended, because the same grep would hide the same class of
advisory again.

Nothing substantive changes: `Api` is clean, and all four are test-only, reachable only
via `Testcontainers.PostgreSql` during `dotnet test`.
