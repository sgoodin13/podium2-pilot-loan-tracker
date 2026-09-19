import { Component, OnInit, inject, signal } from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { Item, ItemCategory, ProblemDetails } from '../../core/models/api.models';
import { ItemService } from '../../core/services/item.service';
import { NotificationService } from '../../core/services/notification.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../shared/confirm-dialog.component';

/**
 * Add item — REQ-1.1, `scr-item-add`, Pattern 04 (single record form).
 *
 * Asset-tag uniqueness is a SERVER-side rule (BR §5.1): the API answers 409 with
 * a specific reason, and that reason is rendered inline on the asset-tag field —
 * not as a page banner and never as a silent failure (ui-spec.md, Standards
 * Guide C6).
 */
@Component({
  selector: 'app-item-add',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './item-add.component.html',
  styleUrl: './item-form.scss',
})
export class ItemAddComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly itemService = inject(ItemService);
  private readonly referenceData = inject(ReferenceDataService);
  private readonly notifications = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  readonly categories = signal<ItemCategory[]>([]);
  readonly saving = signal(false);
  /** 409 from the API — rendered under the asset-tag field, not page-level. */
  readonly assetTagServerError = signal<string | null>(null);
  /** Anything else that stopped the save — rendered in the form's error panel. */
  readonly formError = signal<string | null>(null);
  readonly categoriesError = signal<string | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(1000)]],
    assetTag: ['', [Validators.required, Validators.maxLength(64)]],
    itemCategoryId: ['', [Validators.required]],
  });

  ngOnInit(): void {
    this.referenceData.listCategories().subscribe({
      next: (categories) => this.categories.set(categories),
      error: (problem: ProblemDetails) =>
        this.categoriesError.set(
          problem.detail ?? problem.title ?? 'Could not load categories.',
        ),
    });

    // A fresh keystroke clears the stale server verdict — the duplicate message
    // must not persist against a tag the user has since changed.
    this.form.controls.assetTag.valueChanges.subscribe(() => {
      if (this.assetTagServerError()) this.assetTagServerError.set(null);
    });
  }

  save(): void {
    this.formError.set(null);
    this.assetTagServerError.set(null);

    if (this.form.invalid) {
      // Touch everything so every unmet requirement is announced at once rather
      // than one field at a time.
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();

    this.itemService
      .create({
        name: value.name.trim(),
        description: value.description.trim() || null,
        assetTag: value.assetTag.trim(),
        itemCategoryId: value.itemCategoryId,
      })
      .subscribe({
        next: (created: Item) => {
          this.saving.set(false);
          this.form.markAsPristine();
          this.notifications.success(`Item saved — ${created.name} (${created.assetTag}).`);
          void this.router.navigate(['/items', created.id]);
        },
        error: (problem: ProblemDetails) => {
          this.saving.set(false);
          const assetTagDetail = this.assetTagDetail(problem);
          if (assetTagDetail) {
            // Held as a real control error so Angular Material renders it via
            // <mat-error> and wires it into the input's aria-describedby. Typing
            // revalidates the control, which clears it.
            this.assetTagServerError.set(assetTagDetail);
            this.form.controls.assetTag.setErrors({ duplicate: true });
            this.form.controls.assetTag.markAsTouched();
            this.focusAssetTag();
          } else {
            this.formError.set(
              problem.detail ?? problem.title ?? 'Could not save this item.',
            );
          }
        },
      });
  }

  cancel(): void {
    if (!this.form.dirty) {
      void this.router.navigate(['/items']);
      return;
    }

    // Dirty-state guard (ui-spec.md, global shell): unsaved edits are never
    // dropped silently.
    const data: ConfirmDialogData = {
      tier: 2,
      title: 'Discard this item?',
      message: 'This item has not been saved. Leaving now discards what you have entered.',
      confirmLabel: 'Discard',
      cancelLabel: 'Keep editing',
      destructive: true,
      testId: 'item-add-discard',
    };

    this.dialog
      .open(ConfirmDialogComponent, { data, autoFocus: 'dialog', restoreFocus: true })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) void this.router.navigate(['/items']);
      });
  }

  /**
   * Pulls the asset-tag-specific reason out of a problem response: a 409 from the
   * uniqueness rule, or a field-keyed validation error.
   */
  private assetTagDetail(problem: ProblemDetails): string | null {
    const fieldErrors = problem.errors ?? {};
    const key = Object.keys(fieldErrors).find((k) => k.toLowerCase() === 'assettag');
    if (key && fieldErrors[key]?.length) return fieldErrors[key].join(' ');

    if (problem.status === 409) {
      return problem.detail ?? 'That asset tag is already in use.';
    }
    return null;
  }

  private focusAssetTag(): void {
    document.querySelector<HTMLInputElement>('[data-testid="item-add-assettag"]')?.focus();
  }

  /**
   * Confirm-before-navigate contract (ui-spec.md cross-screen states). True while
   * the form has been touched and the change has not been saved.
   */
  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saving();
  }
}
