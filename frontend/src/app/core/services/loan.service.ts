import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  CheckoutRequest,
  Loan,
  LoanStateFilter,
  PagedResult,
  ReturnLoanRequest,
} from '../models/api.models';

export interface LoanListQuery {
  search?: string;
  state?: LoanStateFilter;
  borrowerId?: string;
  itemId?: string;
  page: number;
  pageSize: number;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
}

@Injectable({ providedIn: 'root' })
export class LoanService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/loans';

  list(query: LoanListQuery): Observable<PagedResult<Loan>> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize);

    if (query.search) params = params.set('search', query.search);
    if (query.state && query.state !== 'all') params = params.set('state', query.state);
    if (query.borrowerId) params = params.set('borrowerId', query.borrowerId);
    if (query.itemId) params = params.set('itemId', query.itemId);
    if (query.sortBy) params = params.set('sortBy', query.sortBy);
    if (query.sortDir) params = params.set('sortDir', query.sortDir);

    return this.http.get<PagedResult<Loan>>(this.base, { params });
  }

  get(id: string): Observable<Loan> {
    return this.http.get<Loan>(`${this.base}/${id}`);
  }

  /**
   * The one real business-rule endpoint (BR-1).
   *
   * Returns 409 Conflict with RFC 7807 problem details when the item acquired an
   * open loan between wizard step 2 and commit. That rejection is authoritative:
   * it comes from the database's filtered unique index ux_loans_item_open, not
   * from an application-layer pre-check.
   */
  checkout(request: CheckoutRequest): Observable<Loan> {
    return this.http.post<Loan>('/api/checkout', request);
  }

  /** Closes a loan. The API rejects a non-terminal status — BR §6. */
  return(id: string, request: ReturnLoanRequest): Observable<Loan> {
    return this.http.post<Loan>(`${this.base}/${id}/return`, request);
  }
}
