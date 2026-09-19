import { Component, ElementRef, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { forkJoin } from 'rxjs';

import {
  Borrower,
  Loan,
  ProblemDetails,
  UpdateBorrowerRequest,
} from '../../core/models/api.models';
import { BorrowerService } from '../../core/services/borrower.service';
import { NotificationService } from '../../core/services/notification.service';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../shared/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { GuardBannerComponent } from '../../shared/guard-banner.component';
import { FocusOnCreateDirective } from '../../shared/focus-on-create.directive';

/**
 * Borrower detail — REQ-2.3, `scr-borrower-detail`, Pattern 12 (one-to-many list).
 *
 * The screen's load-bearing rule is the deactivate hard block (BR §8, SME-resolved):
 * while the borrower holds any open loan the Deactivate button is disabled AND a
 * guard banner names the specific items still held. A disabled control with no
 * stated reason is exactly the failure mode the SME argued against, so the banner
 * is not decoration — it is the requirement.
 *
 * Edit is inline on this same card: the modeling standard allows exactly one edit
 * surface per aggregate, so there is no separate /borrowers/:id/edit screen.
 */
@Component({
  selector: 'app-borrower-detail',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatTableModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    EmptyStateComponent,
    GuardBannerComponent,
    FocusOnCreateDirective,
  ],
  templateUrl: './borrower-detail.component.html',
  styles: [
    `
      .form-field {
        width: 100%;
      }

      .edit-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
        gap: 0 16px;
      }

      /* The detail grid uses a description list for real label/value semantics;
         the UA's default dd indent would otherwise break the grid alignment. */
      dl,
      dd {
        margin: 0;
      }
    `,
  ],
})
export class BorrowerDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly borrowers = inject(BorrowerService);
  private readonly notifications = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);

  readonly borrower = signal<Borrower | null>(null);
  readonly loans = signal<Loan[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);

  readonly deactivating = signal(false);
  readonly deactivateError = signal<string | null>(null);

  readonly displayedColumns = ['item', 'checkedOut', 'returned', 'status'];

  private borrowerId = '';

  readonly form: FormGroup = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    contactEmail: ['', [Validators.email, Validators.maxLength(256)]],
    contactPhone: ['', [Validators.maxLength(50)]],
    department: ['', [Validators.maxLength(200)]],
  });

  @ViewChild('editNameInput') private editNameInput?: ElementRef<HTMLInputElement>;

  /** Open loans drive the hard block — derived from loan state, never stored. */
  readonly openLoans = computed(() => this.loans().filter((l) => l.isOpen));

  readonly canDeactivate = computed(() => this.openLoans().length === 0);

  /**
   * Names the specific items still held, so Staff know what to chase rather than
   * only that the action is unavailable.
   */
  readonly guardMessage = computed(() => {
    const open = this.openLoans();
    if (open.length === 0) return '';
    const items = open.map((l) => `${l.itemName} (${l.itemAssetTag})`).join(', ');
    const noun = open.length === 1 ? 'open loan' : 'open loans';
    return `Deactivate is disabled — this borrower has ${open.length} ${noun} (${items}). Return it first.`;
  });

  get name(): AbstractControl {
    return this.form.controls['name'];
  }

  get contactEmail(): AbstractControl {
    return this.form.controls['contactEmail'];
  }

  get contactPhone(): AbstractControl {
    return this.form.controls['contactPhone'];
  }

  get department(): AbstractControl {
    return this.form.controls['department'];
  }

  ngOnInit(): void {
    this.borrowerId = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  load(): void {
    if (!this.borrowerId) {
      this.loadError.set('No borrower was requested.');
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.loadError.set(null);

    forkJoin({
      borrower: this.borrowers.get(this.borrowerId),
      loans: this.borrowers.loanHistory(this.borrowerId),
    }).subscribe({
      next: ({ borrower, loans }) => {
        this.borrower.set(borrower);
        this.loans.set(this.mostRecentFirst(loans));
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.loadError.set(problem.detail ?? problem.title ?? 'Could not load this borrower.');
        this.loading.set(false);
      },
    });
  }

  // --- Inline edit ---------------------------------------------------------

  startEdit(): void {
    const current = this.borrower();
    if (!current) return;

    this.form.reset({
      name: current.name,
      contactEmail: current.contactEmail ?? '',
      contactPhone: current.contactPhone ?? '',
      department: current.department ?? '',
    });
    this.saveError.set(null);
    this.editing.set(true);
    // Focus into the revealed form is handled by `ltFocusOnCreate` on the name input.
  }

  /**
   * True once an edit session has ended, so the Edit button can take focus back when the
   * read-only view returns without stealing it on first paint (Compliance N3).
   */
  readonly editSessionEnded = signal(false);

  cancelEdit(): void {
    this.editing.set(false);
    this.saveError.set(null);

    // Leaving edit mode destroys the Cancel button the user just pressed. Focus
    // returns to the control they came from, mirroring loan-detail's return panel —
    // which got both directions while this screen originally got only the entry.
    this.editSessionEnded.set(true);
  }

  saveEdit(): void {
    if (this.saving()) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      if (this.name.invalid) this.editNameInput?.nativeElement.focus();
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);

    const raw = this.form.getRawValue() as Record<string, string>;
    const request: UpdateBorrowerRequest = {
      name: (raw['name'] ?? '').trim(),
      contactEmail: (raw['contactEmail'] ?? '').trim() || null,
      contactPhone: (raw['contactPhone'] ?? '').trim() || null,
      department: (raw['department'] ?? '').trim() || null,
    };

    this.borrowers.update(this.borrowerId, request).subscribe({
      next: (updated) => {
        this.borrower.set(updated);
        this.saving.set(false);
        this.editing.set(false);
        this.editSessionEnded.set(true);
        this.notifications.success(`Borrower saved — ${updated.name}.`);
      },
      error: (problem: ProblemDetails) => {
        this.saving.set(false);
        this.saveError.set(this.messageFor(problem, 'Could not save this borrower.'));
      },
    });
  }

  // --- Deactivate (soft delete) -------------------------------------------

  deactivate(): void {
    const current = this.borrower();
    if (!current || !this.canDeactivate() || this.deactivating()) return;

    const data: ConfirmDialogData = {
      // Tier 2: reversible — deactivate is a soft delete (is_active = false),
      // never a hard delete, so it does not warrant the Tier 3 ceremony.
      tier: 2,
      title: 'Deactivate borrower?',
      message:
        `${current.name} will stop appearing in the checkout borrower list and will no longer ` +
        `be offered for new loans. Their loan history is kept, and they can be reactivated later.`,
      confirmLabel: 'Deactivate',
      destructive: true,
      testId: 'borrower-deactivate',
    };

    this.dialog
      .open(ConfirmDialogComponent, { data, autoFocus: 'dialog', restoreFocus: true })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed === true) this.runDeactivate();
      });
  }

  private runDeactivate(): void {
    this.deactivating.set(true);
    this.deactivateError.set(null);

    this.borrowers.deactivate(this.borrowerId).subscribe({
      next: (updated) => {
        this.deactivating.set(false);
        this.notifications.success(`Borrower deactivated — ${updated.name}.`);
        this.load();
      },
      error: (problem: ProblemDetails) => {
        this.deactivating.set(false);
        this.deactivateError.set(
          this.messageFor(problem, 'Could not deactivate this borrower.'),
        );
        // The server is the authority on the open-loan block; refresh so the
        // screen reflects whatever it just told us.
        this.load();
      },
    });
  }

  // --- Helpers -------------------------------------------------------------

  backToList(): void {
    void this.router.navigate(['/borrowers']);
  }

  /**
   * YYYY-MM-DD, taken from the ISO-8601 prefix the API emits, so the rendered
   * date cannot drift a day against the stored value via the viewer's timezone.
   */
  asDate(value: string | null): string {
    if (!value) return '—';
    const match = /^(\d{4}-\d{2}-\d{2})/.exec(value);
    return match ? match[1] : value;
  }

  private mostRecentFirst(loans: Loan[]): Loan[] {
    return [...loans].sort((a, b) => b.checkedOutAt.localeCompare(a.checkedOutAt));
  }

  private messageFor(problem: ProblemDetails, fallback: string): string {
    if (problem.errors) {
      const messages = Object.values(problem.errors).flat();
      if (messages.length > 0) return messages.join(' ');
    }
    return problem.detail ?? problem.title ?? fallback;
  }

  /**
   * Confirm-before-navigate contract (ui-spec.md cross-screen states). Only the
   * inline edit mode can hold unsaved work on a detail screen.
   */
  hasUnsavedChanges(): boolean {
    return this.editing() && this.form.dirty && !this.saving();
  }
}
