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

`npm audit --omit=dev` reports 10 vulnerabilities (7 moderate, 3 high), all tracing to
this single advisory.

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
