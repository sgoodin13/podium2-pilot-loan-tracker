/**
 * API contract types. These mirror the backend DTOs exactly — EF Core entities
 * never cross the boundary (LoanTracker_Stack_Rules.md [STACK_RULES]).
 */

/** Server-side pagination envelope — every list endpoint returns this. */
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** RFC 7807 problem details, as emitted by the API's global exception handler. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  /** Present on validation failures. */
  errors?: Record<string, string[]>;
  /** The request's trace identifier, for correlating against server logs. */
  traceId?: string;
}

export interface ItemCategory {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
  /**
   * Active items still referencing this category. Drives the deactivation
   * confirmation, so the effect is named before the save rather than after.
   */
  activeItemCount: number;
}

export interface LoanStatus {
  id: string;
  name: string;
  description: string | null;
  isTerminal: boolean;
  isActive: boolean;
}

export interface Item {
  id: string;
  name: string;
  description: string | null;
  assetTag: string;
  itemCategoryId: string;
  itemCategoryName: string;
  isActive: boolean;
  /**
   * Derived from whether an open loan exists — never a stored column.
   * (Logical Data Model; SME ruling, BR §8.)
   */
  isOnLoan: boolean;
  currentBorrowerName: string | null;
  currentLoanId: string | null;
}

export interface Borrower {
  id: string;
  name: string;
  contactEmail: string | null;
  contactPhone: string | null;
  department: string | null;
  isActive: boolean;
  openLoanCount: number;
}

export interface Loan {
  id: string;
  itemId: string;
  itemName: string;
  itemAssetTag: string;
  borrowerId: string;
  borrowerName: string;
  loanStatusId: string;
  loanStatusName: string;
  loanStatusIsTerminal: boolean;
  checkedOutAt: string;
  returnedAt: string | null;
  isOpen: boolean;
}

export interface CreateItemRequest {
  name: string;
  description: string | null;
  assetTag: string;
  itemCategoryId: string;
}

export interface UpdateItemRequest extends CreateItemRequest {}

export interface CreateBorrowerRequest {
  name: string;
  contactEmail: string | null;
  contactPhone: string | null;
  department: string | null;
}

export interface UpdateBorrowerRequest extends CreateBorrowerRequest {}

export interface CheckoutRequest {
  borrowerId: string;
  itemId: string;
}

export interface ReturnLoanRequest {
  loanStatusId: string;
}

export interface ReferenceDataRequest {
  name: string;
  description: string | null;
  isActive: boolean;
}

export interface LoanStatusRequest extends ReferenceDataRequest {
  isTerminal: boolean;
}

/** Availability filter on the item list — "on loan" is derived, not stored. */
export type AvailabilityFilter = 'all' | 'available' | 'onloan';

/** Open/closed filter on the loan list. */
export type LoanStateFilter = 'all' | 'open' | 'closed';
