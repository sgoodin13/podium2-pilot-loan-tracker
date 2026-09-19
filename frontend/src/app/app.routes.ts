import { Routes } from '@angular/router';

/**
 * Route table. Paths come from design/ui-spec/ui-spec.md — each screen's section
 * declares its route, and the shell's six nav links bind to these.
 *
 * Every feature is lazy-loaded so the initial bundle stays the shell only.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'items' },

  {
    path: 'items',
    loadChildren: () => import('./features/items/items.routes').then((m) => m.ITEM_ROUTES),
  },
  {
    path: 'borrowers',
    loadChildren: () =>
      import('./features/borrowers/borrowers.routes').then((m) => m.BORROWER_ROUTES),
  },
  {
    path: 'checkout',
    loadComponent: () =>
      import('./features/checkout/checkout-wizard.component').then(
        (m) => m.CheckoutWizardComponent,
      ),
    title: 'Checkout — LoanTracker',
  },
  {
    path: 'loans',
    loadChildren: () => import('./features/loans/loans.routes').then((m) => m.LOAN_ROUTES),
  },
  {
    path: 'reference',
    loadChildren: () =>
      import('./features/reference/reference.routes').then((m) => m.REFERENCE_ROUTES),
  },

  { path: '**', redirectTo: 'items' },
];
