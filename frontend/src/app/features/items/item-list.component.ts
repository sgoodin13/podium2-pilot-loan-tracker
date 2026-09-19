import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort, SortDirection } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';

import {
  AvailabilityFilter,
  Item,
  ItemCategory,
  ProblemDetails,
} from '../../core/models/api.models';
import { ItemService } from '../../core/services/item.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { SkeletonRowsComponent } from '../../shared/skeleton-rows.component';

/**
 * Items list — REQ-1.2, `scr-item-list`, Pattern 08 (simple flat grid).
 *
 * Availability is DERIVED from open-loan state (`item.isOnLoan`), never a stored
 * column — Logical/Physical Data Model, restated in ui-spec.md. The chip carries
 * its own text label so colour is never the sole signal.
 *
 * Paging, sorting and filtering are all server-side: the API owns the page
 * window (`PagedResult<T>`), so MatPaginator and MatSort are driven as inputs and
 * their events refetch rather than re-slicing a client-side array.
 */
@Component({
  selector: 'app-item-list',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    EmptyStateComponent,
    SkeletonRowsComponent,
  ],
  templateUrl: './item-list.component.html',
  styleUrl: './item-list.scss',
})
export class ItemListComponent implements OnInit, OnDestroy {
  private readonly itemService = inject(ItemService);
  private readonly referenceData = inject(ReferenceDataService);
  private readonly router = inject(Router);

  private readonly destroyed = new Subject<void>();
  private readonly searchChanged = new Subject<string>();

  readonly items = signal<Item[]>([]);
  readonly categories = signal<ItemCategory[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  /** Filter state. Empty categoryId means "All categories". */
  search = '';
  categoryId = '';
  availability: AvailabilityFilter = 'all';

  /** Paging state — pageIndex is zero-based for MatPaginator, the API is 1-based. */
  readonly pageIndex = signal(0);
  readonly pageSize = signal(25);
  readonly pageSizeOptions = [10, 25, 50, 100];

  /** Sort state — column ids match the API's `sortBy` contract. */
  readonly sortBy = signal<string>('assetTag');
  readonly sortDir = signal<SortDirection>('asc');

  readonly displayedColumns = ['assetTag', 'name', 'category', 'availability', 'borrower'];

  ngOnInit(): void {
    this.loadCategories();
    this.load();

    // Debounced so typing in the search box does not fire a request per keystroke.
    this.searchChanged
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroyed))
      .subscribe((value) => {
        this.search = value;
        this.pageIndex.set(0);
        this.load();
      });
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.itemService
      .list({
        search: this.search.trim() || undefined,
        categoryId: this.categoryId || undefined,
        availability: this.availability,
        page: this.pageIndex() + 1,
        pageSize: this.pageSize(),
        sortBy: this.sortBy(),
        sortDir: this.sortDir() === 'desc' ? 'desc' : 'asc',
      })
      .subscribe({
        next: (result) => {
          this.items.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: (problem: ProblemDetails) => {
          this.items.set([]);
          this.totalCount.set(0);
          this.loadError.set(problem.detail ?? problem.title ?? 'Could not load items.');
          this.loading.set(false);
        },
      });
  }

  private loadCategories(): void {
    this.referenceData.listCategories().subscribe({
      next: (categories) => this.categories.set(categories),
      // A failed category lookup must not blank the list: the filter simply
      // offers "All categories" only, and the grid still loads.
      error: () => this.categories.set([]),
    });
  }

  onSearchInput(value: string): void {
    this.searchChanged.next(value);
  }

  onFilterChange(): void {
    this.pageIndex.set(0);
    this.load();
  }

  onSortChange(sort: Sort): void {
    // Clearing a sort (third click) falls back to the default asset-tag order
    // rather than an undefined server-side ordering.
    this.sortBy.set(sort.direction ? sort.active : 'assetTag');
    this.sortDir.set(sort.direction || 'asc');
    this.pageIndex.set(0);
    this.load();
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  clearFilters(): void {
    this.search = '';
    this.categoryId = '';
    this.availability = 'all';
    this.pageIndex.set(0);
    this.load();
  }

  /** True when any filter is applied — drives which empty state is shown. */
  get hasActiveFilters(): boolean {
    return !!this.search.trim() || !!this.categoryId || this.availability !== 'all';
  }

  open(item: Item): void {
    void this.router.navigate(['/items', item.id]);
  }

  onRowKeydown(event: KeyboardEvent, item: Item): void {
    if (event.key === 'Enter' || event.key === ' ' || event.key === 'Spacebar') {
      event.preventDefault();
      this.open(item);
    }
  }
}
