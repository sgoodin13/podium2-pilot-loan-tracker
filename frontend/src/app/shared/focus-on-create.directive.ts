import {
  AfterViewInit,
  Directive,
  ElementRef,
  Input,
  booleanAttribute,
  inject,
} from '@angular/core';

/**
 * Moves focus to this element as soon as it is created.
 *
 * @remarks
 * Use this for content that appears in response to an action and replaces the control
 * the user was on — the checkout rejection panel being the case this was written for.
 *
 * It exists because the `@ViewChild` + deferred-callback approach is not reliable for
 * content inside nested `@if` blocks: the query did not resolve in time, the optional
 * chaining turned the miss into a silent no-op, and focus stayed on `<body>` while the
 * code read as though it worked. Binding the focus to the element's own lifecycle
 * removes the timing question entirely — `ngAfterViewInit` runs when *this* element
 * exists, not when a query happens to be refreshed.
 *
 * Only focus content that appears as the RESULT of a user action. An element that is
 * also present at initial render would otherwise steal focus on page load, so bind the
 * input to whatever tells you a real transition has happened:
 *
 * ```html
 * <div ltFocusOnCreate>…</div>                      <!-- always, e.g. an error panel -->
 * <div [ltFocusOnCreate]="hasNavigated()">…</div>   <!-- not on first paint -->
 * ```
 */
@Directive({
  selector: '[ltFocusOnCreate]',
  standalone: true,
})
export class FocusOnCreateDirective implements AfterViewInit {
  private readonly host = inject(ElementRef<HTMLElement>);

  /** Bare attribute means "always focus"; bind `false` to skip the initial render. */
  @Input({ alias: 'ltFocusOnCreate', transform: booleanAttribute }) enabled = true;

  ngAfterViewInit(): void {
    if (this.enabled) {
      this.host.nativeElement.focus();
    }
  }
}
