import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';

import { Loan, LoanStatus, ProblemDetails } from '../../core/models/api.models';
import { LoanService } from '../../core/services/loan.service';
import { NotificationService } from '../../core/services/notification.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';
import { FocusOnCreateDirective } from '../../shared/focus-on-create.directive';

/**
 * Loan detail — `scr-loan-detail`, Pattern 15 (approval/status workflow adapted
 * to a two-node lifecycle), REQ-4.2.
 *
 * The Return action is the one state transition, and it requires a TERMINAL
 * status: a non-terminal status cannot close a loan (BR §6). The dropdown is
 * populated from the terminal set only, so the invalid choice is not offered —
 * and the API rejects it independently if it somehow arrives.
 */
@Component({
  selector: 'app-loan-detail',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    FocusOnCreateDirective,
  ],
  templateUrl: './loan-detail.component.html',
  styleUrl: './loan-detail.component.scss',
})
export class LoanDetailComponent implements OnInit {
  private readonly loanService = inject(LoanService);
  private readonly referenceData = inject(ReferenceDataService);
  private readonly notifications = inject(NotificationService);
  private readonly route = inject(ActivatedRoute);

  readonly loan = signal<Loan | null>(null);
  readonly terminalStatuses = signal<LoanStatus[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  /** True once "Return this item" is pressed — reveals the return panel. */
  readonly returnPanelOpen = signal(false);
  readonly selectedStatusId = signal<string>('');
  readonly submitting = signal(false);
  readonly returnError = signal<string | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.loadError.set('No loan id was supplied.');
      this.loading.set(false);
      return;
    }
    this.load(id);
  }

  load(id: string): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.loanService.get(id).subscribe({
      next: (loan) => {
        this.loan.set(loan);
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.loadError.set(problem.detail ?? problem.title ?? 'Could not load this loan.');
        this.loading.set(false);
      },
    });

    this.referenceData.listTerminalStatuses().subscribe({
      next: (statuses) => this.terminalStatuses.set(statuses),
      error: () => this.terminalStatuses.set([]),
    });
  }

  /**
   * True once the return panel has been opened at least once.
   *
   * The trigger button is present on first paint for any open loan, so focusing it
   * unconditionally would steal focus on page load. It should only take focus back when
   * it reappears after the panel closes (Compliance finding F6).
   */
  readonly returnPanelWasOpen = signal(false);

  /**
   * Opening and cancelling each destroy the control that currently has focus, so each
   * moves it. Both moves are bound to the target element's own lifecycle via
   * `ltFocusOnCreate` rather than driven from here — see that directive for why driving
   * focus from the component silently failed three times.
   */
  openReturnPanel(): void {
    this.returnPanelOpen.set(true);
    this.returnError.set(null);
    this.returnPanelWasOpen.set(true);
  }

  cancelReturn(): void {
    this.returnPanelOpen.set(false);
    this.selectedStatusId.set('');
    this.returnError.set(null);
  }

  confirmReturn(): void {
    const loan = this.loan();
    const statusId = this.selectedStatusId();

    if (!loan || !statusId || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.returnError.set(null);

    this.loanService.return(loan.id, { loanStatusId: statusId }).subscribe({
      next: (updated) => {
        this.submitting.set(false);
        this.returnPanelOpen.set(false);
        this.selectedStatusId.set('');
        this.loan.set(updated);

        // Names the specific item instance, consistent with the checkout
        // confirmation — never just the category.
        this.notifications.success(
          `Returned — ${updated.itemName} (${updated.itemAssetTag}), status ${updated.loanStatusName}.`,
          'return-success-snackbar',
        );
      },
      error: (problem: ProblemDetails) => {
        this.submitting.set(false);
        this.returnError.set(problem.detail ?? 'Could not return this loan.');
      },
    });
  }
}
