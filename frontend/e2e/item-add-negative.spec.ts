import { expect, test } from '@playwright/test';

import { countItems, createItem, findItems, firstCategory, unique } from './support/api';
import { pickSelectOption, testId } from './support/ui';

/**
 * Negative path for the Item create form — TWO cases, which is the ceiling for this
 * screen. No pairwise matrix: per-field boundary coverage lives in the backend unit
 * tests, where it is cheap and exhaustive.
 */
test.describe('Add item — negative paths', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium-desktop', 'Primary desktop flow');
  });

  test('a missing required field blocks the save, renders inline, and persists nothing', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const before = await countItems(request);
    const name = unique('QA Missing Tag Item');

    await page.goto('/items/new');

    // Everything but the required asset tag.
    await testId(page, 'item-add-name').fill(name);
    await pickSelectOption(page, 'item-add-category', category.name);

    await testId(page, 'item-add-save').click();

    // The reason renders on the field, not as a page banner and not as silence.
    await expect(page.getByText('Asset tag is required.')).toBeVisible();
    await expect(page).toHaveURL(/\/items\/new$/);

    // And nothing was written.
    expect(await findItems(request, name)).toHaveLength(0);
    expect(await countItems(request)).toBe(before);
  });

  test('a duplicate asset tag is rejected inline on the field and creates no second item', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-DUPE');

    // The existing holder of the tag.
    await createItem(request, category.id, assetTag);
    const before = await countItems(request);

    await page.goto('/items/new');
    await testId(page, 'item-add-name').fill(`QA Duplicate Attempt ${assetTag}`);
    await testId(page, 'item-add-assettag').fill(assetTag);
    await pickSelectOption(page, 'item-add-category', category.name);

    await testId(page, 'item-add-save').click();

    const fieldError = testId(page, 'item-add-assettag-error');
    await expect(fieldError).toBeVisible();
    await expect(fieldError).toContainText(assetTag);
    await expect(page).toHaveURL(/\/items\/new$/);

    // Exactly one item still carries the tag.
    expect(await findItems(request, assetTag)).toHaveLength(1);
    expect(await countItems(request)).toBe(before);

    // Editing the tag clears the stale server verdict rather than leaving it stuck.
    await testId(page, 'item-add-assettag').fill(`${assetTag}-B`);
    await expect(testId(page, 'item-add-assettag-error')).toHaveCount(0);
  });
});
