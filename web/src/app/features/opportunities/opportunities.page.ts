import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { debounceTime } from 'rxjs';
import { ApiService } from '../../core/api/api.service';
import { CoverLetterDto, DocumentDto, GeoFit, JobAnalysisDto, JobDetailDto, JobFilters, JobSummaryDto, UserDecision } from '../../core/models';
import { problemMessage } from '../../shared/http-error';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-opportunities-page',
  imports: [ReactiveFormsModule, DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './opportunities.page.html',
})
export class OpportunitiesPage {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly jobs = signal<JobSummaryDto[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly rescoring = signal(false);
  readonly started = signal<Record<string, string>>({});
  readonly startErrors = signal<Record<string, string>>({});
  readonly cvs = signal<Record<string, DocumentDto>>({});
  readonly letters = signal<Record<string, CoverLetterDto>>({});
  readonly letterText = signal<Record<string, string>>({});
  readonly letterBusy = signal<Set<string>>(new Set());
  readonly letterErrors = signal<Record<string, string>>({});
  readonly cvBusy = signal<Set<string>>(new Set());
  readonly cvErrors = signal<Record<string, string>>({});
  readonly analysing = signal<Set<string>>(new Set());
  readonly analysisErrors = signal<Record<string, string>>({});
  readonly notice = signal<string | null>(null);

  readonly details = signal<Record<string, JobDetailDto | 'loading' | 'error'>>({});
  readonly open = signal<Set<string>>(new Set());

  readonly filters = new FormGroup({
    q: new FormControl('', { nonNullable: true }),
    country: new FormControl('', { nonNullable: true }),
    workMode: new FormControl('', { nonNullable: true }),
    geoFit: new FormControl('', { nonNullable: true }),
    status: new FormControl('', { nonNullable: true }),
    decision: new FormControl('', { nonNullable: true }),
    sort: new FormControl('score', { nonNullable: true }),
    minScore: new FormControl('', { nonNullable: true }),
    showFiltered: new FormControl(false, { nonNullable: true }),
  });

  constructor() {
    this.filters.valueChanges.pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.page.set(1);
      this.load();
    });
    this.load();
  }

  get pages(): number {
    return Math.max(1, Math.ceil(this.total() / PAGE_SIZE));
  }

  private currentFilters(): JobFilters {
    const v = this.filters.getRawValue();
    return {
      q: v.q.trim(),
      country: v.country.trim(),
      workMode: v.workMode,
      geoFit: v.geoFit,
      status: v.status,
      decision: v.decision,
      sort: v.sort,
      minScore: v.minScore ? Number(v.minScore) : undefined,
      relevantOnly: !v.showFiltered,
      page: this.page(),
      pageSize: PAGE_SIZE,
    };
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.listJobs(this.currentFilters()).subscribe({
      next: (r) => {
        this.jobs.set(r.items);
        this.total.set(r.total);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.loading.set(false);
        this.error.set(problemMessage(e, 'Could not load jobs.'));
      },
    });
  }

  go(delta: number): void {
    const next = this.page() + delta;
    if (next < 1 || next > this.pages) return;
    this.page.set(next);
    this.load();
  }

  rescore(): void {
    this.rescoring.set(true);
    this.actionError.set(null);
    this.notice.set(null);
    this.api.rescore().subscribe({
      next: (r) => {
        this.rescoring.set(false);
        this.notice.set(
          r.profileMissing ? 'Create your profile first: scores compare jobs with it.' : `Re-scored ${r.scored} ${r.scored === 1 ? 'job' : 'jobs'} with your current profile.`,
        );
        this.details.set({});
        this.load();
      },
      error: (e: unknown) => {
        this.rescoring.set(false);
        this.actionError.set(problemMessage(e, 'Could not re-score the jobs.'));
      },
    });
  }

  clearFilters(): void {
    this.filters.reset();
  }

  // ---- row actions ----------------------------------------------------------------------------

  decide(job: JobSummaryDto, decision: UserDecision): void {
    this.actionError.set(null);
    this.api.setDecision(job.id, decision).subscribe({
      next: (updated) => {
        const hideNow = decision === 'Excluded' && this.filters.controls.decision.value !== 'Excluded';
        this.jobs.update((all) => (hideNow ? all.filter((j) => j.id !== job.id) : all.map((j) => (j.id === job.id ? updated : j))));
        if (hideNow) this.total.update((t) => Math.max(0, t - 1));
      },
      error: (e: unknown) => this.actionError.set(problemMessage(e, `Could not update "${job.title}".`)),
    });
  }

  toggleDetails(job: JobSummaryDto): void {
    const next = new Set(this.open());
    if (next.has(job.id)) {
      next.delete(job.id);
      this.open.set(next);
      return;
    }
    next.add(job.id);
    this.open.set(next);
    if (this.details()[job.id]) return;

    this.details.update((d) => ({ ...d, [job.id]: 'loading' }));
    this.api.getJob(job.id).subscribe({
      next: (d) => this.details.update((all) => ({ ...all, [job.id]: d })),
      error: () => this.details.update((all) => ({ ...all, [job.id]: 'error' })),
    });
  }

  isOpen(job: JobSummaryDto): boolean {
    return this.open().has(job.id);
  }

  detail(job: JobSummaryDto): JobDetailDto | null {
    const d = this.details()[job.id];
    return d && d !== 'loading' && d !== 'error' ? d : null;
  }

  detailState(job: JobSummaryDto): 'loading' | 'error' | null {
    const d = this.details()[job.id];
    return d === 'loading' || d === 'error' ? d : null;
  }

  // ---- presentation helpers -------------------------------------------------------------------

  geoLabel(fit: GeoFit): string {
    switch (fit) {
      case 'Match':
        return 'Location matches your targets';
      case 'NeedsReview':
        return 'Check eligibility';
      default:
        return 'Outside your target countries';
    }
  }

  analyse(job: JobSummaryDto, refresh = false): void {
    this.analysing.update((s) => new Set(s).add(job.id));
    this.analysisErrors.update((e) => ({ ...e, [job.id]: '' }));
    this.api.analyzeJob(job.id, refresh).subscribe({
      next: (a: JobAnalysisDto) => {
        this.analysing.update((s) => {
          const n = new Set(s);
          n.delete(job.id);
          return n;
        });
        this.details.update((all) => {
          const d = all[job.id];
          return d && d !== 'loading' && d !== 'error' ? { ...all, [job.id]: { ...d, analysis: a } } : all;
        });
      },
      error: (e: unknown) => {
        this.analysing.update((s) => {
          const n = new Set(s);
          n.delete(job.id);
          return n;
        });
        this.analysisErrors.update((all) => ({ ...all, [job.id]: problemMessage(e, 'The AI analysis failed.') }));
      },
    });
  }

  startApplication(job: JobSummaryDto): void {
    this.startErrors.update((e) => ({ ...e, [job.id]: '' }));
    this.api.startApplication(job.id).subscribe({
      next: (a) => this.started.update((s) => ({ ...s, [job.id]: a.status })),
      error: (e: unknown) => this.startErrors.update((all) => ({ ...all, [job.id]: problemMessage(e, 'Could not start the application.') })),
    });
  }

  prepareCv(job: JobSummaryDto): void {
    this.setCvBusy(job.id, true);
    this.cvErrors.update((e) => ({ ...e, [job.id]: '' }));
    this.api.createCv(job.id).subscribe({
      next: (doc) => {
        this.setCvBusy(job.id, false);
        this.cvs.update((all) => ({ ...all, [job.id]: doc }));
      },
      error: (e: unknown) => {
        this.setCvBusy(job.id, false);
        this.cvErrors.update((all) => ({ ...all, [job.id]: problemMessage(e, 'Could not prepare the CV.') }));
      },
    });
  }

  downloadCv(job: JobSummaryDto, format: 'docx' | 'pdf' = 'docx'): void {
    const doc = this.cvs()[job.id];
    if (!doc) return;
    this.setCvBusy(job.id, true);
    this.api.downloadDocument(doc.id, format).subscribe({
      next: (res) => {
        this.setCvBusy(job.id, false);
        if (!res.body) return;
        const name = /filename="?([A-Za-z0-9._-]+)"?/.exec(res.headers.get('content-disposition') ?? '')?.[1] ?? `CV.${format}`;
        const url = URL.createObjectURL(res.body);
        const a = document.createElement('a');
        a.href = url;
        a.download = name;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (e: unknown) => {
        this.setCvBusy(job.id, false);
        this.cvErrors.update((all) => ({ ...all, [job.id]: problemMessage(e, 'Could not download the CV.') }));
      },
    });
  }

  draftLetter(job: JobSummaryDto): void {
    this.setLetterBusy(job.id, true);
    this.letterErrors.update((e) => ({ ...e, [job.id]: '' }));
    this.api.draftCoverLetter(job.id).subscribe({
      next: (l) => this.letterArrived(job.id, l),
      error: (e: unknown) => this.letterFailed(job.id, e, 'Could not draft the letter.'),
    });
  }

  /** Saves the owner's edits as a new version and re-runs the claim check on it. */
  reviseLetter(job: JobSummaryDto): void {
    const current = this.letters()[job.id];
    if (!current) return;
    const paragraphs = (this.letterText()[job.id] ?? '')
      .split(/\n\s*\n/)
      .map((p) => p.trim())
      .filter((p) => p.length > 0);
    this.setLetterBusy(job.id, true);
    this.letterErrors.update((e) => ({ ...e, [job.id]: '' }));
    this.api.reviseCoverLetter(current.id, paragraphs).subscribe({
      next: (l) => this.letterArrived(job.id, l),
      error: (e: unknown) => this.letterFailed(job.id, e, 'Could not save your edits.'),
    });
  }

  onLetterInput(job: JobSummaryDto, event: Event): void {
    const value = (event.target as HTMLTextAreaElement).value;
    this.letterText.update((t) => ({ ...t, [job.id]: value }));
  }

  downloadLetter(job: JobSummaryDto, format: 'docx' | 'pdf' = 'docx'): void {
    const letter = this.letters()[job.id];
    if (!letter) return;
    this.setLetterBusy(job.id, true);
    this.api.downloadDocument(letter.id, format).subscribe({
      next: (res) => {
        this.setLetterBusy(job.id, false);
        if (!res.body) return;
        const name = /filename="?([A-Za-z0-9._-]+)"?/.exec(res.headers.get('content-disposition') ?? '')?.[1] ?? `Cover-letter.${format}`;
        const url = URL.createObjectURL(res.body);
        const a = document.createElement('a');
        a.href = url;
        a.download = name;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (e: unknown) => this.letterFailed(job.id, e, 'Could not download the letter.'),
    });
  }

  letterEdited(job: JobSummaryDto): boolean {
    const l = this.letters()[job.id];
    return !!l && (this.letterText()[job.id] ?? '') !== l.letter.paragraphs.join('\n\n');
  }

  isLetterBusy(job: JobSummaryDto): boolean {
    return this.letterBusy().has(job.id);
  }

  private letterArrived(id: string, l: CoverLetterDto): void {
    this.setLetterBusy(id, false);
    this.letters.update((all) => ({ ...all, [id]: l }));
    this.letterText.update((t) => ({ ...t, [id]: l.letter.paragraphs.join('\n\n') }));
  }

  private letterFailed(id: string, e: unknown, fallback: string): void {
    this.setLetterBusy(id, false);
    this.letterErrors.update((all) => ({ ...all, [id]: problemMessage(e, fallback) }));
  }

  private setLetterBusy(id: string, busy: boolean): void {
    this.letterBusy.update((s) => {
      const n = new Set(s);
      if (busy) n.add(id);
      else n.delete(id);
      return n;
    });
  }

  isCvBusy(job: JobSummaryDto): boolean {
    return this.cvBusy().has(job.id);
  }

  private setCvBusy(id: string, busy: boolean): void {
    this.cvBusy.update((s) => {
      const n = new Set(s);
      if (busy) n.add(id);
      else n.delete(id);
      return n;
    });
  }

  isAnalysing(job: JobSummaryDto): boolean {
    return this.analysing().has(job.id);
  }

  pct(c: { points: number; maxPoints: number }): number {
    return c.maxPoints <= 0 ? 0 : Math.round((c.points / c.maxPoints) * 100);
  }

  salary(job: JobSummaryDto): string | null {
    if (job.salaryMin === null && job.salaryMax === null) return null;
    const fmt = (n: number) => new Intl.NumberFormat('en', { maximumFractionDigits: 0 }).format(n);
    const range = job.salaryMin !== null && job.salaryMax !== null ? `${fmt(job.salaryMin)} to ${fmt(job.salaryMax)}` : fmt((job.salaryMin ?? job.salaryMax)!);
    return `${range} ${job.salaryCurrency ?? ''}`.trim() + (job.salaryPeriod ? ` (${job.salaryPeriod})` : '');
  }
}
