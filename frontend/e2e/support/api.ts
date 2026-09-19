import { APIRequestContext, expect } from '@playwright/test';

/**
 * Out-of-band access to the API, straight to http://localhost:5080 rather than
 * through the Angular dev-server proxy.
 *
 * This is the TRUTH channel only. Assertions about what the user can see are made
 * against rendered text on screen; this module exists so a spec can independently
 * confirm what did — or did not — get persisted, and to build fixtures for specs
 * whose subject is not the create form.
 */
export const API = 'http://localhost:5080';

export interface ItemRow {
  id: string;
  name: string;
  assetTag: string;
  isOnLoan: boolean;
  itemCategoryId: string;
  itemCategoryName: string;
  currentBorrowerName: string | null;
}

export interface BorrowerRow {
  id: string;
  name: string;
  isActive: boolean;
  openLoanCount: number;
}

export interface LoanRow {
  id: string;
  itemId: string;
  itemAssetTag: string;
  borrowerId: string;
  borrowerName: string;
  loanStatusName: string;
  returnedAt: string | null;
  isOpen: boolean;
}

interface Paged<T> {
  items: T[];
  totalCount: number;
}

/** A short, collision-free suffix so every spec's fixtures are its own. */
export function unique(prefix: string): string {
  const stamp = Date.now().toString(36).toUpperCase();
  const salt = Math.random().toString(36).slice(2, 6).toUpperCase();
  return `${prefix}-${stamp}${salt}`;
}

export async function firstCategory(request: APIRequestContext): Promise<{ id: string; name: string }> {
  const response = await request.get(`${API}/api/item-categories`);
  expect(response.ok()).toBeTruthy();
  const categories = (await response.json()) as { id: string; name: string }[];
  expect(categories.length).toBeGreaterThan(0);
  return categories[0];
}

export async function terminalStatus(
  request: APIRequestContext,
  name = 'Returned',
): Promise<{ id: string; name: string }> {
  const response = await request.get(`${API}/api/loan-statuses?terminalOnly=true`);
  expect(response.ok()).toBeTruthy();
  const statuses = (await response.json()) as { id: string; name: string }[];
  const match = statuses.find((s) => s.name === name);
  expect(match, `terminal status "${name}" must exist`).toBeTruthy();
  return match!;
}

export async function createItem(
  request: APIRequestContext,
  categoryId: string,
  assetTag = unique('QA-E2E'),
  name = `QA E2E Item ${assetTag}`,
): Promise<ItemRow> {
  const response = await request.post(`${API}/api/items`, {
    data: { name, description: 'Synthetic E2E fixture', assetTag, itemCategoryId: categoryId },
  });
  expect(response.status(), await response.text()).toBe(201);
  return (await response.json()) as ItemRow;
}

export async function createBorrower(
  request: APIRequestContext,
  name = unique('QA E2E Borrower'),
): Promise<BorrowerRow> {
  const response = await request.post(`${API}/api/borrowers`, {
    data: { name, contactEmail: null, contactPhone: null, department: 'QA' },
  });
  expect(response.status(), await response.text()).toBe(201);
  return (await response.json()) as BorrowerRow;
}

/** Checks an item out directly — used to make a wizard's step-2 snapshot genuinely stale. */
export async function checkoutOutOfBand(
  request: APIRequestContext,
  itemId: string,
  borrowerId: string,
): Promise<LoanRow> {
  const response = await request.post(`${API}/api/checkout`, { data: { itemId, borrowerId } });
  expect(response.status(), await response.text()).toBe(201);
  return (await response.json()) as LoanRow;
}

/** Every loan recorded against an item, open or closed. */
export async function loansForItem(
  request: APIRequestContext,
  itemId: string,
): Promise<LoanRow[]> {
  const response = await request.get(`${API}/api/loans?itemId=${itemId}&pageSize=200`);
  expect(response.ok()).toBeTruthy();
  return ((await response.json()) as Paged<LoanRow>).items;
}

export async function itemById(request: APIRequestContext, itemId: string): Promise<ItemRow> {
  const response = await request.get(`${API}/api/items/${itemId}`);
  expect(response.ok()).toBeTruthy();
  return (await response.json()) as ItemRow;
}

/** Items matching a search term — used to prove a rejected create persisted nothing. */
export async function findItems(
  request: APIRequestContext,
  search: string,
): Promise<ItemRow[]> {
  const response = await request.get(
    `${API}/api/items?search=${encodeURIComponent(search)}&pageSize=200`,
  );
  expect(response.ok()).toBeTruthy();
  return ((await response.json()) as Paged<ItemRow>).items;
}

/** Total row count, used to prove a rejected create added nothing at all. */
export async function countItems(request: APIRequestContext): Promise<number> {
  const response = await request.get(`${API}/api/items?page=1&pageSize=1`);
  expect(response.ok()).toBeTruthy();
  return ((await response.json()) as Paged<ItemRow>).totalCount;
}

export async function countBorrowers(request: APIRequestContext): Promise<number> {
  const response = await request.get(`${API}/api/borrowers?page=1&pageSize=1`);
  expect(response.ok()).toBeTruthy();
  return ((await response.json()) as Paged<BorrowerRow>).totalCount;
}

export async function findBorrowers(
  request: APIRequestContext,
  search: string,
): Promise<BorrowerRow[]> {
  const response = await request.get(
    `${API}/api/borrowers?search=${encodeURIComponent(search)}&pageSize=200`,
  );
  expect(response.ok()).toBeTruthy();
  return ((await response.json()) as Paged<BorrowerRow>).items;
}
