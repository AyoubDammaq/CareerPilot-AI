import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { ApplicationDto, DocumentSummaryDto } from '../../core/models';
import { problemMessage } from '../../shared/http-error';

const POST_SUBMISSION = ['Submitted', 'InterviewScheduled', 'InterviewCompleted', 'OfferReceived'];

@Component({
  selector: 'app-application-review-page',
  imports: [DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './application-review.page.html',
})
export class ApplicationReviewPage {
  private readonly api = inject(ApiService);

  readonly apps = signal<ApplicationDto[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly open = signal<string | null>(null);
  readonly detail = signal<Record<string, ApplicationDto>>({});
  readonly docs = signal<Record<string, DocumentSummaryDto[]>>({});
  readonly choice = signal<Record<string, { cv: string; letter: string }>>({});
  readonly readAck = signal<Record<string, boolean>>({});
  readonly flagAck = signal<Record<string, boolean>>({});
  readonly note = signal<Record<string, string>>({});
  readonly busy = signal<string | null>(null);
  readonly errors = signal<Record<string, string>>({});

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.listApplications().subscribe({
      next: (a) => {
        this.apps.set(a);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.loading.set(false);
        this.error.set(problemMessage(e, 'Could not load your applications.'));
      },
    });
  }

  toggle(app: ApplicationDto): void {
    if (this.open() === app.id) {
      this.open.set(null);
      return;
    }
    this.open.set(app.id);
    this.refresh(app.id, app.jobId);
  }

  /** Loads the full review (with documents and checks) and the documents that can be attached. */
  private refresh(id: string, jobId: string): void {
    this.api.getApplication(id).subscribe({
      next: (d) => this.applied(d),
      error: (e: unknown) => this.fail(id, e, 'Could not load this application.'),
    });
    this.api.jobDocuments(jobId).subscribe({
      next: (list) => this.docs.update((all) => ({ ...all, [jobId]: list })),
      error: (e: unknown) => this.fail(id, e, 'Could not load the documents for this job.'),
    });
  }

  private applied(d: ApplicationDto): void {
    this.detail.update((all) => ({ ...all, [d.id]: d }));
    this.apps.update((all) => all.map((a) => (a.id === d.id ? { ...d, review: null } : a)));
    this.choice.update((all) => ({ ...all, [d.id]: { cv: d.cvDocumentId ?? all[d.id]?.cv ?? '', letter: d.coverLetterDocumentId ?? all[d.id]?.letter ?? '' } }));
    this.readAck.update((all) => ({ ...all, [d.id]: false }));
    this.flagAck.update((all) => ({ ...all, [d.id]: false }));
    this.errors.update((all) => ({ ...all, [d.id]: '' }));
  }

  private fail(id: string, e: unknown, fallback: string): void {
    this.busy.set(null);
    this.errors.update((all) => ({ ...all, [id]: problemMessage(e, fallback) }));
  }

  private run(id: string, call: () => import('rxjs').Observable<ApplicationDto>, fallback: string): void {
    this.busy.set(id);
    this.errors.update((all) => ({ ...all, [id]: '' }));
    call().subscribe({
      next: (d) => {
        this.busy.set(null);
        this.applied(d);
      },
      error: (e: unknown) => this.fail(id, e, fallback),
    });
  }

  // ---- helpers for the template --------------------------------------------------------------

  d(app: ApplicationDto): ApplicationDto | null {
    return this.detail()[app.id] ?? null;
  }

  cvDocs(app: ApplicationDto): DocumentSummaryDto[] {
    return (this.docs()[app.jobId] ?? []).filter((x) => x.kind === 'Cv');
  }

  letterDocs(app: ApplicationDto): DocumentSummaryDto[] {
    return (this.docs()[app.jobId] ?? []).filter((x) => x.kind === 'CoverLetter');
  }

  pick(app: ApplicationDto, field: 'cv' | 'letter', value: string): void {
    this.choice.update((all) => ({ ...all, [app.id]: { cv: all[app.id]?.cv ?? '', letter: all[app.id]?.letter ?? '', [field]: value } }));
  }

  isBusy(app: ApplicationDto): boolean {
    return this.busy() === app.id;
  }

  canApprove(app: ApplicationDto): boolean {
    const r = this.d(app)?.review;
    if (!r || r.blockers.length > 0 || !r.bundleHash || !this.readAck()[app.id]) return false;
    return !r.claims || r.claims.clean || !!this.flagAck()[app.id];
  }

  isPostSubmission(app: ApplicationDto): boolean {
    return POST_SUBMISSION.includes(app.status);
  }

  statusLabel(status: string): string {
    const map: Record<string, string> = {
      Shortlisted: 'Choosing documents',
      AwaitingApproval: 'Waiting for your approval',
      Approved: 'Approved by you',
      PreparingApplication: 'Preparing',
      ReadyToSubmit: 'Ready for you to submit',
      Submitted: 'Submitted (confirmed by you)',
      SubmissionUnconfirmed: 'Submitted, not confirmed',
      RejectedByEmployer: 'Rejected by employer',
      InterviewScheduled: 'Interview scheduled',
      InterviewCompleted: 'Interview completed',
      OfferReceived: 'Offer received',
      OfferAccepted: 'Offer accepted',
      OfferDeclined: 'Offer declined',
      Withdrawn: 'Withdrawn',
      ClosedOrExpired: 'Closed or expired',
    };
    return map[status] ?? status;
  }

  // ---- actions ---------------------------------------------------------------------------------

  setReadAck(app: ApplicationDto, event: Event): void {
    const v = (event.target as HTMLInputElement).checked;
    this.readAck.update((all) => ({ ...all, [app.id]: v }));
  }

  setFlagAck(app: ApplicationDto, event: Event): void {
    const v = (event.target as HTMLInputElement).checked;
    this.flagAck.update((all) => ({ ...all, [app.id]: v }));
  }

  setNote(app: ApplicationDto, event: Event): void {
    const v = (event.target as HTMLInputElement).value;
    this.note.update((all) => ({ ...all, [app.id]: v }));
  }

  useDocuments(app: ApplicationDto): void {
    const c = this.choice()[app.id];
    if (!c?.cv) {
      this.errors.update((all) => ({ ...all, [app.id]: 'Choose a CV first.' }));
      return;
    }
    this.run(app.id, () => this.api.setApplicationDocuments(app.id, c.cv, c.letter || null), 'Could not attach the documents.');
  }

  requestReview(app: ApplicationDto): void {
    this.run(app.id, () => this.api.applicationAction(app.id, 'request-review'), 'Could not start the review.');
  }

  approve(app: ApplicationDto): void {
    const r = this.d(app)?.review;
    if (!r?.bundleHash || !this.canApprove(app)) return;
    this.run(app.id, () => this.api.approveApplication(app.id, r.bundleHash!, !!this.flagAck()[app.id]), 'Could not approve.');
  }

  withdrawApproval(app: ApplicationDto): void {
    this.run(app.id, () => this.api.applicationAction(app.id, 'withdraw-approval'), 'Could not withdraw the approval.');
  }

  markReady(app: ApplicationDto): void {
    this.run(app.id, () => this.api.applicationAction(app.id, 'ready'), 'Could not mark it ready.');
  }

  submitted(app: ApplicationDto, confirmed: boolean): void {
    this.run(app.id, () => this.api.recordSubmission(app.id, confirmed, this.note()[app.id] ?? ''), 'Could not record the submission.');
  }

  confirmSubmission(app: ApplicationDto): void {
    this.run(app.id, () => this.api.applicationAction(app.id, 'confirm-submission'), 'Could not confirm.');
  }

  setStatus(app: ApplicationDto, status: string): void {
    this.run(app.id, () => this.api.setApplicationStatus(app.id, status), 'Could not change the status.');
  }

  download(app: ApplicationDto, documentId: string | null): void {
    if (!documentId) return;
    this.busy.set(app.id);
    this.api.downloadDocument(documentId).subscribe({
      next: (res) => {
        this.busy.set(null);
        if (!res.body) return;
        const name = /filename="?([A-Za-z0-9._-]+)"?/.exec(res.headers.get('content-disposition') ?? '')?.[1] ?? 'document.docx';
        const url = URL.createObjectURL(res.body);
        const a = document.createElement('a');
        a.href = url;
        a.download = name;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (e: unknown) => this.fail(app.id, e, 'Could not download the file.'),
    });
  }
}
