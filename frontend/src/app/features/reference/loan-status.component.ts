import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';

import { LoanStatus, ProblemDetails } from '../../core/models/api.models';
import { NotificationService } from '../../core/services/notification.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';
import { EmptyStateComponent } from '../../shared/empty-state.component';

interface EditableStatus extends LoanStatus {
  editing: boolean;
  isNew: boolean;
  draft: { name: string; description: string; isTerminal: boolean; isActive: boolean };
  error?: string;
  saving?: boolean;
}

/**
 * Loan Status maintenance — REQ-5.2, `ref-loan-status`, Pattern 18 extended with
 * the Is Terminal flag.
 *
 * Is Terminal is the whole point of this screen being maintained data rather than
 * an enum: Staff can add a further terminal status (BR §4's worked example is
 * "Returned – Needs Repair") without a code change, and it immediately becomes
 * selectable in the loan-detail return panel.
 */
@Component({
  selector: 'app-loan-status',
  standalone: true,
  imports: [
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatProgressSpinnerModule,
    EmptyStateComponent,
  ],
  templateUrl: './loan-status.component.html',
  styleUrl: './reference-table.scss',
})
export class LoanStatusComponent implements OnInit {
  private readonly referenceData = inject(ReferenceDataService);
  private readonly notifications = inject(NotificationService);

  readonly rows = signal<EditableStatus[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  readonly displayedColumns = ['name', 'description', 'terminal', 'active', 'actions'];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.referenceData.listStatuses(true).subscribe({
      next: (statuses) => {
        this.rows.set(statuses.map((s) => this.toEditable(s)));
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.loadError.set(problem.detail ?? problem.title ?? 'Could not load loan statuses.');
        this.loading.set(false);
      },
    });
  }

  addRow(): void {
    this.rows.update((rows) => [
      ...rows,
      {
        id: '',
        name: '',
        description: null,
        isTerminal: false,
        isActive: true,
        editing: true,
        isNew: true,
        draft: { name: '', description: '', isTerminal: false, isActive: true },
      },
    ]);
  }

  edit(row: EditableStatus): void {
    row.draft = {
      name: row.name,
      description: row.description ?? '',
      isTerminal: row.isTerminal,
      isActive: row.isActive,
    };
    row.editing = true;
    row.error = undefined;
    this.rows.update((rows) => [...rows]);
  }

  cancel(row: EditableStatus): void {
    if (row.isNew) {
      this.rows.update((rows) => rows.filter((r) => r !== row));
      return;
    }
    row.editing = false;
    row.error = undefined;
    this.rows.update((rows) => [...rows]);
  }

  save(row: EditableStatus): void {
    const name = row.draft.name.trim();
    if (!name) {
      row.error = 'Name is required.';
      this.rows.update((rows) => [...rows]);
      return;
    }

    row.saving = true;
    row.error = undefined;
    this.rows.update((rows) => [...rows]);

    const request = {
      name,
      description: row.draft.description.trim() || null,
      isTerminal: row.draft.isTerminal,
      isActive: row.draft.isActive,
    };

    const done = (saved: LoanStatus) => {
      Object.assign(row, this.toEditable(saved));
      row.saving = false;
      this.rows.update((rows) => [...rows]);
      this.notifications.success(`Loan status saved — ${saved.name}.`);
    };

    const fail = (problem: ProblemDetails) => {
      row.saving = false;
      row.error = problem.detail ?? problem.title ?? 'Could not save this loan status.';
      this.rows.update((rows) => [...rows]);
    };

    if (row.isNew) {
      this.referenceData.createStatus(request).subscribe({ next: done, error: fail });
    } else {
      this.referenceData.updateStatus(row.id, request).subscribe({ next: done, error: fail });
    }
  }

  saveAll(): void {
    this.rows()
      .filter((r) => r.editing && !r.saving)
      .forEach((r) => this.save(r));
  }

  get hasEditingRows(): boolean {
    return this.rows().some((r) => r.editing);
  }

  private toEditable(status: LoanStatus): EditableStatus {
    return {
      ...status,
      editing: false,
      isNew: false,
      draft: {
        name: status.name,
        description: status.description ?? '',
        isTerminal: status.isTerminal,
        isActive: status.isActive,
      },
    };
  }
}
