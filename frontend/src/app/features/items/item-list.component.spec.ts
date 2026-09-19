import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

import { ItemListComponent } from './item-list.component';
import { problemDetailsInterceptor } from '../../core/interceptors/problem-details.interceptor';
import { Item, PagedResult } from '../../core/models/api.models';

/**
 * Item list — the derived availability chip.
 *
 * `isOnLoan` comes from the API's derivation over open-loan state; there is no stored
 * availability column. The chip must carry its own TEXT, because colour is never the
 * sole signal (WCAG 2.2 AA, LoanTracker_UI_Standard.md §1).
 */
describe('ItemListComponent', () => {
  let fixture: ComponentFixture<ItemListComponent>;
  let http: HttpTestingController;
  let element: HTMLElement;

  const available: Item = {
    id: 'i-available',
    name: 'Projector',
    description: null,
    assetTag: 'AV-2001',
    itemCategoryId: 'c-av',
    itemCategoryName: 'AV Equipment',
    isActive: true,
    isOnLoan: false,
    currentBorrowerName: null,
    currentLoanId: null,
  };

  const onLoan: Item = {
    ...available,
    id: 'i-onloan',
    name: 'Cordless Drill',
    assetTag: 'PT-1001',
    itemCategoryName: 'Power Tools',
    isOnLoan: true,
    currentBorrowerName: 'Avery Test',
    currentLoanId: 'l-1',
  };

  function page(items: Item[]): PagedResult<Item> {
    return { items, totalCount: items.length, page: 1, pageSize: 25 };
  }

  async function render(items: Item[]): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [ItemListComponent],
      providers: [
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ItemListComponent);
    element = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();

    http.expectOne((r) => r.url === '/api/item-categories').flush([]);
    http.expectOne((r) => r.url === '/api/items').flush(page(items));
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  const chips = (): HTMLElement[] =>
    Array.from(element.querySelectorAll<HTMLElement>('[data-testid="item-availability-chip"]'));

  it('renders "Available" as text, not colour alone, for an item with no open loan', async () => {
    await render([available]);

    expect(chips().length).toBe(1);
    expect(chips()[0].textContent!.trim()).toBe('Available');
    expect(chips()[0].classList).toContain('lt-chip-available');
    expect(chips()[0].classList).not.toContain('lt-chip-onloan');
  });

  it('renders "On loan" as text and names the current borrower', async () => {
    await render([onLoan]);

    expect(chips()[0].textContent!.trim()).toBe('On loan');
    expect(chips()[0].classList).toContain('lt-chip-onloan');
    expect(element.querySelector('[data-testid="item-list-table"]')!.textContent).toContain(
      'Avery Test',
    );
  });

  it('derives each row independently on a mixed page', async () => {
    await render([available, onLoan]);

    const rendered = chips().map((c) => c.textContent!.trim());
    expect(rendered).toEqual(['Available', 'On loan']);
  });

  it('shows an em dash rather than a blank cell when no borrower holds the item', async () => {
    await render([available]);

    const row = element.querySelector('[data-testid="item-list-table"] tbody tr')!;
    expect(row.textContent).toContain('—');
  });

  it('shows the no-data empty state, not the filtered one, when nothing exists yet', async () => {
    await render([]);

    expect(element.querySelector('[data-testid="item-list-empty"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="item-list-empty-filtered"]')).toBeNull();
  });

  it('renders the API failure reason inline with a retry rather than an empty grid', async () => {
    await TestBed.configureTestingModule({
      imports: [ItemListComponent],
      providers: [
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ItemListComponent);
    element = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();

    http.expectOne((r) => r.url === '/api/item-categories').flush([]);
    http.expectOne((r) => r.url === '/api/items').flush(
      { title: 'Unexpected error', status: 500, detail: 'Something went wrong handling this request.' },
      { status: 500, statusText: 'Server Error' },
    );
    fixture.detectChanges();

    const panel = element.querySelector('[data-testid="item-list-error"]')!;
    expect(panel).not.toBeNull();
    expect(panel.getAttribute('role')).toBe('alert');
    expect(panel.textContent).toContain('Something went wrong');
    expect(element.querySelector('[data-testid="item-list-retry"]')).not.toBeNull();
  });
});
