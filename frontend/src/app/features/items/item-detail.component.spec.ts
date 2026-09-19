import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { ItemDetailComponent } from './item-detail.component';
import { problemDetailsInterceptor } from '../../core/interceptors/problem-details.interceptor';
import { Item, Loan } from '../../core/models/api.models';

/**
 * Item detail — the retire hard block.
 *
 * SME ruling (BR §8, resolved): while an open loan exists, Retire is DISABLED and the
 * reason is STATED. A disabled control with no stated reason is the failure mode the
 * ruling exists to prevent, so the banner is asserted as a requirement, not decoration.
 */
describe('ItemDetailComponent — retire guard', () => {
  let fixture: ComponentFixture<ItemDetailComponent>;
  let http: HttpTestingController;
  let element: HTMLElement;

  const baseItem: Item = {
    id: 'i-1',
    name: 'Cordless Drill',
    description: '18V cordless drill',
    assetTag: 'PT-1001',
    itemCategoryId: 'c-1',
    itemCategoryName: 'Power Tools',
    isActive: true,
    isOnLoan: false,
    currentBorrowerName: null,
    currentLoanId: null,
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

  async function render(item: Item, loans: Loan[]): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [ItemDetailComponent],
      providers: [
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: item.id }) } },
        },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ItemDetailComponent);
    element = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();

    http.expectOne(`/api/items/${item.id}`).flush(item);
    http.expectOne(`/api/items/${item.id}/loans`).flush(loans);
    http.expectOne((r) => r.url === '/api/item-categories').flush([
      { id: 'c-1', name: 'Power Tools', description: null, isActive: true },
    ]);
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  const testId = <T extends HTMLElement>(id: string): T | null =>
    element.querySelector<T>(`[data-testid="${id}"]`);

  it('disables Retire AND states the reason while the item has an open loan', async () => {
    await render(
      { ...baseItem, isOnLoan: true, currentBorrowerName: 'Avery Test', currentLoanId: 'loan-1' },
      [openLoan],
    );

    const retire = testId<HTMLButtonElement>('item-retire-btn')!;
    expect(retire.disabled).toBeTrue();

    const banner = testId('item-detail-guard');
    expect(banner).withContext('the disabled button alone is not AA-sufficient').not.toBeNull();
    expect(banner!.textContent).toContain('Retire is disabled');
    expect(banner!.textContent).toContain('Avery Test');
    expect(banner!.textContent).toContain('Return it first');

    // The reason is programmatically associated with the control, not just adjacent.
    expect(retire.getAttribute('aria-describedby')).toBe('item-retire-guard-text');
  });

  it('shows the On loan chip with its own text label on the detail screen', async () => {
    await render({ ...baseItem, isOnLoan: true, currentBorrowerName: 'Avery Test' }, [openLoan]);

    expect(testId('item-availability-chip')!.textContent!.trim()).toBe('On loan');
  });

  it('enables Retire and drops the banner once nothing is out', async () => {
    await render(baseItem, [{ ...openLoan, returnedAt: '2026-09-19T15:00:00Z', isOpen: false }]);

    expect(testId<HTMLButtonElement>('item-retire-btn')!.disabled).toBeFalse();
    expect(testId('item-detail-guard')).toBeNull();
    expect(testId('item-availability-chip')!.textContent!.trim()).toBe('Available');
  });

  it('renders the loan history child grid, most recent first', async () => {
    await render(baseItem, [
      { ...openLoan, id: 'older', checkedOutAt: '2026-01-01T09:00:00Z', isOpen: false, returnedAt: '2026-01-02T09:00:00Z' },
      { ...openLoan, id: 'newer', checkedOutAt: '2026-09-19T09:00:00Z', isOpen: false, returnedAt: '2026-09-19T15:00:00Z' },
    ]);

    const rows = element.querySelectorAll('[data-testid="item-loan-history-row"]');
    expect(rows.length).toBe(2);
    expect(fixture.componentInstance.loans()[0].id).toBe('newer');
  });

  it('shows the empty state, not a blank grid, when the item has never been out', async () => {
    await render(baseItem, []);

    expect(testId('item-loan-history-empty')).not.toBeNull();
  });
});
