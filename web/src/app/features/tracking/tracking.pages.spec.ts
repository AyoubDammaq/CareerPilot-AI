import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TrackingSummary } from '../../core/models';
import { sampleApplication } from '../../testing/fixtures';
import { AnalyticsPage } from './analytics.page';
import { ApplicationsPage } from './applications.page';

const summary = (o: Partial<TrackingSummary> = {}): TrackingSummary => ({
  total: 4, active: 3, submitted: 3, replied: 1, rejected: 0, interviewing: 1, offers: 0, responseRate: 33, medianDaysToResponse: 4.5, smallSample: true,
  byStatus: [{ status: 'Submitted', count: 2 }, { status: 'InterviewScheduled', count: 1 }],
  byCountry: [{ name: 'France', submitted: 3, replied: 1 }],
  byScoreBand: [{ name: '70 to 100', submitted: 2, replied: 1 }, { name: 'Not scored', submitted: 1, replied: 0 }],
  weekly: Array.from({ length: 8 }, (_, i) => ({ weekStart: `2026-09-${String(7 + i).padStart(2, '0')}`, submitted: i === 7 ? 3 : 0 })),
  notes: ['Only 3 submitted applications: too few to draw conclusions.'],
  ...o,
});

describe('ApplicationsPage', () => {
  let ctrl: HttpTestingController;

  function create() {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
    const f = TestBed.createComponent(ApplicationsPage);
    f.detectChanges();
    return f;
  }

  it('lists applications with readable statuses and what needs attention', () => {
    const f = create();
    ctrl.expectOne('/api/applications').flush([
      sampleApplication({ id: 'a1', status: 'Submitted', submittedAt: '2026-10-01T10:00:00Z' }),
      sampleApplication({ id: 'a2', jobTitle: 'Old one', status: 'Withdrawn' }),
    ]);
    ctrl.expectOne('/api/tracking/reminders').flush([
      { applicationId: 'a1', kind: 'FollowUp', jobTitle: 'Backend Engineer', companyName: 'Datadog', message: 'No reply for 21 days.', days: 21 },
    ]);
    f.detectChanges();
    const text = (f.nativeElement as HTMLElement).textContent!;
    expect(text).toContain('Submitted (confirmed by you)');
    expect(text).toContain('No reply for 21 days.');
    expect(text).toContain('Follow up');
    expect(text).not.toContain('Old one'); // finished ones are hidden by default
  });

  it('can show finished applications', () => {
    const f = create();
    ctrl.expectOne('/api/applications').flush([sampleApplication({ id: 'a2', jobTitle: 'Old one', status: 'Withdrawn' })]);
    ctrl.expectOne('/api/tracking/reminders').flush([]);
    f.detectChanges();
    (f.nativeElement as HTMLElement).querySelector<HTMLInputElement>('#show-fin')!.click();
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).textContent).toContain('Old one');
    expect((f.nativeElement as HTMLElement).textContent).toContain('Nothing needs your attention');
  });

  it('still shows the list when reminders fail, and says plainly when there are none', () => {
    const f = create();
    ctrl.expectOne('/api/applications').flush([]);
    ctrl.expectOne('/api/tracking/reminders').flush({}, { status: 500, statusText: 'x' });
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).textContent).toContain('No applications yet');
  });

  it('shows an error when the list cannot be loaded', () => {
    const f = create();
    ctrl.expectOne('/api/applications').flush({}, { status: 500, statusText: 'x' });
    ctrl.expectOne('/api/tracking/reminders').flush([]);
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).querySelector('[role=alert]')).not.toBeNull();
  });
});

describe('AnalyticsPage', () => {
  let ctrl: HttpTestingController;

  function create(s: TrackingSummary) {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
    const f = TestBed.createComponent(AnalyticsPage);
    f.detectChanges();
    ctrl.expectOne('/api/tracking/summary').flush(s);
    f.detectChanges();
    return f.nativeElement as HTMLElement;
  }

  it('shows the figures and the small-sample warning', () => {
    const el = create(summary());
    expect(el.textContent).toContain('33%');
    expect(el.textContent).toContain('4.5');
    expect(el.textContent).toContain('too few to draw conclusions');
    expect(el.textContent).toContain('70 to 100');
    expect(el.textContent).toContain('France');
    expect(el.querySelectorAll('.bars li').length).toBe(8);
  });

  it('shows dashes, not made-up rates, when nothing was submitted', () => {
    const el = create(summary({ submitted: 0, replied: 0, responseRate: null, medianDaysToResponse: null, byCountry: [], byScoreBand: [] }));
    const rate = Array.from(el.querySelectorAll('.stats div')).find((d) => d.textContent!.includes('Reply rate'))!;
    expect(rate.textContent).toContain('—');
    expect(rate.textContent).not.toContain('%');
  });

  it('explains an empty history instead of showing zeros everywhere', () => {
    const el = create(summary({ total: 0, active: 0, submitted: 0, replied: 0, interviewing: 0, responseRate: null, medianDaysToResponse: null, byStatus: [], byCountry: [], byScoreBand: [] }));
    expect(el.textContent).toContain('Start an application');
    expect(el.querySelector('.stats')).toBeNull();
  });
});
