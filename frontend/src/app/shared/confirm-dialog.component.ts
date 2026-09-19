import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

/**
 * Severity tiers per Podium2_Standards_Guide.md C3.
 *
 * Tier 1 — reversible, low-stakes: no confirmation at all (so this dialog is
 *          simply not opened; the tier exists in the type for completeness).
 * Tier 2 — reversible but consequential: names the specific downstream effect.
 * Tier 3 — destructive or hard to reverse: blocking, enumerates the actual
 *          affected records by name and requires explicit acknowledgement.
 */
export type ConfirmTier = 1 | 2 | 3;

export interface ConfirmDialogData {
  tier: ConfirmTier;
  title: string;
  /** The specific downstream effect, in plain language. Never "Are you sure?". */
  message: string;
  /** Tier 3 only — the actual affected records, by name. */
  affectedRecords?: string[];
  confirmLabel: string;
  cancelLabel?: string;
  /** Styles the confirm button as destructive. */
  destructive?: boolean;
  testId?: string;
}

/**
 * The single reusable confirm-action component — never redesigned per screen
 * (Standards Guide C3).
 *
 * Accessibility: MatDialog traps focus without trapping the keyboard, Escape
 * closes, and focus returns to the trigger on close. The Tier 3 acknowledgement
 * checkbox is a real control with a programmatic label.
 */
@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <h2 mat-dialog-title id="confirm-dialog-title">{{ data.title }}</h2>

    <mat-dialog-content>
      <p [attr.data-testid]="(data.testId ?? 'confirm') + '-message'">{{ data.message }}</p>

      @if (data.tier === 3 && data.affectedRecords?.length) {
        <p class="affected-label" id="affected-label">This affects:</p>
        <ul class="affected" aria-labelledby="affected-label">
          @for (record of data.affectedRecords; track record) {
            <li>{{ record }}</li>
          }
        </ul>

        <label class="ack">
          <input
            type="checkbox"
            [checked]="acknowledged"
            (change)="acknowledged = !acknowledged"
            data-testid="confirm-acknowledge"
          />
          <span>I understand this cannot be undone.</span>
        </label>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-stroked-button mat-dialog-close data-testid="confirm-cancel">
        {{ data.cancelLabel ?? 'Cancel' }}
      </button>
      <button
        mat-flat-button
        [color]="data.destructive ? 'warn' : 'primary'"
        [disabled]="data.tier === 3 && !acknowledged"
        [mat-dialog-close]="true"
        data-testid="confirm-accept"
      >
        {{ data.confirmLabel }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      :host {
        display: block;
        max-width: 480px;
      }
      p {
        font-size: 13.5px;
        margin: 0 0 12px;
      }
      .affected-label {
        font-weight: 600;
        margin-bottom: 4px;
      }
      ul.affected {
        margin: 0 0 12px;
        padding-left: 20px;
        font-size: 13px;
      }
      .ack {
        display: flex;
        align-items: flex-start;
        gap: 8px;
        font-size: 13px;
      }
    `,
  ],
})
export class ConfirmDialogComponent {
  readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
  readonly dialogRef = inject(MatDialogRef<ConfirmDialogComponent>);

  acknowledged = false;
}
