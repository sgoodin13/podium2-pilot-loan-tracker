import { Routes } from '@angular/router';

import { unsavedChangesGuard } from '../../core/guards/unsaved-changes.guard';

/**
 * Item management routes — REQ-1.1 (add), REQ-1.2 (list/search), REQ-1.3 (detail).
 *
 * Paths come from design/ui-spec/ui-spec.md: `/items`, `/items/new`, `/items/:id`.
 * `'new'` MUST precede `':id'` — otherwise the literal segment is swallowed by
 * the parameterised route and /items/new resolves as a detail lookup for the id
 * "new".
 */
export const ITEM_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./item-list.component').then((m) => m.ItemListComponent),
    title: 'Items — LoanTracker',
  },
  {
    path: 'new',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./item-add.component').then((m) => m.ItemAddComponent),
    title: 'Add item — LoanTracker',
  },
  {
    path: ':id',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./item-detail.component').then((m) => m.ItemDetailComponent),
    title: 'Item detail — LoanTracker',
  },
];
