import { expect, test } from '@playwright/test';

import {
  checkoutOutOfBand,
  createBorrower,
  createItem,
  firstCategory,
  loansForItem,
  unique,
} from './support/api';
import { driveWizardToConfirm, selectBorrowerOption, testId } from './support/ui';

/**
 * THE BLOCKED PATH — BR-1, the rule this pilot exists to prove.
 *
 * Driven SEQUENTIALLY, never as a race: `Podium2_Template.md:246` bars concurrency and
 * fault-injection testing. The stale snapshot is produced by a second actor checking the
 * item out while the wizard sits on its confirm step — which is exactly the gateway the
 * business process models (g1), and exactly what the database's filtered unique index
 * `ux_loans_item_open` is there to catch at commit time.
 */
test.describe('Checkout — the rejection path', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium-desktop', 'Primary desktop flow');
  });

  test('a second checkout of the same item is visibly rejected and persists no second loan', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-REJ');
    const itemName = `QA Reject Drill ${assetTag}`;

    const item = await createItem(request, category.id, assetTag, itemName);
    const holder = await createBorrower(request, unique('QA Holder'));
    const latecomer = await createBorrower(request, unique('QA Latecomer'));

    // The wizard draws its step-2 list and reaches Confirm while the item is free.
    await driveWizardToConfirm(page, { borrowerName: latecomer.name, assetTag });
    await expect(testId(page, 'checkout-confirm-summary')).toContainText(assetTag);

    // Someone else checks the SAME item out first. Sequential, not concurrent.
    const winningLoan = await checkoutOutOfBand(request, item.id, holder.id);
    expect(winningLoan.isOpen).toBe(true);

    // The wizard's snapshot is now stale. Commit anyway — the database decides.
    await testId(page, 'checkout-confirm').click();

    // --- The rejection is visible, inline, and specific --------------------
    const rejection = testId(page, 'checkout-rejection-panel');
    await expect(rejection).toBeVisible();
    await expect(rejection).toHaveAttribute('role', 'alert');
    await expect(rejection).toContainText(itemName);
    await expect(rejection).toContainText(assetTag);
    await expect(rejection).toContainText('Nothing was saved');

    // It is NOT a toast and NOT a top-of-page banner — it is on the wizard itself.
    await expect(page.locator('.checkout-success-snackbar')).toHaveCount(0);
    await expect(page).toHaveURL(/\/checkout$/);

    // The Confirm action is withdrawn and replaced by a recovery action.
    await expect(testId(page, 'checkout-confirm')).toHaveCount(0);
    await expect(testId(page, 'checkout-back-to-item-selection')).toBeVisible();

    // --- Out-of-band: nothing was persisted --------------------------------
    const loans = await loansForItem(request, item.id);
    expect(loans, 'the rejected checkout must not have created a second loan').toHaveLength(1);
    expect(loans[0].id).toBe(winningLoan.id);
    expect(loans[0].borrowerId).toBe(holder.id);
    expect(loans.filter((l) => l.borrowerId === latecomer.id)).toHaveLength(0);

    // --- Recovery: the taken item is no longer offered ---------------------
    await testId(page, 'checkout-back-to-item-selection').click();
    await expect(testId(page, 'checkout-step-2')).toBeVisible();
    await expect(testId(page, 'checkout-rejection-panel')).toHaveCount(0);

    await testId(page, 'checkout-item-search').fill(assetTag);
    await expect(testId(page, `checkout-step2-select-${assetTag}`)).toHaveCount(0);
    await expect(testId(page, 'checkout-item-empty')).toBeVisible();
  });

  test('an item already on loan is never offered by the wizard in the first place', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-HIDDEN');

    const item = await createItem(request, category.id, assetTag);
    const holder = await createBorrower(request, unique('QA Hidden Holder'));
    const other = await createBorrower(request, unique('QA Hidden Other'));

    await checkoutOutOfBand(request, item.id, holder.id);

    await page.goto('/checkout');
    await selectBorrowerOption(page, other.name);
    await testId(page, 'checkout-next-to-item').click();

    await expect(testId(page, 'checkout-step-2')).toBeVisible();
    await testId(page, 'checkout-item-search').fill(assetTag);

    await expect(testId(page, `checkout-step2-select-${assetTag}`)).toHaveCount(0);
    await expect(testId(page, 'checkout-item-empty')).toBeVisible();

    // Still exactly one loan — the UI-level filter changed nothing in the ledger.
    expect(await loansForItem(request, item.id)).toHaveLength(1);
  });

  test('retiring an item is hard-blocked, with the reason stated on screen', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-GUARD');

    const item = await createItem(request, category.id, assetTag);
    const holder = await createBorrower(request, unique('QA Guard Holder'));
    await checkoutOutOfBand(request, item.id, holder.id);

    await page.goto(`/items/${item.id}`);

    await expect(testId(page, 'item-availability-chip')).toHaveText('On loan');
    await expect(testId(page, 'item-retire-btn')).toBeDisabled();

    const guard = testId(page, 'item-detail-guard');
    await expect(guard).toBeVisible();
    await expect(guard).toContainText('Retire is disabled');
    await expect(guard).toContainText(holder.name);
    await expect(guard).toContainText('Return it first');
  });

  test('deactivating a borrower is hard-blocked, naming the item still held', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-BGUARD');

    const item = await createItem(request, category.id, assetTag);
    const holder = await createBorrower(request, unique('QA BGuard Holder'));
    await checkoutOutOfBand(request, item.id, holder.id);

    await page.goto(`/borrowers/${holder.id}`);

    await expect(testId(page, 'borrower-deactivate-btn')).toBeDisabled();

    const guard = testId(page, 'borrower-detail-guard');
    await expect(guard).toBeVisible();
    await expect(guard).toContainText('Deactivate is disabled');
    await expect(guard).toContainText('1 open loan');
    await expect(guard).toContainText(assetTag);
  });
});
