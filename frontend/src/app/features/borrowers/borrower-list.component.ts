import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { Borrower, ProblemDetails } from '../../core/models/api.models';
import { BorrowerService } from '../../core/services/borrower.service';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { SkeletonRowsComponent } from '../../shared/skeleton-rows.component';

/**
 * Borrowers list — REQ-2.1, `scr-borrower-list`, Pattern 08 (simple flat grid).
 *
 * Server-side pagination and sorting: the grid never holds the whole table, so
 * search, sort and page all round-trip through BorrowerService.list()
 * (CLAUDE.md — "paginate any list endpoint").
 *
 * "Open loans" is the column that matters here: it is the signal Staff read
 * before attempting a deactivate, which is hard-blocked while a loan is open
 * (BR §8, SME-resolved).
 */
@Component({
  selector: 'app-borrower-list',
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
    EmptyStateComponent,
    SkeletonRowsComponent,
  ],
  templateUrl: './borrower-list.component.html',
  styles: [
    `
      .search-field {
        width: 100%;
        max-width: 320px;
        font-size: 13px;
      }

      /* Rows are a navigation affordance: pointer, hover band and a visible
         focus ring, since they are reachable by Tab as well as by click. */
      tr.borrower-row {
        cursor: pointer;
      }

      tr.borrower-row:hover {
        background: var(--surface-2);
      }

      tr.borrower-row:focus-visible {
        outline: 2px solid var(--navy);
        outline-offset: -2px;
      }

      .open-loans {
        font-variant-numeric: tabular-nums;
      }
    `,
  ],
})
export class BorrowerListComponent implements OnInit {
  private readonly borrowers = inject(BorrowerService);
  private readonly router = inject(Router);

  readonly rows = signal<Borrower[]>([]);
  readonly total = signal(0);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  readonly search = signal('');
  readonly pageIndex = signal(0);
  readonly pageSize = signal(25);
  readonly sortBy = signal<string | undefined>(undefined);
  readonly sortDir = signal<'asc' | 'desc' | undefined>(undefined);

  readonly displayedColumns = ['name', 'department', 'contact', 'openLoans'];

  /** Guards against an earlier, slower response overwriting a later one. */
  private requestSeq = 0;

  private readonly searchInput$ = new Subject<string>();

  constructor() {
    this.searchInput$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((term) => {
        this.search.set(term);
        this.pageIndex.set(0);
        this.load();
      });
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);

    const seq = ++this.requestSeq;

    this.borrowers
      .list({
        search: this.search().trim() || undefined,
        page: this.pageIndex() + 1, // the API's page parameter is 1-based
        pageSize: this.pageSize(),
        sortBy: this.sortBy(),
        sortDir: this.sortDir(),
      })
      .subscribe({
        next: (result) => {
          if (seq !== this.requestSeq) return;
          this.rows.set(result.items);
          this.total.set(result.totalCount);
          this.loading.set(false);
        },
        error: (problem: ProblemDetails) => {
          if (seq !== this.requestSeq) return;
          this.loadError.set(problem.detail ?? problem.title ?? 'Could not load borrowers.');
          this.rows.set([]);
          this.total.set(0);
          this.loading.set(false);
        },
      });
  }

  onSearchInput(value: string): void {
    this.searchInput$.next(value);
  }

  onSort(sort: Sort): void {
    if (!sort.direction) {
      this.sortBy.set(undefined);
      this.sortDir.set(undefined);
    } else {
      this.sortBy.set(sort.active);
      this.sortDir.set(sort.direction);
    }
    this.pageIndex.set(0);
    this.load();
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  clearFilters(): void {
    this.search.set('');
    this.pageIndex.set(0);
    this.load();
  }

  open(borrower: Borrower): void {
    void this.router.navigate(['/borrowers', borrower.id]);
  }

  /** Enter and Space both activate a focused row, matching button semantics. */
  onRowKeydown(event: KeyboardEvent, borrower: Borrower): void {
    if (event.key === 'Enter' || event.key === ' ' || event.key === 'Spacebar') {
      event.preventDefault();
      this.open(borrower);
    }
  }

  /** Email, falling back to phone, then an em dash — never a blank cell. */
  contactOf(borrower: Borrower): string {
    return borrower.contactEmail || borrower.contactPhone || '—';
  }

  get isFiltered(): boolean {
    return this.search().trim().length > 0;
  }
}
