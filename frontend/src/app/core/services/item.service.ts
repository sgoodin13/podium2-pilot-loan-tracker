import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  AvailabilityFilter,
  CreateItemRequest,
  Item,
  Loan,
  PagedResult,
  UpdateItemRequest,
} from '../models/api.models';

export interface ItemListQuery {
  search?: string;
  categoryId?: string;
  availability?: AvailabilityFilter;
  page: number;
  pageSize: number;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
}

@Injectable({ providedIn: 'root' })
export class ItemService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/items';

  list(query: ItemListQuery): Observable<PagedResult<Item>> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize);

    if (query.search) params = params.set('search', query.search);
    if (query.categoryId) params = params.set('categoryId', query.categoryId);
    if (query.availability && query.availability !== 'all') {
      params = params.set('availability', query.availability);
    }
    if (query.sortBy) params = params.set('sortBy', query.sortBy);
    if (query.sortDir) params = params.set('sortDir', query.sortDir);

    return this.http.get<PagedResult<Item>>(this.base, { params });
  }

  /** Items with zero open loans — drives checkout wizard step 2. */
  listAvailable(search?: string): Observable<Item[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<Item[]>(`${this.base}/available`, { params });
  }

  get(id: string): Observable<Item> {
    return this.http.get<Item>(`${this.base}/${id}`);
  }

  /** An item's full loan history, most recent first. */
  loanHistory(id: string): Observable<Loan[]> {
    return this.http.get<Loan[]>(`${this.base}/${id}/loans`);
  }

  create(request: CreateItemRequest): Observable<Item> {
    return this.http.post<Item>(this.base, request);
  }

  update(id: string, request: UpdateItemRequest): Observable<Item> {
    return this.http.put<Item>(`${this.base}/${id}`, request);
  }

  /**
   * Soft-delete (is_active = false). Hard-blocked by the API while the item has
   * an open loan — BR §6, SME-resolved hard block, not a warning.
   */
  retire(id: string): Observable<Item> {
    return this.http.post<Item>(`${this.base}/${id}/retire`, {});
  }
}
