import { Component, Input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * The visible explanation paired with a hard-blocked action.
 *
 * BR §6 / SME ruling (BR §8, resolved): retiring an item or deactivating a
 * borrower that holds an open loan is a HARD BLOCK, not a warning. The button is
 * disabled and this banner states the reason and the remedy — a `title`
 * attribute alone is not AA-sufficient, and a disabled control with no stated
 * reason is the failure mode the SME specifically argued against.
 *
 * Colour is never the sole signal: the amber background is paired with an icon
 * and explicit text. Amber is a filled background with dark ink, never amber
 * text on white (LoanTracker_UI_Standard.md §1 records that as a contrast fail).
 */
@Component({
  selector: 'app-guard-banner',
  standalone: true,
  imports: [MatIconModule],
  template: `
    <div class="lt-guard-banner" role="status" [attr.data-testid]="testId">
      <mat-icon aria-hidden="true">block</mat-icon>
      <span>{{ message }}</span>
    </div>
  `,
  styles: [
    `
      mat-icon {
        font-size: 18px;
        width: 18px;
        height: 18px;
        flex: none;
      }
    `,
  ],
})
export class GuardBannerComponent {
  @Input({ required: true }) message!: string;
  @Input() testId = 'guard-banner';
}
