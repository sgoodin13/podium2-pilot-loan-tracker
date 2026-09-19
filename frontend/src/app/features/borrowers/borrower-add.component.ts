import { Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { Borrower, CreateBorrowerRequest, ProblemDetails } from '../../core/models/api.models';
import { BorrowerService } from '../../core/services/borrower.service';
import { NotificationService } from '../../core/services/notification.service';

/**
 * Add borrower — REQ-2.2, `scr-borrower-add`, Pattern 04 (single record form).
 *
 * Only Name is required; the three contact/grouping fields are optional per the
 * BRs and the mockup's own a11y note. Validation renders inline under each
 * field, and an API rejection renders inline in the card — never as a
 * top-of-page banner (ui-spec.md "Error/dialog handling").
 */
@Component({
  selector: 'app-borrower-add',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './borrower-add.component.html',
  styles: [
    `
      .form-field {
        width: 100%;
      }
    `,
  ],
})
export class BorrowerAddComponent {
  private readonly fb = inject(FormBuilder);
  private readonly borrowers = inject(BorrowerService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);

  readonly form: FormGroup = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    contactEmail: ['', [Validators.email, Validators.maxLength(256)]],
    contactPhone: ['', [Validators.maxLength(50)]],
    department: ['', [Validators.maxLength(200)]],
  });

  @ViewChild('nameInput') private nameInput?: ElementRef<HTMLInputElement>;
  @ViewChild('emailInput') private emailInput?: ElementRef<HTMLInputElement>;

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

  save(): void {
    if (this.saving()) return;

    if (this.form.invalid) {
      // Surface every message at once rather than one field at a time, and put
      // focus where the first problem is so a keyboard user is not stranded.
      this.form.markAllAsTouched();
      this.focusFirstInvalid();
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);

    const raw = this.form.getRawValue() as Record<string, string>;
    const request: CreateBorrowerRequest = {
      name: (raw['name'] ?? '').trim(),
      contactEmail: (raw['contactEmail'] ?? '').trim() || null,
      contactPhone: (raw['contactPhone'] ?? '').trim() || null,
      department: (raw['department'] ?? '').trim() || null,
    };

    this.borrowers.create(request).subscribe({
      next: (created: Borrower) => {
        this.saving.set(false);
        this.notifications.success(`Borrower saved — ${created.name}.`);
        void this.router.navigate(['/borrowers', created.id]);
      },
      error: (problem: ProblemDetails) => {
        this.saving.set(false);
        this.saveError.set(this.messageFor(problem));
      },
    });
  }

  cancel(): void {
    void this.router.navigate(['/borrowers']);
  }

  /** Flattens RFC 7807 validation errors into one specific, readable reason. */
  private messageFor(problem: ProblemDetails): string {
    if (problem.errors) {
      const messages = Object.values(problem.errors).flat();
      if (messages.length > 0) return messages.join(' ');
    }
    return problem.detail ?? problem.title ?? 'Could not save this borrower.';
  }

  private focusFirstInvalid(): void {
    if (this.name.invalid) {
      this.nameInput?.nativeElement.focus();
    } else if (this.contactEmail.invalid) {
      this.emailInput?.nativeElement.focus();
    }
  }

  /**
   * Confirm-before-navigate contract (ui-spec.md cross-screen states). True while
   * the form has been touched and the change has not been saved.
   */
  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saving();
  }
}
