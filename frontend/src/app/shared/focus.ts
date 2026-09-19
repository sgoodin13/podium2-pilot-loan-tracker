import { ElementRef } from '@angular/core';

/**
 * Moves focus onto an element that already exists but has just become focusable.
 *
 * @remarks
 * **Prefer `ltFocusOnCreate`.** If the target is *created* by the state change — which is
 * almost always the case — use that directive instead: it binds focus to the element's own
 * lifecycle and has no timing assumption to get wrong.
 *
 * This helper remains for the one case the directive cannot serve: a target that is never
 * destroyed and only re-enabled. The item-detail Edit button is disabled during edit rather
 * than removed, so no element is created when edit mode ends, and a disabled element cannot
 * take focus until change detection has processed the flag.
 *
 * Use this rather than `queueMicrotask`. Angular schedules change detection on the
 * microtask queue, so a focus call queued as a microtask races the render that creates
 * the target — and loses. The `@ViewChild` is then still `undefined`, or still points at
 * the element that was just detached, and optional chaining turns that miss into a
 * **silent no-op**: the code reads correctly, the markup is faultless, axe reports a clean
 * page, and focus has actually fallen back to `<body>`.
 *
 * A macrotask runs only after the microtask queue has drained, so the new element exists
 * by the time this callback fires.
 *
 * This was not a theoretical risk — every one of the four focus moves in this app was
 * written with `queueMicrotask` first, and all four failed their `toBeFocused()`
 * assertions. Those assertions are the only thing that would ever catch a regression
 * here, so each caller has one.
 */
export function focusWhenRendered(target: () => ElementRef<HTMLElement> | undefined): void {
  setTimeout(() => target()?.nativeElement.focus(), 0);
}
