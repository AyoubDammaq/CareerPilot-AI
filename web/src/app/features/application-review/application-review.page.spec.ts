import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ApplicationDto, DocumentSummaryDto } from '../../core/models';
import { sampleApplication, sampleReview } from '../../testing/fixtures';
import { ApplicationReviewPage } from './application-review.page';

const cvDoc: DocumentSummaryDto = { id: 'cv1', kind: 'Cv', title: 'CV', createdAt: '2026-10-21T10:00:00Z', contentHash: 'aaaaaaaa11111111', stale: false, source: null };
const letterDoc: DocumentSummaryDto = { id: 'l1', kind: 'CoverLetter', title: 'Letter', createdAt: '2026-10-21T10:05:00Z', contentHash: 'bbbbbbbb22222222', stale: false, source: 'ai:m' };

describe('ApplicationReviewPage', () => {
  let ctrl: HttpTestingController;

  function create(apps: ApplicationDto[] = [sampleApplication()]): ComponentFixture<ApplicationReviewPage> {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
    const f = TestBed.createComponent(ApplicationReviewPage);
    f.detectChanges();
    ctrl.expectOne('/api/applications').flush(apps);
    f.detectChanges();
    return f;
  }

  const el = (f: ComponentFixture<ApplicationReviewPage>) => f.nativeElement as HTMLElement;
  const btn = (f: ComponentFixture<ApplicationReviewPage>, text: string) =>
    Array.from(el(f).querySelectorAll('button, a')).find((b) => b.textContent!.includes(text)) as HTMLButtonElement | undefined;

  /** Opens the first application and answers the two loads it triggers. */
  function open(f: ComponentFixture<ApplicationReviewPage>, app: ApplicationDto, docs: DocumentSummaryDto[] = [cvDoc, letterDoc]) {
    btn(f, 'Open')!.click();
    ctrl.expectOne(`/api/applications/${app.id}`).flush(app);
    ctrl.expectOne(`/api/jobs/${app.jobId}/documents`).flush(docs);
    f.detectChanges();
  }

  it('says plainly that nothing is sent automatically', () => {
    const f = create();
    expect(el(f).textContent).toContain('Nothing is ever sent for you');
    expect(el(f).textContent).toContain('Changing a document after approval withdraws the approval');
  });

  it('shows an empty state pointing to Job Opportunities', () => {
    const f = create([]);
    expect(el(f).textContent).toContain('No applications yet');
    expect(el(f).querySelector('a[href="/opportunities"]')).not.toBeNull();
  });

  it('shows an error with retry when loading fails', () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
    const f = TestBed.createComponent(ApplicationReviewPage);
    f.detectChanges();
    ctrl.expectOne('/api/applications').flush({ title: 'nope' }, { status: 500, statusText: 'Server Error' });
    f.detectChanges();
    expect(el(f).querySelector('[role="alert"]')).not.toBeNull();
    btn(f, 'Try again')!.click();
    ctrl.expectOne('/api/applications').flush([]);
  });

  it('lists applications with a plain-language status and flags a closed posting', () => {
    const f = create([sampleApplication({ status: 'AwaitingApproval', jobStatus: 'Closed' })]);
    expect(el(f).textContent).toContain('Waiting for your approval');
    expect(el(f).textContent).toContain('This posting is no longer open');
  });

  describe('choosing documents', () => {
    it('asks for a CV first and attaches the chosen documents', () => {
      const app = sampleApplication();
      const f = create([app]);
      open(f, app);
      btn(f, 'Use these documents')!.click();
      f.detectChanges();
      expect(el(f).textContent).toContain('Choose a CV first.');
      ctrl.expectNone((r) => r.method === 'PUT');

      f.componentInstance.pick(app, 'cv', 'cv1');
      f.componentInstance.pick(app, 'letter', 'l1');
      btn(f, 'Use these documents')!.click();
      const req = ctrl.expectOne((r) => r.method === 'PUT' && r.url === '/api/applications/a1/documents');
      expect(req.request.body).toEqual({ cvDocumentId: 'cv1', coverLetterDocumentId: 'l1' });
      req.flush({ ...app, cvDocumentId: 'cv1', coverLetterDocumentId: 'l1' });
      f.detectChanges();
      expect(btn(f, 'Review before approval')).toBeDefined();
    });

    it('explains how to prepare a CV when none exists', () => {
      const app = sampleApplication();
      const f = create([app]);
      open(f, app, []);
      expect(el(f).textContent).toContain('No CV has been prepared for this job yet');
      expect(btn(f, 'Use these documents')).toBeUndefined();
    });

    it('requests the review once a CV is attached', () => {
      const app = sampleApplication({ cvDocumentId: 'cv1' });
      const f = create([app]);
      open(f, app);
      btn(f, 'Review before approval')!.click();
      ctrl.expectOne((r) => r.method === 'POST' && r.url === '/api/applications/a1/request-review').flush({ ...app, status: 'AwaitingApproval', review: sampleReview });
      f.detectChanges();
      expect(el(f).textContent).toContain('What you are approving');
    });
  });

  describe('approval', () => {
    const awaiting = (over: Partial<ApplicationDto> = {}) =>
      sampleApplication({ status: 'AwaitingApproval', cvDocumentId: 'cv1', coverLetterDocumentId: 'l1', review: sampleReview, ...over });

    it('shows exactly what will be sent, and keeps Approve disabled until the owner confirms they read it', () => {
      const app = awaiting();
      const f = create([app]);
      open(f, app);
      const text = el(f).querySelector('[aria-label="What will be sent"]')!.textContent!;
      expect(text).toContain('abcdef012345');
      expect(text).toContain('Ayoub Test');
      expect(text).toContain('Sagemcom');
      expect(text).toContain('Dear Hiring Team,');
      expect(text).toContain('First paragraph.');
      expect(btn(f, 'Approve')!.disabled).toBe(true);

      (el(f).querySelector('input[id^="read-"]') as HTMLInputElement).click();
      f.detectChanges();
      expect(btn(f, 'Approve')!.disabled).toBe(false);
    });

    it('approves with the fingerprint it showed', () => {
      const app = awaiting();
      const f = create([app]);
      open(f, app);
      (el(f).querySelector('input[id^="read-"]') as HTMLInputElement).click();
      f.detectChanges();
      btn(f, 'Approve')!.click();
      const req = ctrl.expectOne((r) => r.method === 'POST' && r.url === '/api/applications/a1/approve');
      expect(req.request.body).toEqual({ bundleHash: 'abcdef0123456789abcdef', acknowledgeFlags: false });
      req.flush({ ...app, status: 'Approved', approvedAt: '2026-10-21T11:00:00Z', approvedBundleHash: 'abcdef0123456789abcdef' });
      f.detectChanges();
      expect(el(f).textContent).toContain('Nothing has been sent');
      expect(btn(f, 'Mark ready to submit')).toBeDefined();
    });

    it('requires a second, explicit acknowledgement when the letter has flagged claims', () => {
      const flagged = { ...sampleReview, claims: { clean: false, flags: [{ kind: 'Skill' as const, text: 'Rust', message: '"Rust" is not a skill on your profile.' }] } };
      const app = awaiting({ review: flagged });
      const f = create([app]);
      open(f, app);
      expect(el(f).textContent).toContain('Unsupported claims flagged in the letter');
      expect(el(f).textContent).toContain('"Rust" is not a skill');
      (el(f).querySelector('input[id^="read-"]') as HTMLInputElement).click();
      f.detectChanges();
      expect(btn(f, 'Approve')!.disabled).toBe(true);
      (el(f).querySelector('input[id^="flag-"]') as HTMLInputElement).click();
      f.detectChanges();
      expect(btn(f, 'Approve')!.disabled).toBe(false);
      btn(f, 'Approve')!.click();
      expect(ctrl.expectOne((r) => r.url === '/api/applications/a1/approve').request.body.acknowledgeFlags).toBe(true);
    });

    it('cannot be approved while there are blockers', () => {
      const app = awaiting({ review: { ...sampleReview, blockers: ['Your profile changed after these documents were prepared. Prepare them again so they reflect it.'] } });
      const f = create([app]);
      open(f, app);
      expect(el(f).querySelector('[role="alert"]')!.textContent).toContain('profile changed');
      (el(f).querySelector('input[id^="read-"]') as HTMLInputElement).click();
      f.detectChanges();
      expect(btn(f, 'Approve')!.disabled).toBe(true);
    });

    it('shows the server message when approval is refused', () => {
      const app = awaiting();
      const f = create([app]);
      open(f, app);
      (el(f).querySelector('input[id^="read-"]') as HTMLInputElement).click();
      f.detectChanges();
      btn(f, 'Approve')!.click();
      ctrl.expectOne((r) => r.url === '/api/applications/a1/approve').flush(
        { title: 'What you reviewed is not what is attached now. Reload and review the current documents.', status: 409 }, { status: 409, statusText: 'Conflict' });
      f.detectChanges();
      expect(el(f).querySelector('[role="alert"]')!.textContent).toContain('not what is attached now');
    });

    it('lets the owner withdraw an approval and mark ready', () => {
      const app = awaiting({ status: 'Approved', approvedAt: '2026-10-21T11:00:00Z' });
      const f = create([app]);
      open(f, app);
      btn(f, 'Withdraw approval')!.click();
      ctrl.expectOne((r) => r.url === '/api/applications/a1/withdraw-approval').flush({ ...app, status: 'AwaitingApproval' });
      f.detectChanges();
      expect(btn(f, 'Approve')).toBeDefined();
    });
  });

  describe('guided manual submission', () => {
    const ready = () => sampleApplication({ status: 'ReadyToSubmit', cvDocumentId: 'cv1', coverLetterDocumentId: 'l1', review: sampleReview, allowedNext: ['Withdrawn'] });

    it('states that CareerPilot sends nothing and offers the files and the employer link', () => {
      const app = ready();
      const f = create([app]);
      open(f, app);
      const s = el(f).querySelector('[aria-label="Submit it yourself"]')!;
      expect(s.textContent).toContain('CareerPilot does not contact employers');
      expect(s.textContent).toContain('Do not edit them first');
      const link = s.querySelector('a')!;
      expect(link.getAttribute('href')).toBe('https://careers.example.test/1/apply');
      expect(link.getAttribute('target')).toBe('_blank');
      expect(link.getAttribute('rel')).toBe('noopener noreferrer');
    });

    it('downloads documents through the API', () => {
      const app = ready();
      const f = create([app]);
      open(f, app);
      const create$ = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
      const revoke = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
      btn(f, 'Download CV')!.click();
      ctrl.expectOne('/api/documents/cv1/download').flush(new Blob(['x']), { headers: { 'Content-Disposition': 'attachment; filename=CV-A.docx' } });
      btn(f, 'Download cover letter')!.click();
      ctrl.expectOne('/api/documents/l1/download').flush(new Blob(['y']));
      expect(create$).toHaveBeenCalledTimes(2);
      create$.mockRestore();
      revoke.mockRestore();
    });

    it('records an owner-confirmed submission with the note', () => {
      const app = ready();
      const f = create([app]);
      open(f, app);
      const note = el(f).querySelector('input[id^="note-"]') as HTMLInputElement;
      note.value = 'Saw confirmation page';
      note.dispatchEvent(new Event('input'));
      btn(f, 'I submitted it and saw a confirmation')!.click();
      const req = ctrl.expectOne((r) => r.method === 'POST' && r.url === '/api/applications/a1/submission');
      expect(req.request.body).toEqual({ confirmed: true, note: 'Saw confirmation page' });
      req.flush({ ...app, status: 'Submitted', submittedAt: '2026-10-21T12:00:00Z', submissionNote: 'Saw confirmation page', allowedNext: ['InterviewScheduled', 'RejectedByEmployer', 'Withdrawn'] });
      f.detectChanges();
      expect(el(f).textContent).toContain('(as reported by you)');
      expect(btn(f, 'Interview scheduled')).toBeDefined();
    });

    it('records an unsure submission as unconfirmed and lets the owner confirm it later', () => {
      const app = ready();
      const f = create([app]);
      open(f, app);
      btn(f, 'not sure it went through')!.click();
      const req = ctrl.expectOne((r) => r.url === '/api/applications/a1/submission');
      expect(req.request.body.confirmed).toBe(false);
      req.flush({ ...app, status: 'SubmissionUnconfirmed', allowedNext: ['Submitted', 'Withdrawn'] });
      f.detectChanges();
      expect(el(f).textContent).toContain('Submitted, not confirmed');
      btn(f, 'I have confirmed it went through')!.click();
      ctrl.expectOne((r) => r.url === '/api/applications/a1/confirm-submission').flush({ ...app, status: 'Submitted', submittedAt: '2026-10-21T12:00:00Z', allowedNext: [] });
      f.detectChanges();
      expect(el(f).textContent).toContain('Submitted (confirmed by you)');
    });
  });

  it('offers withdrawal only where the workflow allows it', () => {
    const app = sampleApplication({ allowedNext: ['AwaitingApproval', 'Withdrawn'] });
    const f = create([app]);
    open(f, app);
    btn(f, 'Withdraw this application')!.click();
    ctrl.expectOne((r) => r.url === '/api/applications/a1/status').flush({ ...app, status: 'Withdrawn', allowedNext: [] });
    f.detectChanges();
    expect(el(f).textContent).toContain('Withdrawn');
    expect(btn(f, 'Withdraw this application')).toBeUndefined();
  });
});
