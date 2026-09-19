import { Component, Input } from '@angular/core';

/**
 * Skeleton rows for the three list screens (Items, Borrowers, Loans) —
 * Standards Guide C6 and LoanTracker_UI_Standard.md §3 both specify skeletons on
 * data-dense views and a spinner elsewhere.
 *
 * aria-hidden: the loading state is announced by the live region on the host
 * screen, so the placeholder bars themselves are decorative.
 */
@Component({
  selector: 'app-skeleton-rows',
  standalone: true,
  template: `
    <div aria-hidden="true" data-testid="skeleton-rows">
      @for (row of rowArray; track $index) {
        <div class="lt-skeleton-row">
          @for (col of colArray; track $index) {
            <div class="lt-skeleton-bar" [style.max-width.%]="col"></div>
          }
        </div>
      }
    </div>
  `,
})
export class SkeletonRowsComponent {
  @Input() rows = 5;
  @Input() columns = 4;

  get rowArray(): number[] {
    return Array.from({ length: this.rows }, (_, i) => i);
  }

  /** Varying widths so the placeholder reads as tabular rather than a block. */
  get colArray(): number[] {
    const widths = [70, 90, 55, 80, 60, 75];
    return Array.from({ length: this.columns }, (_, i) => widths[i % widths.length]);
  }
}
