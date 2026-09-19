import { ElementRef } from '@angular/core';

/**
 * Moves focus onto an element that a state change has just created.
 *
 * @remarks
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
