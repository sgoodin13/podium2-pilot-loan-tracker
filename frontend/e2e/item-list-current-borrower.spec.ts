import { expect, test } from '@playwright/test';

import { checkoutOutOfBand, createBorrower, createItem, firstCategory, unique } from './support/api';
import { searchList } from './support/ui';

/**
 * DEFECT D5 — regression spec. RED ON PURPOSE.
 *
 * `scr-item-list` declares a "Current borrower" column ("display only, blank if
 * available", ui-spec.md), and `item-list.component.html` renders
 * `row.currentBorrowerName || '—'`. It is ALWAYS the em dash.
 *
 * `ItemService.ListAsync` projects each row through the two-argument
 * `ItemResponse.From(item, isOnLoan)` overload, which hard-codes
 * `currentBorrowerName: null` and `currentLoanId: null`. The paged list resolves only
 * the open/closed FACT (via `GetItemIdsWithOpenLoanAsync`), never the holder — so the
 * column cannot ever be populated. Confirmed directly against the API:
 *
 *   GET /api/items?availability=onloan
 *   → { "isOnLoan": true, "currentBorrowerName": null, "currentLoanId": null }
 *
 * The single-item read (`GetAsync`) uses the other overload and is correct, which is
 * why the detail screen and the retire guard banner both name the holder.
 *
 * FIX: resolve holders for the page in one query — the sibling of the existing
 * `GetItemIdsWithOpenLoanAsync`, returning item id → borrower name — and use it in the
 * list projection. Keeping it to one query is the point; a per-row lookup would put the
 * list back on the N+1 path the data-layer standard forbids.
 *
 * This spec asserts the CORRECT behaviour and turns green when the defect is fixed.
 */
test.describe('Item list — current borrower column', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium-desktop', 'Primary desktop flow');
  });

  test('names the holder in the Current borrower column while the item is on loan', async ({
    page,
    request,
  }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-CURBOR');

    const item = await createItem(request, category.id, assetTag);
    const holder = await createBorrower(request, unique('QA Current Holder'));
    await checkoutOutOfBand(request, item.id, holder.id);

    await page.goto('/items');
    const row = await searchList(page, 'item-list-search', 'item-list-row', assetTag);
    await expect(row).toBeVisible();
    await expect(row.locator('[data-testid="item-availability-chip"]')).toHaveText('On loan');

    await expect(
      row,
      'an item shown as "On loan" must say who has it — the column exists for exactly that',
    ).toContainText(holder.name);
  });

  test('leaves the column blank for an available item', async ({ page, request }) => {
    const category = await firstCategory(request);
    const assetTag = unique('QA-NOBOR');
    await createItem(request, category.id, assetTag);

    await page.goto('/items');
    const row = await searchList(page, 'item-list-search', 'item-list-row', assetTag);
    await expect(row.locator('[data-testid="item-availability-chip"]')).toHaveText('Available');
    await expect(row).toContainText('—');
  });
});
