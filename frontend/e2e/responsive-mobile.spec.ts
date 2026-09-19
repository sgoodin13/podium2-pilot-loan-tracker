import { expect, test } from '@playwright/test';

import { checkoutOutOfBand, createBorrower, createItem, firstCategory, unique } from './support/api';
import { expectNoA11yViolations } from './support/axe';
import { searchList, selectBorrowerOption, testId } from './support/ui';

/**
 * The mobile floor — one viewport (Pixel 5, 393×851, from playwright.config.ts), not a
 * compatibility matrix. Everything here runs ONLY on the mobile-chrome project, so the
 * desktop flow specs are not duplicated at a viewport they were not written for.
 */
test.describe('Mobile viewport floor', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== 'mobile-chrome', 'The mobile floor only');
  });

  /** Horizontal overflow of the document, in CSS pixels. 0 means the page reflows. */
  async function horizontalOverflow(page: import('@playwright/test').Page): Promise<number> {
    return page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
  }

  /**
   * DEFECT D7 — RED ON PURPOSE.
   *
   * WCAG 2.2 AA 1.4.10 (Reflow): content must be presentable without two-dimensional
   * scrolling at 320 CSS px. The shell's fixed 220 px sidenav sits beside a main area
   * whose tables have no small-viewport treatment, so at 393 px the page scrolls
   * sideways and the right-hand controls (the wizard's "Select" button, a table's last
   * column) sit off-screen.
   */
  test('the app reflows at 393px without horizontal scrolling (WCAG 2.2 AA 1.4.10)', async ({
    page,
  }) => {
    for (const route of ['/items', '/borrowers', '/loans', '/checkout']) {
      await page.goto(route);
      await expect(page.locator('#main')).toBeVisible();

      expect(
        await horizontalOverflow(page),
        `${route} must not scroll sideways at 393 CSS px`,
      ).toBe(0);
    }
  });

  test('the item list renders and its availability chips are readable on a phone', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-MOB');
    const item = await createItem(request, category.id, assetTag);
    const borrower = await createBorrower(request, unique('QA Mobile Borrower'));
    await checkoutOutOfBand(request, item.id, borrower.id);

    await page.goto('/items');
    const row = await searchList(page, 'item-list-search', 'item-list-row', assetTag);
    await expect(row).toBeVisible();
    await expect(row.locator('[data-testid="item-availability-chip"]')).toHaveText('On loan');

    await expectNoA11yViolations(page, 'scr-item-list at 393x851');
  });

  test('the checkout wizard is operable on a phone', async ({ page, request }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-MOBWIZ');
    await createItem(request, category.id, assetTag);
    const borrower = await createBorrower(request, unique('QA Mobile Wizard'));

    await page.goto('/checkout');
    await selectBorrowerOption(page, borrower.name);
    await testId(page, 'checkout-next-to-item').click();

    await expect(testId(page, 'checkout-step-2')).toBeVisible();
    await testId(page, 'checkout-item-search').fill(assetTag);

    const select = testId(page, `checkout-step2-select-${assetTag}`);
    await select.scrollIntoViewIfNeeded();
    await expect(select).toBeVisible();

    // This assertion was RED ON PURPOSE for defect D8: the Select control rendered and
    // was enabled, but could not be tapped at this viewport — the sticky toolbar and
    // table cells sat on top of its centre once the sideways-overflowing page (D7) was
    // scrolled to reach it, so a tap landed on the wrong element and the wizard could not
    // be completed on a phone at all. Both are fixed; the assertion now pins the fix.
    await select.click({ timeout: 5_000 });

    // Focus moves at 393px as well as on the desktop. The wizard's transitions are the
    // ones a phone user most needs to work, and no focus spec covered this viewport —
    // the desktop project is the only one the focus suite runs in.
    await expect(testId(page, `checkout-step2-selected-${assetTag}`)).toBeFocused();

    await testId(page, 'checkout-next-to-confirm').click();

    await expect(testId(page, 'checkout-step-3')).toBeFocused();

    const summary = testId(page, 'checkout-confirm-summary');
    await expect(summary).toBeVisible();
    await expect(summary).toContainText(assetTag);

    await expectNoA11yViolations(page, 'scr-checkout-wizard at 393x851');
  });

  test('the nav is reachable on a phone', async ({ page }) => {
    await page.goto('/items');

    const nav = page.getByRole('navigation', { name: 'Primary' });
    await expect(nav.getByRole('link', { name: 'Checkout', exact: true })).toBeVisible();

    await nav.getByRole('link', { name: 'Loans', exact: true }).click();
    await expect(page).toHaveURL(/\/loans$/);
  });
});
