import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { LoanDetailComponent } from './loan-detail.component';
import { problemDetailsInterceptor } from '../../core/interceptors/problem-details.interceptor';
import { Loan, LoanStatus } from '../../core/models/api.models';

/**
 * Loan detail — the return panel.
 *
 * BR §6: a loan can only be closed by a TERMINAL status. The dropdown offers the
 * terminal set only, and "Confirm return" stays disabled until one is chosen, so the
 * invalid transition cannot even be submitted.
 */
describe('LoanDetailComponent', () => {
  let fixture: ComponentFixture<LoanDetailComponent>;
  let http: HttpTestingController;
  let element: HTMLElement;

  const openLoan: Loan = {
    id: 'loan-1',
    itemId: 'i-1',
    itemName: 'Cordless Drill',
    itemAssetTag: 'PT-1001',
    borrowerId: 'b-1',
    borrowerName: 'Avery Test',
    loanStatusId: 's-open',
    loanStatusName: 'Checked Out',
    loanStatusIsTerminal: false,
    checkedOutAt: '2026-09-19T09:00:00Z',
    returnedAt: null,
    isOpen: true,
  };

  const terminalStatuses: LoanStatus[] = [
    { id: 's-returned', name: 'Returned', description: null, isTerminal: true, isActive: true },
    { id: 's-lost', name: 'Lost', description: null, isTerminal: true, isActive: true },
    { id: 's-damaged', name: 'Damaged', description: null, isTerminal: true, isActive: true },
  ];

  async function render(loan: Loan): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [LoanDetailComponent],
      providers: [
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: loan.id }) } },
        },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(LoanDetailComponent);
    element = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();

    http.expectOne(`/api/loans/${loan.id}`).flush(loan);
    http.expectOne((r) => r.url === '/api/loan-statuses').flush(terminalStatuses);
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  const testId = <T extends HTMLElement>(id: string): T | null =>
    element.querySelector<T>(`[data-testid="${id}"]`);

  it('offers the Return action while the loan is open and names the item on screen', async () => {
    await render(openLoan);

    expect(testId('loan-detail-item')!.textContent).toContain('Cordless Drill');
    expect(testId('loan-detail-item')!.textContent).toContain('PT-1001');
    expect(testId('loan-detail-status')!.textContent).toContain('Checked Out');
    expect(testId('loan-return-open')).not.toBeNull();
    expect(testId('loan-return-panel')).toBeNull();
  });

  it('keeps "Confirm return" disabled until a terminal status is chosen', async () => {
    await render(openLoan);

    testId<HTMLButtonElement>('loan-return-open')!.click();
    fixture.detectChanges();

    expect(testId('loan-return-panel')).not.toBeNull();

    const confirm = testId<HTMLButtonElement>('loan-return-confirm')!;
    expect(confirm.disabled)
      .withContext('a loan must not be closable without a resulting status')
      .toBeTrue();

    // What the mat-select's ngModelChange does when an option is picked.
    fixture.componentInstance.selectedStatusId.set('s-returned');
    fixture.detectChanges();

    expect(testId<HTMLButtonElement>('loan-return-confirm')!.disabled).toBeFalse();
  });

  it('only offers terminal statuses — the API is asked for terminalOnly', async () => {
    await TestBed.configureTestingModule({
      imports: [LoanDetailComponent],
      providers: [
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'loan-1' }) } },
        },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(LoanDetailComponent);
    element = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();

    http.expectOne('/api/loans/loan-1').flush(openLoan);

    const statusRequest = http.expectOne((r) => r.url === '/api/loan-statuses');
    expect(statusRequest.request.params.get('terminalOnly')).toBe('true');
    statusRequest.flush(terminalStatuses);
    fixture.detectChanges();

    expect(fixture.componentInstance.terminalStatuses().every((s) => s.isTerminal)).toBeTrue();
  });

  it('renders the returned state on screen after a successful return', fakeAsync(async () => {
    await render(openLoan);

    testId<HTMLButtonElement>('loan-return-open')!.click();
    fixture.detectChanges();
    fixture.componentInstance.selectedStatusId.set('s-returned');
    fixture.detectChanges();

    testId<HTMLButtonElement>('loan-return-confirm')!.click();
    fixture.detectChanges();

    http.expectOne(`/api/loans/${openLoan.id}/return`).flush({
      ...openLoan,
      loanStatusId: 's-returned',
      loanStatusName: 'Returned',
      loanStatusIsTerminal: true,
      returnedAt: '2026-09-19T15:00:00Z',
      isOpen: false,
    });
    tick();
    fixture.detectChanges();

    expect(testId('loan-detail-status')!.textContent).toContain('Returned');
    expect(testId('loan-detail-status')!.textContent).toContain('this loan is closed');
    expect(testId('loan-detail-returned-at')).not.toBeNull();
    expect(testId('loan-return-open'))
      .withContext('a closed loan must not offer Return again')
      .toBeNull();
    expect(testId('loan-closed-note')).not.toBeNull();

    // Snackbar cleanup so the overlay does not leak into the next spec.
    tick(6000);
    fixture.detectChanges();
    tick();
  }));

  it('renders a rejected return inline on the panel and leaves the loan open', fakeAsync(async () => {
    await render(openLoan);

    testId<HTMLButtonElement>('loan-return-open')!.click();
    fixture.detectChanges();
    fixture.componentInstance.selectedStatusId.set('s-returned');
    fixture.detectChanges();
    testId<HTMLButtonElement>('loan-return-confirm')!.click();
    fixture.detectChanges();

    http.expectOne(`/api/loans/${openLoan.id}/return`).flush(
      {
        title: 'Rule violation',
        status: 422,
        detail: "'Checked Out' is not a terminal status, so it cannot close a loan.",
      },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    tick();
    fixture.detectChanges();

    const error = testId('loan-return-error')!;
    expect(error).not.toBeNull();
    expect(error.getAttribute('role')).toBe('alert');
    expect(error.textContent).toContain('not a terminal status');
    expect(testId('loan-detail-status')!.textContent).toContain('Checked Out');
  }));

  it('hides the Return action entirely on a closed loan', async () => {
    await render({
      ...openLoan,
      loanStatusId: 's-lost',
      loanStatusName: 'Lost',
      loanStatusIsTerminal: true,
      returnedAt: '2026-09-19T15:00:00Z',
      isOpen: false,
    });

    expect(testId('loan-return-open')).toBeNull();
    expect(testId('loan-closed-note')!.textContent).toContain('Lost');
  });
});
