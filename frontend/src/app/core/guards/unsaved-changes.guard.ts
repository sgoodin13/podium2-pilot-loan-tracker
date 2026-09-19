import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';

import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../shared/confirm-dialog.component';

/**
 * A screen that can report unsaved work.
 *
 * Components opt in by exposing `hasUnsavedChanges()`. The guard treats anything
 * without it as always safe to leave, so adding a new screen never silently
 * acquires a prompt it did not ask for.
 */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

function canReportChanges(component: unknown): component is HasUnsavedChanges {
  return (
    typeof component === 'object' &&
    component !== null &&
    typeof (component as HasUnsavedChanges).hasUnsavedChanges === 'function'
  );
}

/**
 * Confirm-before-navigate on a touched, unsaved form.
 *
 * Required by `design/ui-spec/ui-spec.md` cross-screen states: "any unsaved form
 * (Add screens, inline-edit rows in reference data) prompts before navigation
 * away if fields have been touched."
 *
 * The in-screen Cancel buttons prompt on their own; this covers the other exits —
 * a nav-link click or a router back. It cannot cover a browser tab close, which
 * is the browser's own `beforeunload` and deliberately out of scope here.
 */
export const unsavedChangesGuard: CanDeactivateFn<unknown> = (
  component,
): Observable<boolean> | boolean => {
  if (!canReportChanges(component) || !component.hasUnsavedChanges()) {
    return true;
  }

  const dialog = inject(MatDialog);

  const data: ConfirmDialogData = {
    tier: 2,
    title: 'Discard unsaved changes?',
    message:
      'This form has changes that have not been saved. Leaving now discards them.',
    confirmLabel: 'Discard changes',
    cancelLabel: 'Keep editing',
    destructive: true,
    testId: 'unsaved-changes',
  };

  return dialog
    .open(ConfirmDialogComponent, { data, autoFocus: 'dialog' })
    .afterClosed()
    .pipe(map((confirmed) => confirmed === true));
};
