import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';

import { ItemCategory, ProblemDetails } from '../../core/models/api.models';
import { NotificationService } from '../../core/services/notification.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';
import { EmptyStateComponent } from '../../shared/empty-state.component';

interface EditableCategory extends ItemCategory {
  /** True while this row is in inline-edit mode. */
  editing: boolean;
  /** True for a row added client-side that has never been saved. */
  isNew: boolean;
  draft: { name: string; description: string; isActive: boolean };
  error?: string;
  saving?: boolean;
}

/**
 * Item Category maintenance — REQ-5.1, `ref-item-category`, Pattern 18
 * (simple lookup table with inline edit).
 *
 * Rows are deactivated, never deleted: a category referenced by an active item
 * cannot be removed, only switched inactive (BR §5.5).
 */
@Component({
  selector: 'app-item-category',
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
  templateUrl: './item-category.component.html',
  styleUrl: './reference-table.scss',
})
export class ItemCategoryComponent implements OnInit {
  private readonly referenceData = inject(ReferenceDataService);
  private readonly notifications = inject(NotificationService);

  readonly rows = signal<EditableCategory[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  readonly displayedColumns = ['name', 'description', 'active', 'actions'];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.referenceData.listCategories(true).subscribe({
      next: (categories) => {
        this.rows.set(categories.map((c) => this.toEditable(c)));
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.loadError.set(problem.detail ?? problem.title ?? 'Could not load categories.');
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
        isActive: true,
        editing: true,
        isNew: true,
        draft: { name: '', description: '', isActive: true },
      },
    ]);
  }

  edit(row: EditableCategory): void {
    row.draft = {
      name: row.name,
      description: row.description ?? '',
      isActive: row.isActive,
    };
    row.editing = true;
    row.error = undefined;
    this.rows.update((rows) => [...rows]);
  }

  cancel(row: EditableCategory): void {
    if (row.isNew) {
      this.rows.update((rows) => rows.filter((r) => r !== row));
      return;
    }
    row.editing = false;
    row.error = undefined;
    this.rows.update((rows) => [...rows]);
  }

  save(row: EditableCategory): void {
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
      isActive: row.draft.isActive,
    };

    const done = (saved: ItemCategory) => {
      Object.assign(row, this.toEditable(saved));
      row.saving = false;
      this.rows.update((rows) => [...rows]);
      this.notifications.success(`Category saved — ${saved.name}.`);
    };

    const fail = (problem: ProblemDetails) => {
      row.saving = false;
      row.error = problem.detail ?? problem.title ?? 'Could not save this category.';
      this.rows.update((rows) => [...rows]);
    };

    if (row.isNew) {
      this.referenceData.createCategory(request).subscribe({ next: done, error: fail });
    } else {
      this.referenceData.updateCategory(row.id, request).subscribe({ next: done, error: fail });
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

  private toEditable(category: ItemCategory): EditableCategory {
    return {
      ...category,
      editing: false,
      isNew: false,
      draft: {
        name: category.name,
        description: category.description ?? '',
        isActive: category.isActive,
      },
    };
  }
}
