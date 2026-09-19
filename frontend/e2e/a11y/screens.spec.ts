import { expect, test } from '@playwright/test';

import {
  checkoutOutOfBand,
  createBorrower,
  createItem,
  firstCategory,
  unique,
} from '../support/api';
import { expectNoA11yViolations } from '../support/axe';
import { selectBorrowerOption, testId } from '../support/ui';

/**
 * One WCAG 2.2 AA scan per screen — all eleven in design/ui-spec/ui-spec.md.
 *
 * Run after the shell pass, so anything reported here belongs to the screen itself.
 * Each screen is scanned with REAL data on it, not in its empty state, because an
 * empty grid hides most of the markup that can fail.
 */
test.describe('Accessibility — every screen at WCAG 2.2 AA', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium-desktop', 'Desktop is the primary a11y target');
  });

  // Fixtures shared by the detail screens: one item on loan, one loan, one borrower.
  async function scenario(request: import('@playwright/test').APIRequestContext) {
    const category = await firstCategory(request);
    const assetTag = unique('QA-A11Y');
    const item = await createItem(request, category.id, assetTag);
    const borrower = await createBorrower(request, unique('QA A11y Borrower'));
    const loan = await checkoutOutOfBand(request, item.id, borrower.id);
    return { item, borrower, loan, category };
  }

  test('1 — scr-item-list (/items)', async ({ page, request }) => {
    await scenario(request);
    await page.goto('/items');
    await expect(testId(page, 'item-list-table')).toBeVisible();

    await expectNoA11yViolations(page, 'scr-item-list');
  });

  test('2 — scr-item-add (/items/new)', async ({ page }) => {
    await page.goto('/items/new');
    await expect(testId(page, 'item-add-name')).toBeVisible();

    await expectNoA11yViolations(page, 'scr-item-add');
  });

  test('3 — scr-item-detail (/items/:id), with the retire guard showing', async ({
    page,
    request,
  }) => {
    const { item } = await scenario(request);
    await page.goto(`/items/${item.id}`);
    await expect(testId(page, 'item-detail-guard')).toBeVisible();

    await expectNoA11yViolations(page, 'scr-item-detail');
  });

  test('4 — scr-borrower-list (/borrowers)', async ({ page, request }) => {
    await scenario(request);
    await page.goto('/borrowers');
    await expect(testId(page, 'borrower-list-table')).toBeVisible();

    await expectNoA11yViolations(page, 'scr-borrower-list');
  });

  test('5 — scr-borrower-add (/borrowers/new)', async ({ page }) => {
    await page.goto('/borrowers/new');
    await expect(testId(page, 'borrower-add-name')).toBeVisible();

    await expectNoA11yViolations(page, 'scr-borrower-add');
  });

  test('6 — scr-borrower-detail (/borrowers/:id), with the deactivate guard showing', async ({
    page,
    request,
  }) => {
    const { borrower } = await scenario(request);
    await page.goto(`/borrowers/${borrower.id}`);
    await expect(testId(page, 'borrower-detail-guard')).toBeVisible();

    await expectNoA11yViolations(page, 'scr-borrower-detail');
  });

  test('7 — scr-loan-list (/loans)', async ({ page, request }) => {
    await scenario(request);
    await page.goto('/loans');
    await expect(testId(page, 'loan-table')).toBeVisible();

    await expectNoA11yViolations(page, 'scr-loan-list');
  });

  test('8 — scr-loan-detail (/loans/:id), with the return panel open', async ({
    page,
    request,
  }) => {
    const { loan } = await scenario(request);
    await page.goto(`/loans/${loan.id}`);
    await testId(page, 'loan-return-open').click();
    await expect(testId(page, 'loan-return-panel')).toBeVisible();

    await expectNoA11yViolations(page, 'scr-loan-detail (return panel open)');
  });

  test('9 — scr-checkout-wizard (/checkout), all three steps', async ({ page, request }) => {
    const { borrower, category } = await scenario(request);
    const assetTag = unique('QA-A11Y-WIZ');
    await createItem(request, category.id, assetTag);

    await page.goto('/checkout');
    await expect(testId(page, 'checkout-step-1')).toBeVisible();
    await expectNoA11yViolations(page, 'scr-checkout-wizard step 1');

    await selectBorrowerOption(page, borrower.name);
    await testId(page, 'checkout-next-to-item').click();
    await expect(testId(page, 'checkout-step-2')).toBeVisible();
    await testId(page, 'checkout-item-search').fill(assetTag);
    await expect(testId(page, `checkout-step2-select-${assetTag}`)).toBeVisible();
    await expectNoA11yViolations(page, 'scr-checkout-wizard step 2');

    await testId(page, `checkout-step2-select-${assetTag}`).click();
    await testId(page, 'checkout-next-to-confirm').click();
    await expect(testId(page, 'checkout-step-3')).toBeVisible();
    await expectNoA11yViolations(page, 'scr-checkout-wizard step 3');
  });

  test('10 — ref-item-category (/reference/item-categories)', async ({ page }) => {
    await page.goto('/reference/item-categories');
    await expect(testId(page, 'category-table')).toBeVisible();

    await expectNoA11yViolations(page, 'ref-item-category');
  });

  test('11 — ref-loan-status (/reference/loan-statuses)', async ({ page }) => {
    await page.goto('/reference/loan-statuses');
    await expect(testId(page, 'status-table')).toBeVisible();

    await expectNoA11yViolations(page, 'ref-loan-status');
  });

  test('the reference screens are also clean in their inline-edit state', async ({ page }) => {
    await page.goto('/reference/loan-statuses');
    await expect(testId(page, 'status-table')).toBeVisible();

    await testId(page, 'status-add-row').click();
    await expect(testId(page, 'status-name-input').first()).toBeVisible();

    await expectNoA11yViolations(page, 'ref-loan-status (inline edit row)');
  });
});
