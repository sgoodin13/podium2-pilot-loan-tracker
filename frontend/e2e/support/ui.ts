import { Locator, Page, expect } from '@playwright/test';

/**
 * UI helpers. Every selector here was confirmed against the real markup in
 * `src/app/features/**`; nothing is invented.
 */

export const testId = (page: Page, id: string) => page.locator(`[data-testid="${id}"]`);

/** Angular Material selects render their options into an overlay, not inline. */
export async function pickSelectOption(
  page: Page,
  selectTestId: string,
  optionLabel: string,
): Promise<void> {
  await testId(page, selectTestId).click();
  await page.getByRole('option', { name: optionLabel, exact: true }).click();
  // The overlay animates out; waiting for it keeps the next click off a backdrop.
  await expect(page.locator('.cdk-overlay-backdrop')).toHaveCount(0);
}

/**
 * Creates an Item through the real Add form — REQ-1.1.
 * Returns once the item's detail screen has rendered.
 */
export async function createItemThroughUi(
  page: Page,
  options: { name: string; assetTag: string; categoryName: string; description?: string },
): Promise<void> {
  await page.goto('/items');
  await testId(page, 'item-add-btn').click();
  await expect(page).toHaveURL(/\/items\/new$/);

  await testId(page, 'item-add-name').fill(options.name);
  if (options.description) {
    await testId(page, 'item-add-description').fill(options.description);
  }
  await testId(page, 'item-add-assettag').fill(options.assetTag);
  await pickSelectOption(page, 'item-add-category', options.categoryName);

  await testId(page, 'item-add-save').click();

  await expect(page).toHaveURL(/\/items\/[0-9a-f-]{36}$/);
  await expect(testId(page, 'item-detail-assettag')).toHaveText(options.assetTag);
}

/** Creates a Borrower through the real Add form — REQ-2.2. */
export async function createBorrowerThroughUi(
  page: Page,
  options: { name: string; email?: string; department?: string },
): Promise<void> {
  await page.goto('/borrowers');
  await testId(page, 'borrower-add-link').click();
  await expect(page).toHaveURL(/\/borrowers\/new$/);

  await testId(page, 'borrower-add-name').fill(options.name);
  if (options.email) await testId(page, 'borrower-add-email').fill(options.email);
  if (options.department) await testId(page, 'borrower-add-department').fill(options.department);

  await testId(page, 'borrower-add-save').click();

  // Saving navigates straight to the new borrower's detail screen. No discard
  // prompt should appear: the form is marked pristine on success, so the
  // unsaved-changes guard has nothing to block (this was defect D4).
  await expect(page).toHaveURL(/\/borrowers\/[0-9a-f-]{36}$/);
}

/**
 * Types into a list screen's search box and returns the ONE row that matches.
 *
 * The search inputs are debounced (300 ms) and the grids are server-paged, so taking
 * `.first()` straight after typing can still be looking at the pre-filter page. Filtering
 * the locator by the asset tag makes the row identity explicit rather than positional.
 */
export async function searchList(
  page: Page,
  searchTestId: string,
  rowTestId: string,
  term: string,
): Promise<Locator> {
  await testId(page, searchTestId).fill(term);

  const row = testId(page, rowTestId).filter({ hasText: term });
  await expect(row).toHaveCount(1);
  return row;
}

/**
 * Picks a borrower in wizard step 1.
 *
 * `mat-radio-button` renders its native input visually hidden behind the ripple, with
 * the visible text in a sibling `<label>`. Clicking the label is what a real user does
 * and is what actually drives the control.
 */
export async function selectBorrowerOption(page: Page, borrowerName: string): Promise<void> {
  await testId(page, 'checkout-borrower-search').fill(borrowerName);

  const option = testId(page, `checkout-borrower-${borrowerName}`);
  await expect(option).toBeVisible();
  await option.locator('label').first().click();
}

/**
 * Drives the checkout wizard as far as the CONFIRM step, without committing.
 * Split out so the rejection spec can let the snapshot go stale before confirming.
 */
export async function driveWizardToConfirm(
  page: Page,
  options: { borrowerName: string; assetTag: string },
): Promise<void> {
  await page.goto('/checkout');
  await expect(testId(page, 'checkout-step-1')).toBeVisible();

  await selectBorrowerOption(page, options.borrowerName);

  const toItem = testId(page, 'checkout-next-to-item');
  await expect(toItem).toBeEnabled();
  await toItem.click();

  await expect(testId(page, 'checkout-step-2')).toBeVisible();
  await testId(page, 'checkout-item-search').fill(options.assetTag);

  const selectItem = testId(page, `checkout-step2-select-${options.assetTag}`);
  await expect(selectItem).toBeVisible();
  await selectItem.click();

  const toConfirm = testId(page, 'checkout-next-to-confirm');
  await expect(toConfirm).toBeEnabled();
  await toConfirm.click();

  await expect(testId(page, 'checkout-step-3')).toBeVisible();
}
