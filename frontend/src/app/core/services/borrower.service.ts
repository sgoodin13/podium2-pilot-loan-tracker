import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  Borrower,
  CreateBorrowerRequest,
  Loan,
  PagedResult,
  UpdateBorrowerRequest,
} from '../models/api.models';

export interface BorrowerListQuery {
  search?: string;
  page: number;
  pageSize: number;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
}

@Injectable({ providedIn: 'root' })
export class BorrowerService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/borrowers';

  list(query: BorrowerListQuery): Observable<PagedResult<Borrower>> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize);

    if (query.search) params = params.set('search', query.search);
    if (query.sortBy) params = params.set('sortBy', query.sortBy);
    if (query.sortDir) params = params.set('sortDir', query.sortDir);

    return this.http.get<PagedResult<Borrower>>(this.base, { params });
  }

  /** Active borrowers only — drives checkout wizard step 1. */
  listActive(search?: string): Observable<Borrower[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<Borrower[]>(`${this.base}/active`, { params });
  }

  get(id: string): Observable<Borrower> {
    return this.http.get<Borrower>(`${this.base}/${id}`);
  }

  loanHistory(id: string): Observable<Loan[]> {
    return this.http.get<Loan[]>(`${this.base}/${id}/loans`);
  }

  create(request: CreateBorrowerRequest): Observable<Borrower> {
    return this.http.post<Borrower>(this.base, request);
  }

  update(id: string, request: UpdateBorrowerRequest): Observable<Borrower> {
    return this.http.put<Borrower>(`${this.base}/${id}`, request);
  }

  /**
   * Soft-delete (is_active = false). Hard-blocked by the API while the borrower
   * holds an open loan — BR §6, SME-resolved hard block.
   */
  deactivate(id: string): Observable<Borrower> {
    return this.http.post<Borrower>(`${this.base}/${id}/deactivate`, {});
  }
}
