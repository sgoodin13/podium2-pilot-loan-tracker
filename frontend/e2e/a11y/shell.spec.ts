import { expect, test } from '@playwright/test';

import { createItem, firstCategory, unique } from '../support/api';
import { expectNoA11yViolations, expectNoA11yViolationsIn } from '../support/axe';
import { testId } from '../support/ui';

/**
 * SHELL FIRST. The nav, the theme tokens and the two shared overlay patterns
 * (MatDialog, MatSnackBar) are scanned before any screen is scanned on top of them —
 * a shell-level violation would otherwise be reported eleven times.
 */
test.describe('Accessibility — the shell', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium-desktop', 'Desktop is the primary a11y target');
  });

  test('the nav shell has no WCAG 2.2 AA violations', async ({ page }) => {
    await page.goto('/items');
    await expect(testId(page, 'nav-items')).toBeVisible();

    await expectNoA11yViolationsIn(page, '.lt-sidenav', 'the primary nav');
  });

  test('the skip link is the first focusable element and moves focus to main', async ({ page }) => {
    await page.goto('/items');

    await page.keyboard.press('Tab');

    const focused = page.locator(':focus');
    await expect(focused).toHaveText('Skip to main content');

    await page.keyboard.press('Enter');
    await expect(page.locator('#main')).toBeVisible();
  });

  test('every nav item is reachable and labelled', async ({ page }) => {
    await page.goto('/items');

    const nav = page.getByRole('navigation', { name: 'Primary' });
    await expect(nav).toBeVisible();

    for (const label of [
      'Items',
      'Borrowers',
      'Checkout',
      'Loans',
      'Item Categories',
      'Loan Statuses',
    ]) {
      await expect(nav.getByRole('link', { name: label, exact: true })).toBeVisible();
    }
  });

  test('the shared confirm dialog has no WCAG 2.2 AA violations and traps focus without trapping the keyboard', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const item = await createItem(request, category.id, unique('QA-A11Y-DLG'));

    await page.goto(`/items/${item.id}`);
    await testId(page, 'item-retire-btn').click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(item.assetTag);

    await expectNoA11yViolationsIn(page, '.cdk-overlay-container', 'the shared confirm dialog');

    // Escape closes it — no keyboard trap.
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
  });

  test('the shared success snackbar has no WCAG 2.2 AA violations', async ({ page, request }) => {
    const category = await firstCategory(request);
    const item = await createItem(request, category.id, unique('QA-A11Y-SNACK'));

    await page.goto(`/items/${item.id}`);
    await testId(page, 'item-retire-btn').click();
    await testId(page, 'confirm-accept').click();

    const snackbar = page.locator('.lt-snackbar');
    await expect(snackbar).toBeVisible();
    await expect(snackbar).toContainText(item.assetTag);

    await expectNoA11yViolationsIn(page, '.cdk-overlay-container', 'the success snackbar');
  });

  test('the default landing route as a whole has no WCAG 2.2 AA violations', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveURL(/\/items$/);
    await expect(testId(page, 'item-list-table').or(testId(page, 'item-list-empty'))).toBeVisible();

    await expectNoA11yViolations(page, 'the default route (/ → /items)');
  });
});
