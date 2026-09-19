import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

import { ProblemDetails } from '../models/api.models';

/**
 * Normalises every API failure into an RFC 7807 ProblemDetails object so callers
 * can render a specific reason inline at the point of action.
 *
 * Deliberately does NOT show a global banner or toast: error states are inline
 * at the point of the action taken (Standards Guide C6; LoanTracker_UI_Standard.md
 * §3 — "a failed checkout shows its reason on the checkout wizard itself, never a
 * generic top-of-page banner").
 */
export const problemDetailsInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const problem: ProblemDetails =
        error.error && typeof error.error === 'object' && 'title' in error.error
          ? (error.error as ProblemDetails)
          : {
              status: error.status,
              title: error.status === 0 ? 'Cannot reach the server' : 'Request failed',
              detail:
                error.status === 0
                  ? 'The API is not responding. Check that it is running.'
                  : error.message,
            };

      return throwError(() => problem);
    }),
  );
