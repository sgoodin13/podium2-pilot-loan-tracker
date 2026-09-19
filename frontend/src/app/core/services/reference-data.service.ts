import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  ItemCategory,
  LoanStatus,
  LoanStatusRequest,
  ReferenceDataRequest,
} from '../models/api.models';

/**
 * Item Categories and Loan Statuses are maintained reference data held in real
 * tables — never hardcoded enums. Staff can add a further terminal status
 * without a code change (BR §4).
 *
 * Reference rows are deactivated, never deleted (BR §5.5).
 */
@Injectable({ providedIn: 'root' })
export class ReferenceDataService {
  private readonly http = inject(HttpClient);

  // --- Item categories -----------------------------------------------------

  listCategories(includeInactive = false): Observable<ItemCategory[]> {
    return this.http.get<ItemCategory[]>('/api/item-categories', {
      params: { includeInactive },
    });
  }

  createCategory(request: ReferenceDataRequest): Observable<ItemCategory> {
    return this.http.post<ItemCategory>('/api/item-categories', request);
  }

  updateCategory(id: string, request: ReferenceDataRequest): Observable<ItemCategory> {
    return this.http.put<ItemCategory>(`/api/item-categories/${id}`, request);
  }

  // --- Loan statuses -------------------------------------------------------

  listStatuses(includeInactive = false): Observable<LoanStatus[]> {
    return this.http.get<LoanStatus[]>('/api/loan-statuses', {
      params: { includeInactive },
    });
  }

  /** Terminal statuses only — the set that can legally close a loan. */
  listTerminalStatuses(): Observable<LoanStatus[]> {
    return this.http.get<LoanStatus[]>('/api/loan-statuses', {
      params: { terminalOnly: true },
    });
  }

  createStatus(request: LoanStatusRequest): Observable<LoanStatus> {
    return this.http.post<LoanStatus>('/api/loan-statuses', request);
  }

  updateStatus(id: string, request: LoanStatusRequest): Observable<LoanStatus> {
    return this.http.put<LoanStatus>(`/api/loan-statuses/${id}`, request);
  }
}
