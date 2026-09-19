import { Routes } from '@angular/router';

export const LOAN_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./loan-list.component').then((m) => m.LoanListComponent),
    title: 'Loans — LoanTracker',
  },
  {
    path: ':id',
    loadComponent: () => import('./loan-detail.component').then((m) => m.LoanDetailComponent),
    title: 'Loan detail — LoanTracker',
  },
];
