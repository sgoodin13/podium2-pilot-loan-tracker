import { expect, test } from '@playwright/test';

import { countBorrowers, findBorrowers, unique } from './support/api';
import { testId } from './support/ui';

/**
 * Negative path for the Borrower create form — TWO cases, the ceiling for this screen.
 */
test.describe('Add borrower — negative paths', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium-desktop', 'Primary desktop flow');
  });

  test('a blank name blocks the save, renders inline, and persists nothing', async ({
    page,
    request,
  }) => {
    const before = await countBorrowers(request);
    const department = unique('QA Blank Name Dept');

    await page.goto('/borrowers/new');

    // Optional fields only — the required one left empty.
    await testId(page, 'borrower-add-department').fill(department);
    await testId(page, 'borrower-add-save').click();

    await expect(page.getByText('Name is required.')).toBeVisible();
    await expect(page).toHaveURL(/\/borrowers\/new$/);

    expect(await countBorrowers(request)).toBe(before);
  });

  test('a malformed email is rejected inline and the borrower is not created', async ({
    page,
    request,
  }) => {
    const before = await countBorrowers(request);
    const name = unique('QA Bad Email Borrower');

    await page.goto('/borrowers/new');
    await testId(page, 'borrower-add-name').fill(name);
    await testId(page, 'borrower-add-email').fill('definitely-not-an-email');

    await testId(page, 'borrower-add-save').click();

    await expect(page.getByText('Enter a valid email address', { exact: false })).toBeVisible();
    await expect(page).toHaveURL(/\/borrowers\/new$/);

    expect(await findBorrowers(request, name)).toHaveLength(0);
    expect(await countBorrowers(request)).toBe(before);
  });
});
