import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/**
 * The application shell.
 *
 * Structure and the six nav items (order, labels and routes) come from
 * design/ui-spec/ — every mockup renders this same sidenav. Left fixed sidenav,
 * no top toolbar: there is no user menu, logout, global search or notification
 * surface in scope.
 *
 * The skip link is the first focusable element on every screen — a WCAG 2.2 AA
 * commitment recorded in design/ui-spec/patterns-applied.md.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <a class="lt-skip-link" href="#main">Skip to main content</a>

    <div class="lt-shell">
      <nav class="lt-sidenav" aria-label="Primary">
        <div class="brand">LoanTracker</div>
        @for (link of navLinks; track link.route) {
          <a
            [routerLink]="link.route"
            routerLinkActive="active"
            [routerLinkActiveOptions]="{ exact: false }"
            [attr.data-testid]="link.testId"
            >{{ link.label }}</a
          >
        }
      </nav>

      <main id="main" class="lt-main" tabindex="-1">
        <router-outlet />
      </main>
    </div>
  `,
})
export class AppComponent {
  /**
   * Detail and add routes highlight their parent list item — /items/new and
   * /items/:id both mark "Items" active — so prefix matching is intentional
   * (routerLinkActiveOptions.exact = false).
   */
  readonly navLinks = [
    { label: 'Items', route: '/items', testId: 'nav-items' },
    { label: 'Borrowers', route: '/borrowers', testId: 'nav-borrowers' },
    { label: 'Checkout', route: '/checkout', testId: 'nav-checkout' },
    { label: 'Loans', route: '/loans', testId: 'nav-loans' },
    { label: 'Item Categories', route: '/reference/item-categories', testId: 'nav-item-categories' },
    { label: 'Loan Statuses', route: '/reference/loan-statuses', testId: 'nav-loan-statuses' },
  ];
}
