import { Routes } from '@angular/router';

import { unsavedChangesGuard } from '../../core/guards/unsaved-changes.guard';

/**
 * Borrower management — REQ-2.1 (list/search), REQ-2.2 (add), REQ-2.3 (detail,
 * loan history, deactivate hard block). Routes come from design/ui-spec/ui-spec.md.
 *
 * Order matters: the literal 'new' segment must be declared before the ':id'
 * parameter route, or /borrowers/new resolves as a detail lookup for the id "new".
 */
export const BORROWER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./borrower-list.component').then((m) => m.BorrowerListComponent),
    title: 'Borrowers — LoanTracker',
  },
  {
    path: 'new',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./borrower-add.component').then((m) => m.BorrowerAddComponent),
    title: 'Add borrower — LoanTracker',
  },
  {
    path: ':id',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () =>
      import('./borrower-detail.component').then((m) => m.BorrowerDetailComponent),
    title: 'Borrower detail — LoanTracker',
  },
];
