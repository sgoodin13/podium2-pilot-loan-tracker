import { Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { forkJoin } from 'rxjs';

import { Item, ItemCategory, Loan, ProblemDetails } from '../../core/models/api.models';
import { ItemService } from '../../core/services/item.service';
import { NotificationService } from '../../core/services/notification.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../shared/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { FocusOnCreateDirective } from '../../shared/focus-on-create.directive';
import { focusWhenRendered } from '../../shared/focus';
import { GuardBannerComponent } from '../../shared/guard-banner.component';

/**
 * Item detail — REQ-1.3, `scr-item-detail`, Pattern 12 (one-to-many list):
 * parent Item card + read-only child grid of this item's loans.
 *
 * THE RETIRE HARD BLOCK (SME ruling, BR §8, resolved). While the item has an
 * open loan, Retire is disabled AND a guard banner states the reason and the
 * remedy. A disabled control with no stated reason is exactly the failure mode
 * the SME argued against — the visible reason is the requirement, the `title`
 * attribute is not sufficient on its own.
 *
 * Edit is an INLINE mode on this same card, not a separate screen: the modeling
 * standard allows exactly one edit surface per aggregate, and the Add screen is
 * the create surface.
 */
@Component({
  selector: 'app-item-detail',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatTableModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    EmptyStateComponent,
    GuardBannerComponent,
    FocusOnCreateDirective,
  ],
  templateUrl: './item-detail.component.html',
  styleUrl: './item-form.scss',
})
export class ItemDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(FormBuilder);
  private readonly itemService = inject(ItemService);
  private readonly referenceData = inject(ReferenceDataService);
  private readonly notifications = inject(NotificationService);
  private readonly dialog = inject(MatDialog);

  readonly item = signal<Item | null>(null);
  readonly loans = signal<Loan[]>([]);
  readonly categories = signal<ItemCategory[]>([]);

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly retiring = signal(false);
  /** 409 on save — rendered inline on the asset-tag field, not page-level. */
  readonly assetTagServerError = signal<string | null>(null);
  readonly formError = signal<string | null>(null);
  /** 422 from the retire hard block, if it is somehow reached. */
  readonly retireError = signal<string | null>(null);

  readonly loanColumns = ['borrower', 'checkedOut', 'returned', 'status'];

  private itemId = '';

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(1000)]],
    assetTag: ['', [Validators.required, Validators.maxLength(64)]],
    itemCategoryId: ['', [Validators.required]],
  });

  ngOnInit(): void {
    this.itemId = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();

    this.referenceData.listCategories().subscribe({
      next: (categories) => this.categories.set(categories),
      error: () => this.categories.set([]),
    });

    this.form.controls.assetTag.valueChanges.subscribe(() => {
      if (this.assetTagServerError()) this.assetTagServerError.set(null);
    });
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);

    forkJoin({
      item: this.itemService.get(this.itemId),
      loans: this.itemService.loanHistory(this.itemId),
    }).subscribe({
      next: ({ item, loans }) => {
        this.item.set(item);
        // The API returns these most-recent-first; sorting here as well keeps the
        // "most recent first" contract true regardless of upstream ordering.
        this.loans.set(
          [...loans].sort((a, b) => b.checkedOutAt.localeCompare(a.checkedOutAt)),
        );
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.loadError.set(problem.detail ?? problem.title ?? 'Could not load this item.');
        this.loading.set(false);
      },
    });
  }

  // --- Inline edit ---------------------------------------------------------

  startEdit(): void {
    const item = this.item();
    if (!item) return;

    this.form.reset({
      name: item.name,
      description: item.description ?? '',
      assetTag: item.assetTag,
      itemCategoryId: item.itemCategoryId,
    });
    this.formError.set(null);
    this.assetTagServerError.set(null);
    this.editing.set(true);

    // Entering edit mode replaces the read-only view with the form. Focus into the
    // name input is handled by `ltFocusOnCreate` on the element itself.
  }

  /** The Edit button, which is disabled during edit rather than removed. */
  @ViewChild('editButton', { read: ElementRef }) private editButton?: ElementRef<HTMLElement>;

  /**
   * Leaving edit mode destroys the whole form, including the Cancel button the user just
   * pressed, and focus falls to `<body>` (Compliance N2).
   *
   * Unlike every other case on this screen the target is not newly created — the Edit
   * button stays in the DOM throughout and is only re-enabled — so `ltFocusOnCreate` does
   * not apply and the move is made here. It is deferred because a disabled element cannot
   * take focus: the button is still disabled until change detection processes
   * `editing.set(false)`.
   *
   * The dirty branch needs it most. That dialog sets `restoreFocus: true`, so focus is
   * handed back to the Cancel button and *then* that button is destroyed, landing on a
   * detached node.
   */
  private focusEditButton(): void {
    focusWhenRendered(() => this.editButton);
  }

  cancelEdit(): void {
    if (!this.form.dirty) {
      this.editing.set(false);
      this.focusEditButton();
      return;
    }

    const data: ConfirmDialogData = {
      tier: 2,
      title: 'Discard these changes?',
      message: 'Your edits to this item have not been saved. Leaving edit mode discards them.',
      confirmLabel: 'Discard',
      cancelLabel: 'Keep editing',
      destructive: true,
      testId: 'item-edit-discard',
    };

    this.dialog
      .open(ConfirmDialogComponent, { data, autoFocus: 'dialog', restoreFocus: true })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) {
          this.form.markAsPristine();
          this.editing.set(false);
          this.focusEditButton();
        }
      });
  }

  save(): void {
    this.formError.set(null);
    this.assetTagServerError.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();

    this.itemService
      .update(this.itemId, {
        name: value.name.trim(),
        description: value.description.trim() || null,
        assetTag: value.assetTag.trim(),
        itemCategoryId: value.itemCategoryId,
      })
      .subscribe({
        next: (updated) => {
          this.saving.set(false);
          this.form.markAsPristine();
          this.item.set(updated);
          this.editing.set(false);
          this.focusEditButton();
          this.notifications.success(`Item saved — ${updated.name} (${updated.assetTag}).`);
        },
        error: (problem: ProblemDetails) => {
          this.saving.set(false);
          const assetTagDetail = this.assetTagDetail(problem);
          if (assetTagDetail) {
            this.assetTagServerError.set(assetTagDetail);
            this.form.controls.assetTag.setErrors({ duplicate: true });
            this.form.controls.assetTag.markAsTouched();
          } else {
            this.formError.set(problem.detail ?? problem.title ?? 'Could not save this item.');
          }
        },
      });
  }

  // --- Retire (soft delete) ------------------------------------------------

  /**
   * The hard block, stated in words. `currentBorrowerName` is what the API gives
   * us; if it is somehow absent the sentence still reads correctly.
   */
  get guardMessage(): string {
    const borrower = this.item()?.currentBorrowerName;
    return borrower
      ? `Retire is disabled — this item has an open loan to ${borrower}. Return it first.`
      : 'Retire is disabled — this item has an open loan. Return it first.';
  }

  retire(): void {
    const item = this.item();
    if (!item || item.isOnLoan) return;

    this.retireError.set(null);

    // Tier 2: reversible but consequential. Retiring is a SOFT delete — the row
    // and its loan history survive — so this is not the Tier 3 acknowledgement
    // ceremony, but it names the specific item and asset tag (Standards Guide C3).
    const data: ConfirmDialogData = {
      tier: 2,
      title: 'Retire this item?',
      message:
        `${item.name} (${item.assetTag}) will be retired and will no longer be offered for ` +
        'checkout. This is a soft delete — the item and its loan history are kept, and it ' +
        'can be reactivated.',
      confirmLabel: 'Retire item',
      destructive: true,
      testId: 'item-retire',
    };

    this.dialog
      .open(ConfirmDialogComponent, { data, autoFocus: 'dialog', restoreFocus: true })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) return;

        this.retiring.set(true);
        this.itemService.retire(item.id).subscribe({
          next: (retired) => {
            this.retiring.set(false);
            this.item.set(retired);
            this.notifications.success(`Item retired — ${retired.name} (${retired.assetTag}).`);
            this.load();
          },
          error: (problem: ProblemDetails) => {
            this.retiring.set(false);
            // 422 is the server-side half of the same hard block: the UI state was
            // stale and an open loan appeared. Show the server's exact reason.
            this.retireError.set(
              problem.detail ?? problem.title ?? 'Could not retire this item.',
            );
            this.load();
          },
        });
      });
  }

  // --- Helpers -------------------------------------------------------------

  /** YYYY-MM-DD, in local time — the format every date in the mockups uses. */
  formatDate(iso: string | null): string {
    if (!iso) return '—';
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) return '—';
    const pad = (n: number) => `${n}`.padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  }

  private assetTagDetail(problem: ProblemDetails): string | null {
    const fieldErrors = problem.errors ?? {};
    const key = Object.keys(fieldErrors).find((k) => k.toLowerCase() === 'assettag');
    if (key && fieldErrors[key]?.length) return fieldErrors[key].join(' ');

    if (problem.status === 409) {
      return problem.detail ?? 'That asset tag is already in use.';
    }
    return null;
  }

  /**
   * Confirm-before-navigate contract (ui-spec.md cross-screen states). Only the
   * inline edit mode can hold unsaved work on a detail screen.
   */
  hasUnsavedChanges(): boolean {
    return this.editing() && this.form.dirty && !this.saving();
  }
}
