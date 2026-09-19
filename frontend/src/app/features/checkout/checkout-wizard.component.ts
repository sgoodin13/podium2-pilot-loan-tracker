import { Component, ElementRef, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioModule } from '@angular/material/radio';
import { MatTableModule } from '@angular/material/table';

import { Borrower, Item, ProblemDetails } from '../../core/models/api.models';
import { BorrowerService } from '../../core/services/borrower.service';
import { ItemService } from '../../core/services/item.service';
import { LoanService } from '../../core/services/loan.service';
import { NotificationService } from '../../core/services/notification.service';
import { EmptyStateComponent } from '../../shared/empty-state.component';

/**
 * The Checkout wizard — `scr-checkout-wizard`, Pattern 06, REQ-3.1 to REQ-3.4.
 *
 * This is the product's one modeled process and the screen the whole pilot is
 * built around. Three steps: select borrower (t1) → select available item (t2) →
 * confirm (t3), with the rejection path (t4) rendered inline on this same screen.
 *
 * The step-2 list is deliberately a SNAPSHOT, not a guarantee. Availability is
 * re-validated atomically at commit by the database's filtered unique index
 * `ux_loans_item_open`. That gap between "shown as available" and "still available
 * at commit" is exactly what the gateway g1 models, and why the rejection state
 * exists as a first-class part of this screen rather than an afterthought.
 */
@Component({
  selector: 'app-checkout-wizard',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatRadioModule,
    MatTableModule,
    MatProgressSpinnerModule,
    EmptyStateComponent,
  ],
  templateUrl: './checkout-wizard.component.html',
  styleUrl: './checkout-wizard.component.scss',
})
export class CheckoutWizardComponent implements OnInit {
  private readonly borrowerService = inject(BorrowerService);
  private readonly itemService = inject(ItemService);
  private readonly loanService = inject(LoanService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  /** 1 = borrower, 2 = item, 3 = confirm. */
  readonly step = signal<1 | 2 | 3>(1);

  readonly borrowers = signal<Borrower[]>([]);
  readonly items = signal<Item[]>([]);
  readonly loadingBorrowers = signal(false);
  readonly loadingItems = signal(false);

  readonly selectedBorrower = signal<Borrower | null>(null);
  readonly selectedItem = signal<Item | null>(null);

  readonly borrowerSearch = signal('');
  readonly itemSearch = signal('');

  readonly submitting = signal(false);

  /**
   * The rejection reason from the API, rendered inline on this screen.
   * Never a toast and never a top-of-page banner — errors belong at the point of
   * the action taken (ui-spec.md line 13; the checkout rejection is its canonical
   * example).
   */
  readonly rejection = signal<string | null>(null);

  readonly itemColumns = ['assetTag', 'name', 'category', 'action'];

  readonly canAdvanceFromBorrower = computed(() => this.selectedBorrower() !== null);
  readonly canAdvanceFromItem = computed(() => this.selectedItem() !== null);

  ngOnInit(): void {
    this.loadBorrowers();
  }

  // --- Step 1: borrower ----------------------------------------------------

  loadBorrowers(): void {
    this.loadingBorrowers.set(true);
    this.borrowerService.listActive(this.borrowerSearch() || undefined).subscribe({
      next: (borrowers) => {
        this.borrowers.set(borrowers);
        this.loadingBorrowers.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.rejection.set(problem.detail ?? 'Could not load borrowers.');
        this.loadingBorrowers.set(false);
      },
    });
  }

  onBorrowerSearch(value: string): void {
    this.borrowerSearch.set(value);
    this.loadBorrowers();
  }

  selectBorrower(borrower: Borrower): void {
    this.selectedBorrower.set(borrower);
  }

  // --- Step 2: item --------------------------------------------------------

  loadItems(): void {
    this.loadingItems.set(true);
    this.itemService.listAvailable(this.itemSearch() || undefined).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loadingItems.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.rejection.set(problem.detail ?? 'Could not load available items.');
        this.loadingItems.set(false);
      },
    });
  }

  onItemSearch(value: string): void {
    this.itemSearch.set(value);
    this.loadItems();
  }

  selectItem(item: Item): void {
    this.selectedItem.set(item);
  }

  isSelected(item: Item): boolean {
    return this.selectedItem()?.id === item.id;
  }

  // --- Navigation ----------------------------------------------------------

  /**
   * The panel that is currently rendered. Only one of the three `@if (step() === N)`
   * branches exists at a time, so this resolves to the live one.
   */
  @ViewChild('stepPanel') private stepPanel?: ElementRef<HTMLElement>;

  /**
   * Every step transition swaps the whole card, which removes the button that had
   * focus and drops focus to `<body>` — leaving a keyboard user to Tab from the top
   * of the document on each of the three steps (Compliance finding F6).
   *
   * The panel carries `tabindex="-1"` so it can receive focus without joining the Tab
   * order. The existing `role="status"` "Step N of 3" announcer covers the
   * announcement; this covers the focus half that CLAUDE.md §Accessibility requires.
   */
  private focusCurrentStep(): void {
    queueMicrotask(() => this.stepPanel?.nativeElement.focus());
  }

  next(): void {
    this.rejection.set(null);

    if (this.step() === 1 && this.canAdvanceFromBorrower()) {
      this.step.set(2);
      this.loadItems();
      this.focusCurrentStep();
      return;
    }

    if (this.step() === 2 && this.canAdvanceFromItem()) {
      this.step.set(3);
      this.focusCurrentStep();
    }
  }

  back(): void {
    this.rejection.set(null);

    if (this.step() === 3) {
      this.step.set(2);
      this.focusCurrentStep();
      return;
    }

    if (this.step() === 2) {
      this.step.set(1);
      this.focusCurrentStep();
    }
  }

  /** Recovery action on the rejection panel — "← Back to item selection". */
  backToItemSelection(): void {
    this.rejection.set(null);
    this.selectedItem.set(null);
    this.step.set(2);
    // Refetch: the item that was taken should no longer appear as available.
    this.loadItems();
    this.focusCurrentStep();
  }

  // --- Step 3: commit ------------------------------------------------------

  /**
   * Commits the checkout (t3), or renders the rejection (t4).
   *
   * A 409 here is the database refusing a second open loan on the item. Nothing
   * was persisted — the insert failed atomically — so the recovery path is simply
   * to pick a different item.
   */
  confirm(): void {
    const borrower = this.selectedBorrower();
    const item = this.selectedItem();

    if (!borrower || !item || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.rejection.set(null);

    this.loanService.checkout({ borrowerId: borrower.id, itemId: item.id }).subscribe({
      next: (loan) => {
        this.submitting.set(false);

        // The confirmation names the specific item instance by asset tag, never
        // just its category — the SME was explicit that "a drill" is not
        // accountable but "Drill #114" is (BR §8).
        this.notifications.success(
          `Checked out — ${loan.itemName} (${loan.itemAssetTag}) to ${loan.borrowerName}.`,
          'checkout-success-snackbar',
        );

        this.router.navigate(['/loans', loan.id]);
      },
      error: (problem: ProblemDetails) => {
        this.submitting.set(false);
        this.rejection.set(
          problem.detail ?? 'Could not complete the checkout. Nothing was saved.',
        );
      },
    });
  }

  restart(): void {
    this.step.set(1);
    this.selectedBorrower.set(null);
    this.selectedItem.set(null);
    this.rejection.set(null);
    this.borrowerSearch.set('');
    this.itemSearch.set('');
    this.loadBorrowers();
    this.focusCurrentStep();
  }
}
