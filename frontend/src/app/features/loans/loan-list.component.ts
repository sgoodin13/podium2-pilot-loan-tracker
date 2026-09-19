import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';

import { Borrower, Item, Loan, LoanStateFilter, ProblemDetails } from '../../core/models/api.models';
import { BorrowerService } from '../../core/services/borrower.service';
import { ItemService } from '../../core/services/item.service';
import { LoanService } from '../../core/services/loan.service';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { SkeletonRowsComponent } from '../../shared/skeleton-rows.component';

/**
 * Loan list — `scr-loan-list`, Pattern 08, REQ-4.1.
 *
 * Deliberately has NO create button: a Loan is created exclusively through the
 * Checkout process (Functional Spec — "no add screen for Loan").
 */
@Component({
  selector: 'app-loan-list',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    EmptyStateComponent,
    SkeletonRowsComponent,
  ],
  templateUrl: './loan-list.component.html',
  styleUrl: './loan-list.component.scss',
})
export class LoanListComponent implements OnInit {
  private readonly loanService = inject(LoanService);
  private readonly borrowerService = inject(BorrowerService);
  private readonly itemService = inject(ItemService);
  private readonly router = inject(Router);

  readonly rows = signal<Loan[]>([]);
  readonly total = signal(0);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  readonly borrowers = signal<Borrower[]>([]);
  readonly items = signal<Item[]>([]);

  readonly search = signal('');
  readonly state = signal<LoanStateFilter>('all');
  readonly borrowerId = signal<string>('');
  readonly itemId = signal<string>('');

  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly sortBy = signal<string>('checkedOutAt');
  readonly sortDir = signal<'asc' | 'desc'>('desc');

  readonly displayedColumns = ['item', 'borrower', 'checkedOut', 'returned', 'status'];

  ngOnInit(): void {
    this.load();

    // Filter option sources. Failures here degrade the filters but must not
    // block the list itself.
    this.borrowerService.listActive().subscribe({
      next: (b) => this.borrowers.set(b),
      error: () => this.borrowers.set([]),
    });
    this.itemService.list({ page: 1, pageSize: 200 }).subscribe({
      next: (result) => this.items.set(result.items),
      error: () => this.items.set([]),
    });
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.loanService
      .list({
        search: this.search() || undefined,
        state: this.state(),
        borrowerId: this.borrowerId() || undefined,
        itemId: this.itemId() || undefined,
        page: this.page(),
        pageSize: this.pageSize(),
        sortBy: this.sortBy(),
        sortDir: this.sortDir(),
      })
      .subscribe({
        next: (result) => {
          this.rows.set(result.items);
          this.total.set(result.totalCount);
          this.loading.set(false);
        },
        error: (problem: ProblemDetails) => {
          this.loadError.set(problem.detail ?? problem.title ?? 'Could not load loans.');
          this.loading.set(false);
        },
      });
  }

  /** Any filter change resets to page 1 — otherwise page 3 of a narrower result set is empty. */
  applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  onSearch(value: string): void {
    this.search.set(value);
    this.applyFilters();
  }

  onPage(event: PageEvent): void {
    this.page.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  onSort(sort: Sort): void {
    this.sortBy.set(sort.active);
    this.sortDir.set(sort.direction === 'desc' ? 'desc' : 'asc');
    this.load();
  }

  clearFilters(): void {
    this.search.set('');
    this.state.set('all');
    this.borrowerId.set('');
    this.itemId.set('');
    this.applyFilters();
  }

  get hasActiveFilters(): boolean {
    return !!(this.search() || this.state() !== 'all' || this.borrowerId() || this.itemId());
  }

  open(loan: Loan): void {
    this.router.navigate(['/loans', loan.id]);
  }

  /** Enter opens the focused row, so the grid is operable without a mouse. */
  onRowKeydown(event: KeyboardEvent, loan: Loan): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.open(loan);
    }
  }
}
