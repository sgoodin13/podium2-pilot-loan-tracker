import { expect, test } from '@playwright/test';

import {
  checkoutOutOfBand,
  createBorrower,
  createItem,
  firstCategory,
  unique,
} from './support/api';
import { driveWizardToConfirm, selectBorrowerOption, testId } from './support/ui';

/**
 * FOCUS MANAGEMENT — the part of WCAG 2.2 AA that axe cannot see.
 *
 * Every assertion here covers a state change that DESTROYS the control holding focus.
 * Without a deliberate focus move, focus falls back to `<body>` and a keyboard user has
 * to Tab from the top of the document to carry on — which axe reports as a clean page,
 * because the markup is faultless. CLAUDE.md §Accessibility requires "focus managed on
 * every state change"; these are the transitions that have one.
 *
 * They also pin a timing assumption. The fixes move focus in a `queueMicrotask` against
 * an optionally-chained `@ViewChild`, so if the microtask ever won the race with Angular's
 * render the focus call would become a silent no-op and nothing else would notice. A
 * `toBeFocused()` assertion is the only thing that would fail.
 */
test.describe('Focus management on destructive state changes', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium-desktop', 'Primary desktop flow');
  });

  test('each checkout wizard step transition moves focus to the new step', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-FOCUS');
    await createItem(request, category.id, assetTag, `QA Focus Drill ${assetTag}`);
    const borrower = await createBorrower(request, unique('QA Focus Borrower'));

    await page.goto('/checkout');
    await expect(testId(page, 'checkout-step-1')).toBeVisible();

    await selectBorrowerOption(page, borrower.name);
    await testId(page, 'checkout-next-to-item').click();

    // Forward 1 → 2: the card carrying the Next button was replaced.
    await expect(testId(page, 'checkout-step-2')).toBeFocused();

    await testId(page, 'checkout-item-search').fill(assetTag);
    await testId(page, `checkout-step2-select-${assetTag}`).click();
    await testId(page, 'checkout-next-to-confirm').click();

    // Forward 2 → 3.
    await expect(testId(page, 'checkout-step-3')).toBeFocused();

    // And backwards, which destroys the focused control just the same.
    // Step 3's Back carries no testid, so it is addressed by its accessible name.
    await page.getByRole('button', { name: '← Back' }).click();
    await expect(testId(page, 'checkout-step-2')).toBeFocused();
  });

  /**
   * The one that matters most: BR-1 rejection. The step does NOT change here, so the
   * step-transition focus move never fires — but the confirm button is still destroyed
   * and replaced by the rejection panel. This was missed on the first pass.
   */
  test('a rejected checkout moves focus to the rejection panel that explains it', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-FOCUSREJ');
    const item = await createItem(request, category.id, assetTag, `QA Focus Reject ${assetTag}`);
    const holder = await createBorrower(request, unique('QA Focus Holder'));
    const latecomer = await createBorrower(request, unique('QA Focus Latecomer'));

    await driveWizardToConfirm(page, { borrowerName: latecomer.name, assetTag });

    // Someone else takes the item while this wizard sits on confirm. Sequential.
    await checkoutOutOfBand(request, item.id, holder.id);

    await testId(page, 'checkout-confirm').click();

    const rejection = testId(page, 'checkout-rejection-panel');
    await expect(rejection).toBeVisible();

    // Focus lands on the panel carrying the reason, not on <body>. The only recovery
    // control on screen sits inside it.
    await expect(rejection).toBeFocused();
    await expect(rejection).toContainText(assetTag);
  });

  test('opening and cancelling the return panel moves focus both ways', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-FOCUSRET');
    const item = await createItem(request, category.id, assetTag, `QA Focus Return ${assetTag}`);
    const borrower = await createBorrower(request, unique('QA Focus Returner'));
    const loan = await checkoutOutOfBand(request, item.id, borrower.id);

    await page.goto(`/loans/${loan.id}`);

    await testId(page, 'loan-return-open').click();

    // Opening destroys the trigger; focus moves to the heading, which names the item
    // and says the loan cannot be reopened.
    const heading = testId(page, 'loan-return-heading');
    await expect(heading).toBeFocused();
    await expect(heading).toContainText(assetTag);

    await page.getByRole('button', { name: 'Cancel' }).click();

    // Cancelling destroys the panel; focus returns to the control it came from.
    await expect(testId(page, 'loan-return-open')).toBeFocused();
  });

  test('entering item edit mode moves focus into the form', async ({ page, request }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-FOCUSEDIT');
    const item = await createItem(request, category.id, assetTag, `QA Focus Edit ${assetTag}`);

    await page.goto(`/items/${item.id}`);

    await testId(page, 'item-edit-btn').click();

    await expect(testId(page, 'item-edit-name')).toBeFocused();
  });
});
