import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

/**
 * Success confirmations. A successful save or checkout shows a MatSnackBar and
 * the list refetches — no stale-row flash (LoanTracker_UI_Standard.md §3).
 *
 * Errors deliberately do NOT come through here: they render inline at the point
 * of the action taken (Standards Guide C6). The checkout rejection panel is the
 * canonical example.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);

  success(message: string, testId = 'success-snackbar'): void {
    this.snackBar.open(message, 'Dismiss', {
      duration: 6000,
      horizontalPosition: 'center',
      verticalPosition: 'bottom',
      panelClass: ['lt-snackbar', testId],
    });
  }
}
