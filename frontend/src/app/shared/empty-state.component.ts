import { Component, EventEmitter, Input, Output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

/**
 * Two visually and textually distinct empty states — never one generic message
 * (Standards Guide C6; LoanTracker_UI_Standard.md §3).
 *
 *   'no-data'    — nothing exists yet, with a call to action.
 *   'no-results' — filters matched nothing, with a way to clear them.
 */
@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [MatIconModule, MatButtonModule],
  template: `
    <div class="lt-empty" [attr.data-testid]="testId">
      <mat-icon aria-hidden="true">{{
        variant === 'no-data' ? 'inbox' : 'search_off'
      }}</mat-icon>
      <div class="lt-empty-title">{{ title }}</div>
      @if (hint) {
        <div>{{ hint }}</div>
      }
      @if (variant === 'no-results' && showClear) {
        <button
          mat-stroked-button
          class="clear"
          (click)="clearFilters.emit()"
          data-testid="empty-clear-filters"
        >
          Clear filters
        </button>
      }
    </div>
  `,
  styles: [
    `
      mat-icon {
        font-size: 32px;
        width: 32px;
        height: 32px;
        margin-bottom: 8px;
      }
      .clear {
        margin-top: 12px;
      }
    `,
  ],
})
export class EmptyStateComponent {
  @Input({ required: true }) variant!: 'no-data' | 'no-results';
  @Input({ required: true }) title!: string;
  @Input() hint?: string;
  @Input() showClear = true;
  @Input() testId = 'empty-state';

  @Output() readonly clearFilters = new EventEmitter<void>();
}
