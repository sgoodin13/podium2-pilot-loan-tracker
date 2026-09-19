import { Routes } from '@angular/router';

export const REFERENCE_ROUTES: Routes = [
  {
    path: 'item-categories',
    loadComponent: () =>
      import('./item-category.component').then((m) => m.ItemCategoryComponent),
    title: 'Item Categories — LoanTracker',
  },
  {
    path: 'loan-statuses',
    loadComponent: () =>
      import('./loan-status.component').then((m) => m.LoanStatusComponent),
    title: 'Loan Statuses — LoanTracker',
  },
  { path: '', pathMatch: 'full', redirectTo: 'item-categories' },
];
