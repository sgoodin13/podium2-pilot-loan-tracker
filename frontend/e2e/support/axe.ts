import AxeBuilder from '@axe-core/playwright';
import { Page, expect } from '@playwright/test';

/**
 * WCAG 2.2 Level AA — the standard recorded in design/ui-spec/patterns-applied.md.
 *
 * The tag set is fixed here so no individual scan can quietly narrow it, and no rule
 * is ever disabled to make a screen pass. A real violation is reported and fixed.
 */
export const WCAG_22_AA_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

function describe(violations: import('axe-core').Result[]): string {
  return violations
    .map((v) => {
      const targets = v.nodes
        .slice(0, 5)
        .map((n) => `      - ${n.target.join(' ')}`)
        .join('\n');
      return `  [${v.id}] (${v.impact}) ${v.help}\n    ${v.helpUrl}\n${targets}`;
    })
    .join('\n');
}

/** Scans the whole page and fails with every violation spelled out. */
export async function expectNoA11yViolations(page: Page, label: string): Promise<void> {
  const results = await new AxeBuilder({ page }).withTags(WCAG_22_AA_TAGS).analyze();

  expect(
    results.violations,
    `WCAG 2.2 AA violations on ${label}:\n${describe(results.violations)}`,
  ).toEqual([]);
}

/** Scans a single region — used for the shell-first pass and overlay scans. */
export async function expectNoA11yViolationsIn(
  page: Page,
  selector: string,
  label: string,
): Promise<void> {
  const results = await new AxeBuilder({ page })
    .include(selector)
    .withTags(WCAG_22_AA_TAGS)
    .analyze();

  expect(
    results.violations,
    `WCAG 2.2 AA violations in ${label} (${selector}):\n${describe(results.violations)}`,
  ).toEqual([]);
}
