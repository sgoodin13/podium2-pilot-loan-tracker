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

  /**
   * Selecting an item destroys the "Select" button and creates "Selected ✓" in its place.
   * Both buttons shared one `data-testid` until this was found, so every existing spec
   * resolved the selector to the replacement and sailed straight past the focus loss —
   * the attribute that made the flow testable was the same thing hiding the defect.
   */
  test('choosing an item in step 2 keeps focus on the replacement control', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-FOCUSSEL');
    await createItem(request, category.id, assetTag, `QA Focus Select ${assetTag}`);
    const borrower = await createBorrower(request, unique('QA Focus Select Borrower'));

    await page.goto('/checkout');
    await selectBorrowerOption(page, borrower.name);
    await testId(page, 'checkout-next-to-item').click();

    await testId(page, 'checkout-item-search').fill(assetTag);
    await testId(page, `checkout-step2-select-${assetTag}`).click();

    await expect(testId(page, `checkout-step2-selected-${assetTag}`)).toBeFocused();

    // And stepping back from confirm must NOT re-steal focus onto that button — the
    // item is still chosen, but the user did not just choose it.
    await testId(page, 'checkout-next-to-confirm').click();
    await expect(testId(page, 'checkout-step-3')).toBeFocused();

    await page.getByRole('button', { name: '← Back' }).click();
    await expect(testId(page, 'checkout-step-2')).toBeFocused();
  });

  /**
   * The inverse risk of N1: a focus move that fires when it should not.
   *
   * Searching step 2 after choosing an item re-renders the table, which re-creates the
   * "Selected ✓" button. If the "just chose" flag were still set, focus would be yanked
   * out of the search box in the middle of typing — a worse defect than the one the flag
   * was added to fix, and one no assertion about the happy path would ever catch.
   */
  test('searching step 2 after choosing an item does not steal focus from the search box', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-FOCUSSTALE');
    await createItem(request, category.id, assetTag, `QA Focus Stale ${assetTag}`);
    const borrower = await createBorrower(request, unique('QA Focus Stale Borrower'));

    await page.goto('/checkout');
    await selectBorrowerOption(page, borrower.name);
    await testId(page, 'checkout-next-to-item').click();

    await testId(page, 'checkout-item-search').fill(assetTag);
    await testId(page, `checkout-step2-select-${assetTag}`).click();
    await expect(testId(page, `checkout-step2-selected-${assetTag}`)).toBeFocused();

    // Now refine the search. The chosen item still matches, so its Selected button is
    // re-created — focus must stay where the user is typing.
    await testId(page, 'checkout-item-search').click();
    await testId(page, 'checkout-item-search').fill(assetTag.slice(0, -1));

    await expect(testId(page, 'checkout-item-search')).toBeFocused();
  });

  /**
   * Leaving edit mode is the mirror of entering it, and was missed on both detail
   * screens — `loan-detail` got open *and* cancel, these two got only the entry.
   */
  test('leaving item edit mode returns focus to the Edit button', async ({ page, request }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-FOCUSEXIT');
    const item = await createItem(request, category.id, assetTag, `QA Focus Exit ${assetTag}`);

    await page.goto(`/items/${item.id}`);

    await testId(page, 'item-edit-btn').click();
    await expect(testId(page, 'item-edit-name')).toBeFocused();

    // Pristine form — cancels without the discard dialog.
    await page.getByRole('button', { name: 'Cancel' }).click();

    await expect(testId(page, 'item-edit-btn')).toBeFocused();
  });

  /**
   * The worst of the leave-edit-mode paths, and the one that went uncovered longest.
   *
   * With a dirty form, Cancel opens the discard dialog. That dialog sets
   * `restoreFocus: true`, so on close it hands focus back to the Cancel button — and the
   * component then destroys that button by leaving edit mode. Focus lands on a detached
   * node, which reads as `<body>`. The restore and the teardown fight each other, and the
   * restore runs first.
   */
  test('discarding dirty item edits returns focus to the Edit button, not a detached node', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-FOCUSDIRTY');
    const item = await createItem(request, category.id, assetTag, `QA Focus Dirty ${assetTag}`);

    await page.goto(`/items/${item.id}`);
    await testId(page, 'item-edit-btn').click();
    await expect(testId(page, 'item-edit-name')).toBeFocused();

    // Dirty the form so Cancel routes through the discard dialog.
    await testId(page, 'item-edit-name').fill(`QA Focus Dirty Edited ${assetTag}`);

    await page.getByRole('button', { name: 'Cancel' }).click();
    await expect(testId(page, 'item-edit-discard-message')).toBeVisible();

    await testId(page, 'confirm-accept').click();

    await expect(testId(page, 'item-edit-btn')).toBeFocused();
  });

  test('entering and leaving borrower edit mode both move focus', async ({ page, request }) => {
    const borrower = await createBorrower(request, unique('QA Focus Borrower Edit'));

    await page.goto(`/borrowers/${borrower.id}`);

    // Entry was fixed early and went three rounds with no assertion covering it.
    await testId(page, 'borrower-edit-btn').click();
    await expect(testId(page, 'borrower-edit-name')).toBeFocused();

    await page.getByRole('button', { name: 'Cancel' }).click();

    await expect(testId(page, 'borrower-edit-btn')).toBeFocused();
  });
});
