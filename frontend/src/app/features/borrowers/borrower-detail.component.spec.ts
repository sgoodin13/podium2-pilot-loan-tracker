import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { BorrowerDetailComponent } from './borrower-detail.component';
import { problemDetailsInterceptor } from '../../core/interceptors/problem-details.interceptor';
import { Borrower, Loan } from '../../core/models/api.models';

/**
 * Borrower detail — the deactivate hard block, the mirror of the item retire guard.
 *
 * The banner must NAME the items still held: "a count alone is not accountable"
 * (SME, BR §8). That is asserted here as a requirement of the screen.
 */
describe('BorrowerDetailComponent — deactivate guard', () => {
  let fixture: ComponentFixture<BorrowerDetailComponent>;
  let http: HttpTestingController;
  let element: HTMLElement;

  const borrower: Borrower = {
    id: 'b-1',
    name: 'Avery Test',
    contactEmail: 'avery.test@example.invalid',
    contactPhone: '555-0101',
    department: 'Facilities',
    isActive: true,
    openLoanCount: 1,
  };

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

  async function render(loans: Loan[], override: Partial<Borrower> = {}): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [BorrowerDetailComponent],
      providers: [
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: borrower.id }) } },
        },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(BorrowerDetailComponent);
    element = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();

    http.expectOne(`/api/borrowers/${borrower.id}`).flush({ ...borrower, ...override });
    http.expectOne(`/api/borrowers/${borrower.id}/loans`).flush(loans);
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  const testId = <T extends HTMLElement>(id: string): T | null =>
    element.querySelector<T>(`[data-testid="${id}"]`);

  it('disables Deactivate AND names the specific item still held', async () => {
    await render([openLoan]);

    expect(testId<HTMLButtonElement>('borrower-deactivate-btn')!.disabled).toBeTrue();

    const banner = testId('borrower-detail-guard');
    expect(banner).not.toBeNull();
    expect(banner!.textContent).toContain('Deactivate is disabled');
    expect(banner!.textContent).toContain('1 open loan');
    expect(banner!.textContent)
      .withContext('a count alone is not accountable — name the item')
      .toContain('Cordless Drill');
    expect(banner!.textContent).toContain('PT-1001');
  });

  it('enumerates every held item and pluralises when more than one loan is open', async () => {
    await render([
      openLoan,
      { ...openLoan, id: 'loan-2', itemId: 'i-2', itemName: 'Projector', itemAssetTag: 'AV-2001' },
    ]);

    const banner = testId('borrower-detail-guard')!;
    expect(banner.textContent).toContain('2 open loans');
    expect(banner.textContent).toContain('PT-1001');
    expect(banner.textContent).toContain('AV-2001');
  });

  it('enables Deactivate and drops the banner once every loan is closed', async () => {
    await render([{ ...openLoan, returnedAt: '2026-09-19T15:00:00Z', isOpen: false }], {
      openLoanCount: 0,
    });

    expect(testId<HTMLButtonElement>('borrower-deactivate-btn')!.disabled).toBeFalse();
    expect(testId('borrower-detail-guard')).toBeNull();
  });

  it('keeps Deactivate disabled for an already-inactive borrower', async () => {
    await render([], { isActive: false, openLoanCount: 0 });

    expect(testId<HTMLButtonElement>('borrower-deactivate-btn')!.disabled).toBeTrue();
    expect(testId('borrower-inactive-chip')!.textContent).toContain('Inactive');
  });

  it('renders the loan history for this borrower', async () => {
    await render([openLoan]);

    expect(element.querySelectorAll('[data-testid="borrower-loan-history-row"]').length).toBe(1);
    expect(element.querySelector('[data-testid="borrower-loan-history-table"]')!.textContent)
      .toContain('PT-1001');
  });
});
