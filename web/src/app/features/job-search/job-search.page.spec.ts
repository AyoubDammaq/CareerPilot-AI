import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { sampleCompany, sampleRun } from '../../testing/fixtures';
import { JobSearchPage } from './job-search.page';

describe('JobSearchPage', () => {
  let ctrl: HttpTestingController;

  function create(companies = [sampleCompany()], runs = [sampleRun()]): ComponentFixture<JobSearchPage> {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    ctrl = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(JobSearchPage);
    fixture.detectChanges();
    ctrl.expectOne('/api/sources/companies').flush(companies);
    ctrl.expectOne('/api/search-runs').flush(runs);
    fixture.detectChanges();
    return fixture;
  }

  const el = (f: ComponentFixture<JobSearchPage>) => f.nativeElement as HTMLElement;
  const button = (f: ComponentFixture<JobSearchPage>, text: string) =>
    Array.from(el(f).querySelectorAll('button')).find((b) => b.textContent!.trim() === text) as HTMLButtonElement;

  afterEach(() => {
    vi.useRealTimers();
  });

  it('lists the boards with their state, last error and the latest run', () => {
    const f = create([sampleCompany(), sampleCompany({ id: 'c2', name: 'Broken Co', lastError: 'HTTP 404: this board was not found', lastSuccessAt: null })]);
    const text = el(f).textContent!;
    expect(text).toContain('Datadog');
    expect(text).toContain('42 jobs on the board');
    expect(text).toContain('Last attempt failed: HTTP 404');
    expect(text).toContain('Not read yet.');
    expect(text).toContain('2 of 2 boards read');
    expect(text).toContain('4 new');
  });

  it('states what is and is not searched', () => {
    const f = create();
    expect(el(f).textContent).toContain('does not search LinkedIn, Indeed');
    expect(el(f).textContent).toContain('Himalayas');
  });

  it('imports pasted job text and shows what the server warns about', () => {
    const f = create();
    const input = (id: string, v: string) => {
      const e = el(f).querySelector<HTMLInputElement | HTMLTextAreaElement>('#' + id)!;
      e.value = v;
      e.dispatchEvent(new Event('input'));
    };
    input('imp-title', 'Backend Engineer');
    input('imp-company', 'Acme');
    input('imp-desc', 'C# and ASP.NET Core services for customers.');
    button(f, 'Import pasted job').click();

    const req = ctrl.expectOne('/api/jobs/import/text');
    expect(req.request.body).toMatchObject({ title: 'Backend Engineer', company: 'Acme' });
    req.flush({ listingId: 'l1', created: true, title: 'Backend Engineer', company: 'Acme', warnings: ['Imported by hand: CareerPilot cannot confirm that this job is still open.'] });
    f.detectChanges();
    expect(el(f).textContent).toContain('Added: Backend Engineer at Acme');
    expect(el(f).textContent).toContain('cannot confirm');
  });

  it('shows the server reason when a page address is refused', () => {
    const f = create();
    const url = el(f).querySelector<HTMLInputElement>('#imp-url')!;
    url.value = 'https://www.linkedin.com/jobs/view/1';
    url.dispatchEvent(new Event('input'));
    button(f, 'Import from address').click();

    ctrl.expectOne('/api/jobs/import/url').flush(
      { title: 'Validation failed', errors: { url: ['CareerPilot does not fetch pages from www.linkedin.com automatically.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    f.detectChanges();
    expect(el(f).querySelector('[role=alert]')!.textContent).toContain('does not fetch pages from www.linkedin.com');
  });

  it('shows a helpful empty state', () => {
    const f = create([], []);
    expect(el(f).textContent).toContain('No company boards yet');
    expect(el(f).textContent).toContain('No search has been run yet');
  });

  it('shows an error with a retry when loading fails', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    ctrl = TestBed.inject(HttpTestingController);
    const f = TestBed.createComponent(JobSearchPage);
    f.detectChanges();
    ctrl.expectOne('/api/sources/companies').flush({}, { status: 500, statusText: 'x' });
    f.detectChanges();
    expect(el(f).querySelector('[role="alert"]')!.textContent).toContain('Could not load');
    button(f, 'Try again').click();
    ctrl.expectOne('/api/sources/companies');
  });

  describe('adding a company', () => {
    function fill(f: ComponentFixture<JobSearchPage>, name: string, token: string) {
      f.componentInstance.form.patchValue({ name, boardToken: token });
      f.detectChanges();
    }

    it('posts the trimmed values, lists the new company and clears the form', () => {
      const f = create();
      fill(f, '  Algolia ', 'algolia');
      (el(f).querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
      const req = ctrl.expectOne((r) => r.method === 'POST' && r.url === '/api/sources/companies');
      expect(req.request.body).toEqual({ name: 'Algolia', kind: 'Greenhouse', boardToken: 'algolia', useEuEndpoint: false, isEnabled: true });
      req.flush(sampleCompany({ id: 'c9', name: 'Algolia', boardToken: 'algolia' }), { status: 201, statusText: 'Created' });
      f.detectChanges();
      expect(el(f).textContent).toContain('Algolia');
      expect(f.componentInstance.form.controls.name.value).toBe('');
    });

    it('does not call the server for an invalid token and says why', () => {
      const f = create();
      fill(f, 'X', 'bad token!');
      (el(f).querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
      ctrl.expectNone((r) => r.method === 'POST');
      f.detectChanges();
      expect(el(f).textContent).toContain("Letters, digits, '-', '_' and '.' only.");
      expect(el(f).querySelector('#c-token')!.getAttribute('aria-invalid')).toBe('true');
    });

    it('shows server-side field errors on the field', () => {
      const f = create();
      fill(f, 'Datadog', 'datadog');
      (el(f).querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
      ctrl.expectOne((r) => r.method === 'POST').flush(
        { title: 'One or more validation errors occurred.', errors: { boardToken: ['This board is already on your list.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
      f.detectChanges();
      expect(el(f).textContent).toContain('This board is already on your list.');
      expect(el(f).textContent).toContain('Please fix the highlighted fields.');
    });

    it('offers the EU option only for Lever', () => {
      const f = create();
      expect(el(f).querySelector('#c-eu')).toBeNull();
      f.componentInstance.form.controls.kind.setValue('Lever');
      f.detectChanges();
      expect(el(f).querySelector('#c-eu')).not.toBeNull();
    });
  });

  it('toggles a board by sending the full record with isEnabled flipped', () => {
    const f = create();
    (el(f).querySelector('#en-c1') as HTMLInputElement).click();
    const req = ctrl.expectOne((r) => r.method === 'PUT' && r.url === '/api/sources/companies/c1');
    expect(req.request.body).toEqual({ name: 'Datadog', kind: 'Greenhouse', boardToken: 'datadog', useEuEndpoint: false, isEnabled: false });
    req.flush(sampleCompany({ isEnabled: false }));
    f.detectChanges();
    expect((el(f).querySelector('#en-c1') as HTMLInputElement).checked).toBe(false);
  });

  it('needs a second click to remove a board', () => {
    const f = create();
    button(f, 'Remove').click();
    f.detectChanges();
    ctrl.expectNone((r) => r.method === 'DELETE');
    expect(el(f).textContent).toContain('Confirm remove');

    button(f, 'Cancel').click();
    f.detectChanges();
    expect(el(f).textContent).not.toContain('Confirm remove');

    button(f, 'Remove').click();
    f.detectChanges();
    button(f, 'Confirm remove').click();
    ctrl.expectOne((r) => r.method === 'DELETE' && r.url === '/api/sources/companies/c1').flush(null, { status: 204, statusText: 'No Content' });
    f.detectChanges();
    expect(el(f).textContent).toContain('No company boards yet');
  });

  it('reports the result of testing a board', () => {
    const f = create();
    button(f, 'Test board').click();
    f.detectChanges();
    expect(button(f, 'Testing…').disabled).toBe(true);
    ctrl.expectOne('/api/sources/companies/c1/test').flush({ ok: true, jobCount: 17, error: null });
    f.detectChanges();
    expect(el(f).textContent).toContain('This board works: 17 jobs found.');

    button(f, 'Test board').click();
    ctrl.expectOne('/api/sources/companies/c1/test').flush({ ok: false, jobCount: null, error: 'HTTP 404: this board was not found' });
    f.detectChanges();
    expect(el(f).textContent).toContain('Test failed: HTTP 404');
  });

  describe('running a search', () => {
    it('polls until the run finishes, then refreshes the boards', () => {
      vi.useFakeTimers();
      const f = create();

      button(f, 'Search now').click();
      ctrl.expectOne((r) => r.method === 'POST' && r.url === '/api/search-runs').flush(sampleRun({ id: 'r2', status: 'Queued' }));
      f.detectChanges();
      expect(button(f, 'Searching…').disabled).toBe(true);
      expect(el(f).textContent).toContain('Waiting to start');

      vi.advanceTimersByTime(2000);
      ctrl.expectOne('/api/search-runs/r2').flush(sampleRun({ id: 'r2', status: 'Running' }));
      f.detectChanges();
      expect(el(f).textContent).toContain('Searching…');

      vi.advanceTimersByTime(2000);
      ctrl.expectOne('/api/search-runs/r2').flush(sampleRun({ id: 'r2', status: 'PartiallyFailed', errors: ['GitLab: HTTP 404: this board was not found'], jobsNew: 7 }));
      ctrl.expectOne('/api/sources/companies').flush([sampleCompany()]);
      f.detectChanges();

      expect(button(f, 'Search now').disabled).toBe(false);
      expect(el(f).textContent).toContain('Finished, but some boards could not be read.');
      expect(el(f).textContent).toContain('7 new');
      expect(el(f).textContent).toContain('GitLab: HTTP 404');
      vi.advanceTimersByTime(10_000);
      ctrl.expectNone('/api/search-runs/r2'); // polling stopped
    });

    it('shows the reason when a search cannot start', () => {
      const f = create();
      button(f, 'Search now').click();
      ctrl.expectOne((r) => r.method === 'POST').flush({ title: 'A search is already running. Wait for it to finish.' }, { status: 409, statusText: 'Conflict' });
      f.detectChanges();
      expect(el(f).querySelector('[role="alert"]')!.textContent).toContain('A search is already running');
      expect(button(f, 'Search now').disabled).toBe(false);
    });

    it('resumes polling when a run is already active on load', () => {
      vi.useFakeTimers();
      const f = create([sampleCompany()], [sampleRun({ id: 'r5', status: 'Running' })]);
      expect(button(f, 'Searching…').disabled).toBe(true);
      vi.advanceTimersByTime(2000);
      ctrl.expectOne('/api/search-runs/r5').flush(sampleRun({ id: 'r5', status: 'Succeeded' }));
      ctrl.expectOne('/api/sources/companies').flush([sampleCompany()]);
      f.detectChanges();
      expect(button(f, 'Search now').disabled).toBe(false);
    });

    it('stops polling when the page is left', () => {
      vi.useFakeTimers();
      const f = create([sampleCompany()], [sampleRun({ id: 'r6', status: 'Running' })]);
      f.destroy();
      vi.advanceTimersByTime(10_000);
      ctrl.expectNone('/api/search-runs/r6');
    });
  });
});
