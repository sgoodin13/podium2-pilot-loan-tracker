import { expect, test } from '@playwright/test';

import { firstCategory, loansForItem, findItems, unique } from './support/api';
import {
  createBorrowerThroughUi,
  createItemThroughUi,
  driveWizardToConfirm,
  pickSelectOption,
  searchList,
  testId,
} from './support/ui';

/**
 * THE CANONICAL CHAINED SPEC — the one the Runtime Validation watch-run drives.
 *
 * One flow, in process order: create an Item through the UI form → create a Borrower
 * through the UI form → run the checkout wizard → confirm the Loan on the Loan list →
 * return it → confirm the Item reads as Available again.
 *
 * Every assertion is a value RENDERED ON SCREEN. HTTP is used only out-of-band, to
 * independently confirm what was persisted — never as the assertion itself.
 */
test.describe('Checkout — full chained happy path', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(
      testInfo.project.name !== 'chromium-desktop',
      'Primary desktop flow; the mobile floor is responsive-mobile.spec.ts',
    );
  });

  test('item → borrower → checkout → loan list → return → available again', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-FLOW');
    const itemName = `QA Flow Drill ${assetTag}`;
    const borrowerName = unique('QA Flow Borrower');

    // --- 1. Create an Item through the actual form ------------------------
    await createItemThroughUi(page, {
      name: itemName,
      assetTag,
      categoryName: category.name,
      description: 'Created by the canonical E2E flow',
    });

    await expect(testId(page, 'item-availability-chip')).toHaveText('Available');
    await expect(testId(page, 'item-detail-category')).toHaveText(category.name);
    await expect(testId(page, 'item-loan-history-empty')).toBeVisible();

    // --- 2. Create a Borrower through the actual form ---------------------
    await createBorrowerThroughUi(page, {
      name: borrowerName,
      email: 'qa.flow@example.invalid',
      department: 'QA Flow',
    });

    await expect(page.locator('h1')).toContainText(borrowerName);
    await expect(testId(page, 'borrower-detail-department')).toHaveText('QA Flow');

    // --- 3. Run the checkout wizard ---------------------------------------
    await driveWizardToConfirm(page, { borrowerName, assetTag });

    // The confirmation step names the specific item instance, not just its category.
    const summary = testId(page, 'checkout-confirm-summary');
    await expect(summary).toContainText(itemName);
    await expect(summary).toContainText(assetTag);
    await expect(summary).toContainText(borrowerName);

    await testId(page, 'checkout-confirm').click();

    // The success confirmation names the asset tag — SME requirement (BR §8).
    const snackbar = page.locator('.checkout-success-snackbar');
    await expect(snackbar).toBeVisible();
    await expect(snackbar).toContainText(assetTag);
    await expect(snackbar).toContainText(borrowerName);

    // --- 4. The Loan exists and renders -----------------------------------
    await expect(page).toHaveURL(/\/loans\/[0-9a-f-]{36}$/);
    const loanUrl = page.url();

    await expect(testId(page, 'loan-detail-item')).toContainText(assetTag);
    await expect(testId(page, 'loan-detail-borrower')).toHaveText(borrowerName);
    await expect(testId(page, 'loan-detail-status')).toContainText('Checked Out');
    await expect(testId(page, 'loan-detail-status')).toContainText('not yet returned');

    // --- 5. It appears on the Loan list -----------------------------------
    await page.goto('/loans');
    const row = await searchList(page, 'loan-list-search', 'loan-list-row', assetTag);
    await expect(row).toBeVisible();
    await expect(row).toContainText(itemName);
    await expect(row).toContainText(assetTag);
    await expect(row).toContainText(borrowerName);
    await expect(row.locator('[data-testid="loan-status-chip"]')).toHaveText('Checked Out');

    // --- 6. The Item now reads as On loan ---------------------------------
    await page.goto('/items');
    const itemRow = await searchList(page, 'item-list-search', 'item-list-row', assetTag);
    await expect(itemRow).toContainText(assetTag);
    await expect(itemRow.locator('[data-testid="item-availability-chip"]')).toHaveText('On loan');

    // NOTE: the row's "Current borrower" cell is empty here. That is DEFECT D5, asserted
    // head-on in item-list-current-borrower.spec.ts rather than silently accepted in the
    // middle of this flow. The holder IS shown on the item detail screen below.

    await itemRow.click();
    await expect(page).toHaveURL(/\/items\/[0-9a-f-]{36}$/);
    await expect(testId(page, 'item-detail-guard')).toContainText(borrowerName);

    // --- 7. Open the loan from the list and return it ---------------------
    await page.goto('/loans');
    const loanRow = await searchList(page, 'loan-list-search', 'loan-list-row', assetTag);
    await loanRow.click();
    await expect(page).toHaveURL(loanUrl);

    await testId(page, 'loan-return-open').click();
    await expect(testId(page, 'loan-return-panel')).toBeVisible();

    // A terminal status is required before the return can even be submitted.
    await expect(testId(page, 'loan-return-confirm')).toBeDisabled();
    await pickSelectOption(page, 'loan-return-status', 'Returned');
    await expect(testId(page, 'loan-return-confirm')).toBeEnabled();

    await testId(page, 'loan-return-confirm').click();

    const returnSnackbar = page.locator('.return-success-snackbar');
    await expect(returnSnackbar).toBeVisible();
    await expect(returnSnackbar).toContainText(assetTag);

    // --- 8. The status updates on screen ----------------------------------
    await expect(testId(page, 'loan-detail-status')).toContainText('Returned');
    await expect(testId(page, 'loan-detail-status')).toContainText('this loan is closed');
    await expect(testId(page, 'loan-detail-returned-at')).toBeVisible();
    await expect(testId(page, 'loan-return-open')).toHaveCount(0);
    await expect(testId(page, 'loan-closed-note')).toContainText('Returned');

    // --- 9. The Item is Available again -----------------------------------
    await page.goto('/items');
    const returnedItemRow = await searchList(page, 'item-list-search', 'item-list-row', assetTag);
    await expect(returnedItemRow.locator('[data-testid="item-availability-chip"]')).toHaveText(
      'Available',
    );

    // The item's own history now shows the closed loan.
    await returnedItemRow.click();
    await expect(page).toHaveURL(/\/items\/[0-9a-f-]{36}$/);
    await expect(testId(page, 'item-availability-chip')).toHaveText('Available');
    await expect(testId(page, 'item-loan-history-row')).toHaveCount(1);
    await expect(testId(page, 'item-loan-history-table')).toContainText(borrowerName);
    await expect(testId(page, 'item-loan-history-table')).toContainText('Returned');

    // Retire is offered again now that nothing is out.
    await expect(testId(page, 'item-retire-btn')).toBeEnabled();
    await expect(testId(page, 'item-detail-guard')).toHaveCount(0);

    // --- 10. Out-of-band truth --------------------------------------------
    const created = await findItems(request, assetTag);
    expect(created).toHaveLength(1);

    const loans = await loansForItem(request, created[0].id);
    expect(loans, 'exactly one loan should exist for the whole cycle').toHaveLength(1);
    expect(loans[0].isOpen).toBe(false);
    expect(loans[0].returnedAt).not.toBeNull();
    expect(loans[0].loanStatusName).toBe('Returned');
    expect(loans[0].borrowerName).toBe(borrowerName);
  });
});
