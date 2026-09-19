import { expect, test } from '@playwright/test';

import { countBorrowers, findBorrowers, unique } from './support/api';
import { testId } from './support/ui';

/**
 * DEFECT D4 — regression spec. RED ON PURPOSE.
 *
 * `BorrowerAddComponent.save()` navigates to the new borrower's detail screen on
 * success, but never calls `form.markAsPristine()`. `hasUnsavedChanges()` therefore
 * still reports true, and `unsavedChangesGuard` intercepts the navigation with
 * "Discard unsaved changes? — This form has changes that have not been saved."
 *
 * The borrower HAS been saved at that point, so the prompt is simply wrong, and both
 * of its answers are bad:
 *   • "Keep editing"    → the user stays on a form whose contents are already
 *                         persisted; pressing Save again creates a DUPLICATE borrower.
 *   • "Discard changes" → the user is told their work was discarded when it was not.
 *
 * `ItemAddComponent.save()` does call `markAsPristine()` and is unaffected, which is
 * what makes this an oversight in one component rather than a design decision.
 *
 * FIX: call `this.form.markAsPristine()` before `router.navigate(...)` in
 * `borrower-add.component.ts`, exactly as `item-add.component.ts` does.
 *
 * This spec asserts the CORRECT behaviour. It must not be softened — it turns green
 * when the defect is fixed.
 */
test.describe('Add borrower — dirty-state guard', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium-desktop', 'Primary desktop flow');
  });

  test('a successful save navigates straight to the new borrower, with no discard prompt', async ({
    page,
    request,
  }) => {
    const name = unique('QA Dirty Guard Borrower');

    await page.goto('/borrowers/new');
    await testId(page, 'borrower-add-name').fill(name);
    await testId(page, 'borrower-add-department').fill('QA');

    await testId(page, 'borrower-add-save').click();

    // The record is saved — confirmed out of band, independently of the UI.
    await expect
      .poll(async () => (await findBorrowers(request, name)).length)
      .toBe(1);

    // So there is nothing unsaved, and nothing to discard.
    await expect(
      page.getByRole('dialog'),
      'a saved form must not claim to have unsaved changes',
    ).toHaveCount(0);

    await expect(page).toHaveURL(/\/borrowers\/[0-9a-f-]{36}$/);
    await expect(page.locator('h1')).toContainText(name);
  });

  test('the discard prompt does not lead to a duplicate borrower', async ({ page, request }) => {
    const name = unique('QA Duplicate Risk Borrower');

    await page.goto('/borrowers/new');
    await testId(page, 'borrower-add-name').fill(name);
    await testId(page, 'borrower-add-save').click();

    await expect.poll(async () => (await findBorrowers(request, name)).length).toBe(1);

    const before = await countBorrowers(request);

    // If the guard stranded the user on the form, "Keep editing" + Save again is the
    // natural next thing a person does — and it must not write a second row.
    const keepEditing = page.getByRole('button', { name: 'Keep editing' });
    if (await keepEditing.isVisible().catch(() => false)) {
      await keepEditing.click();
      await testId(page, 'borrower-add-save').click();
      await page.waitForTimeout(1000);
    }

    expect(
      await findBorrowers(request, name),
      'saving twice from a stranded Add form must not create a duplicate borrower',
    ).toHaveLength(1);
    expect(await countBorrowers(request)).toBe(before);
  });
});
