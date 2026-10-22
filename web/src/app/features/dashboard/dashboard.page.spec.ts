import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DashboardPage } from './dashboard.page';

describe('DashboardPage', () => {
  let ctrl: HttpTestingController;

  function create() {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
    return fixture;
  }

  it('renders completeness and missing items from the response', async () => {
    const fixture = create();
    ctrl.expectOne('/api/dashboard').flush({
      profileCompleteness: 62,
      missingItems: ['Add a phone number', 'Add at least one project'],
      implementedModules: ['Profile'],
    });
    await fixture.whenStable();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    const bar = el.querySelector('[role="progressbar"]')!;
    expect(bar.getAttribute('aria-valuenow')).toBe('62');
    expect(el.querySelector('.meter-value')!.textContent).toContain('62%');
    const links = Array.from(el.querySelectorAll('.missing-list a'));
    expect(links.map((a) => a.textContent!.trim())).toEqual(['Add a phone number', 'Add at least one project']);
    expect(links.every((a) => a.getAttribute('href') === '/profile')).toBe(true);
    expect(el.textContent).toContain('Profile');
  });

  it('shows future areas as phase labels without numbers', async () => {
    const fixture = create();
    ctrl.expectOne('/api/dashboard').flush({ profileCompleteness: 100, missingItems: [], implementedModules: [] });
    await fixture.whenStable();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const badges = Array.from(el.querySelectorAll('.card-soon .badge')).map((b) => b.textContent!.trim());
    expect(badges).toContain('Available in Phase 4');
    expect(badges).not.toContain('Available in Phase 5'); // Application Review is now a working page
    expect(badges).toContain('Available in Phase 7');
    expect(badges.every((b) => b.startsWith('Available in'))).toBe(true);
    // No missing items: no list, a positive statement instead
    expect(el.querySelector('.missing-list')).toBeNull();
    // Cards contain no digits other than inside the phase badge
    for (const card of Array.from(el.querySelectorAll('.card-soon'))) {
      const text = card.textContent!.replace(/Available in[^\n]*/, '');
      expect(/\d/.test(text)).toBe(false);
    }
  });

  it('shows an error with retry when the request fails', async () => {
    const fixture = create();
    ctrl.expectOne('/api/dashboard').flush({}, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')!.textContent).toContain('Could not load the dashboard');
    el.querySelector<HTMLButtonElement>('[role="alert"] button')!.click();
    ctrl.expectOne('/api/dashboard');
  });
});
