import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

import { CheckoutWizardComponent } from './checkout-wizard.component';
import { problemDetailsInterceptor } from '../../core/interceptors/problem-details.interceptor';
import { Borrower, Item } from '../../core/models/api.models';

/**
 * Checkout wizard — step gating and the rejection render.
 *
 * The interceptor is wired in deliberately: the rejection panel renders
 * `problem.detail`, so testing the error path without the interceptor would test a
 * different object than the one the running app receives.
 */
describe('CheckoutWizardComponent', () => {
  let fixture: ComponentFixture<CheckoutWizardComponent>;
  let http: HttpTestingController;
  let element: HTMLElement;

  const borrower: Borrower = {
    id: 'b-1',
    name: 'Avery Test',
    contactEmail: null,
    contactPhone: null,
    department: 'Facilities',
    isActive: true,
    openLoanCount: 0,
  };

  const item: Item = {
    id: 'i-1',
    name: 'Cordless Drill',
    description: null,
    assetTag: 'QA-DRILL-1',
    itemCategoryId: 'c-1',
    itemCategoryName: 'Power Tools',
    isActive: true,
    isOnLoan: false,
    currentBorrowerName: null,
    currentLoanId: null,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CheckoutWizardComponent],
      providers: [
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CheckoutWizardComponent);
    element = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();

    http.expectOne((r) => r.url === '/api/borrowers/active').flush([borrower]);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const testId = <T extends HTMLElement>(id: string): T | null =>
    element.querySelector<T>(`[data-testid="${id}"]`);

  function selectTheBorrower(): void {
    const radio = element.querySelector<HTMLInputElement>(
      `[data-testid="checkout-borrower-${borrower.name}"] input[type="radio"]`,
    );
    expect(radio).withContext('the borrower radio must be in the markup').not.toBeNull();
    radio!.click();
    fixture.detectChanges();
  }

  function advanceToItemStep(): void {
    selectTheBorrower();
    testId<HTMLButtonElement>('checkout-next-to-item')!.click();
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/items/available').flush([item]);
    fixture.detectChanges();
  }

  // --- Step gating -------------------------------------------------------

  it('starts on step 1 with the "Next: Item" button disabled until a borrower is picked', () => {
    expect(testId('checkout-step-1')).not.toBeNull();
    expect(testId<HTMLButtonElement>('checkout-next-to-item')!.disabled).toBeTrue();

    selectTheBorrower();

    expect(testId<HTMLButtonElement>('checkout-next-to-item')!.disabled).toBeFalse();
  });

  it('does not advance past step 1 while no borrower is selected', () => {
    testId<HTMLButtonElement>('checkout-next-to-item')!.click();
    fixture.detectChanges();

    expect(testId('checkout-step-1'))
      .withContext('a disabled Next must not move the wizard on')
      .not.toBeNull();
    expect(testId('checkout-step-2')).toBeNull();
    http.expectNone((r) => r.url === '/api/items/available');
  });

  it('gates step 2 on an item selection the same way', () => {
    advanceToItemStep();

    expect(testId('checkout-step-2')).not.toBeNull();
    expect(testId<HTMLButtonElement>('checkout-next-to-confirm')!.disabled).toBeTrue();

    testId<HTMLButtonElement>(`checkout-step2-select-${item.assetTag}`)!.click();
    fixture.detectChanges();

    expect(testId<HTMLButtonElement>('checkout-next-to-confirm')!.disabled).toBeFalse();
  });

  it('names the specific item and asset tag on the confirm step', () => {
    advanceToItemStep();
    testId<HTMLButtonElement>(`checkout-step2-select-${item.assetTag}`)!.click();
    fixture.detectChanges();
    testId<HTMLButtonElement>('checkout-next-to-confirm')!.click();
    fixture.detectChanges();

    const summary = testId('checkout-confirm-summary')!;
    expect(summary.textContent).toContain(item.name);
    expect(summary.textContent)
      .withContext('"a drill" is not accountable — the asset tag must be on screen')
      .toContain(item.assetTag);
    expect(summary.textContent).toContain(borrower.name);
  });

  // --- Rejection rendering (t4) ------------------------------------------

  it('renders the API rejection inline on the wizard, naming the item, and offers recovery', fakeAsync(() => {
    advanceToItemStep();
    testId<HTMLButtonElement>(`checkout-step2-select-${item.assetTag}`)!.click();
    fixture.detectChanges();
    testId<HTMLButtonElement>('checkout-next-to-confirm')!.click();
    fixture.detectChanges();

    testId<HTMLButtonElement>('checkout-confirm')!.click();
    fixture.detectChanges();

    const detail =
      'Cordless Drill (QA-DRILL-1) was just checked out to another borrower. Nothing was saved. Pick a different item to continue.';

    http.expectOne((r) => r.url === '/api/checkout').flush(
      { title: 'Conflict', status: 409, detail, instance: '/api/checkout', traceId: 'trace-1' },
      { status: 409, statusText: 'Conflict' },
    );
    tick();
    fixture.detectChanges();

    const panel = testId('checkout-rejection-panel');
    expect(panel).withContext('the rejection must render inline, not as a toast').not.toBeNull();
    expect(panel!.getAttribute('role')).toBe('alert');
    expect(panel!.textContent).toContain(item.name);
    expect(panel!.textContent).toContain(item.assetTag);
    expect(panel!.textContent).toContain('Nothing was saved');

    // The Confirm button is replaced by the recovery action.
    expect(testId('checkout-confirm')).toBeNull();
    expect(testId('checkout-back-to-item-selection')).not.toBeNull();
  }));

  it('returns to step 2 and refetches availability from the rejection panel', fakeAsync(() => {
    advanceToItemStep();
    testId<HTMLButtonElement>(`checkout-step2-select-${item.assetTag}`)!.click();
    fixture.detectChanges();
    testId<HTMLButtonElement>('checkout-next-to-confirm')!.click();
    fixture.detectChanges();
    testId<HTMLButtonElement>('checkout-confirm')!.click();
    fixture.detectChanges();

    http.expectOne((r) => r.url === '/api/checkout').flush(
      { title: 'Conflict', status: 409, detail: 'Already out.' },
      { status: 409, statusText: 'Conflict' },
    );
    tick();
    fixture.detectChanges();

    testId<HTMLButtonElement>('checkout-back-to-item-selection')!.click();
    fixture.detectChanges();

    // The item that was taken must not still be offered, so the list is refetched.
    http.expectOne((r) => r.url === '/api/items/available').flush([]);
    tick();
    fixture.detectChanges();

    expect(testId('checkout-step-2')).not.toBeNull();
    expect(testId('checkout-rejection-panel')).toBeNull();
    expect(testId('checkout-item-empty')).not.toBeNull();
  }));

  it('renders a loading-only state rather than an empty list while borrowers are in flight', () => {
    // Re-search re-issues the request; the empty state must not flash in the gap.
    const search = testId<HTMLInputElement>('checkout-borrower-search')!;
    search.value = 'nobody';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(testId('checkout-borrower-empty')).toBeNull();

    http.expectOne((r) => r.url === '/api/borrowers/active').flush([]);
    fixture.detectChanges();

    expect(testId('checkout-borrower-empty')).not.toBeNull();
  });
});
