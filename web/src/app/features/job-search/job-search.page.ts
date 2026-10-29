import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService } from '../../core/api/api.service';
import { CompanyDto, CompanyTestResult, JobSourceKind, SearchRunDto } from '../../core/models';
import { fieldErrors, problemMessage } from '../../shared/http-error';
import { ImportJobComponent } from './import-job.component';

const ACTIVE = ['Queued', 'Running'];

@Component({
  selector: 'app-job-search-page',
  imports: [ReactiveFormsModule, DatePipe, ImportJobComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './job-search.page.html',
})
export class JobSearchPage {
  private readonly api = inject(ApiService);
  private pollHandle: ReturnType<typeof setTimeout> | undefined;

  readonly kinds: JobSourceKind[] = ['Greenhouse', 'Lever', 'Ashby', 'Himalayas'];
  readonly tokenHelp: Record<JobSourceKind, string> = {
    Greenhouse: 'The last part of boards.greenhouse.io/<token> or job-boards.greenhouse.io/<token>.',
    Lever: 'The last part of jobs.lever.co/<site>.',
    Ashby: 'The last part of jobs.ashbyhq.com/<board>.',
    Himalayas: 'A search keyword (for example dotnet or backend-engineer), not a company. Remote jobs from many employers.',
  };

  readonly companies = signal<CompanyDto[]>([]);
  readonly runs = signal<SearchRunDto[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  readonly searching = signal(false);
  readonly runError = signal<string | null>(null);

  readonly tests = signal<Record<string, CompanyTestResult | 'running'>>({});
  readonly confirmRemove = signal<string | null>(null);
  readonly listError = signal<string | null>(null);

  readonly addError = signal<string | null>(null);
  readonly addFieldErrors = signal<Record<string, string[]>>({});
  readonly adding = signal(false);

  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    kind: new FormControl<JobSourceKind>('Greenhouse', { nonNullable: true }),
    boardToken: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$/)],
    }),
    useEuEndpoint: new FormControl(false, { nonNullable: true }),
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.pollHandle));
    this.load();
  }

  get latestRun(): SearchRunDto | null {
    return this.runs()[0] ?? null;
  }

  isActive(run: SearchRunDto | null): boolean {
    return !!run && ACTIVE.includes(run.status);
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.api.listCompanies().subscribe({
      next: (c) => {
        this.companies.set(c);
        this.api.listRuns().subscribe({
          next: (r) => {
            this.runs.set(r);
            this.loading.set(false);
            if (this.isActive(r[0] ?? null)) {
              this.searching.set(true);
              this.poll(r[0].id); // a search is already running (e.g. the page was reloaded)
            }
          },
          error: (e: unknown) => this.fail(e),
        });
      },
      error: (e: unknown) => this.fail(e),
    });
  }

  private fail(e: unknown): void {
    this.loading.set(false);
    this.loadError.set(problemMessage(e, 'Could not load your job sources.'));
  }

  // ---- running a search -----------------------------------------------------------------------

  startSearch(): void {
    this.runError.set(null);
    this.searching.set(true);
    this.api.startSearch().subscribe({
      next: (run) => {
        this.runs.update((r) => [run, ...r.filter((x) => x.id !== run.id)]);
        this.poll(run.id);
      },
      error: (e: unknown) => {
        this.searching.set(false);
        this.runError.set(problemMessage(e, 'Could not start the search.'));
      },
    });
  }

  private poll(id: string): void {
    clearTimeout(this.pollHandle);
    this.pollHandle = setTimeout(() => {
      this.api.getRun(id).subscribe({
        next: (run) => {
          this.runs.update((r) => r.map((x) => (x.id === run.id ? run : x)));
          if (this.isActive(run)) {
            this.poll(id);
          } else {
            this.searching.set(false);
            this.refreshCompanies();
          }
        },
        error: (e: unknown) => {
          this.searching.set(false);
          this.runError.set(problemMessage(e, 'Lost contact with the server while the search was running.'));
        },
      });
    }, 2000);
  }

  private refreshCompanies(): void {
    this.api.listCompanies().subscribe({ next: (c) => this.companies.set(c) });
  }

  runSummary(run: SearchRunDto): string {
    switch (run.status) {
      case 'Queued':
        return 'Waiting to start…';
      case 'Running':
        return 'Searching…';
      case 'Succeeded':
        return 'Finished.';
      case 'PartiallyFailed':
        return 'Finished, but some boards could not be read.';
      default:
        return 'The search failed.';
    }
  }

  // ---- watchlist ------------------------------------------------------------------------------

  private input(c: CompanyDto, patch: Partial<CompanyDto> = {}) {
    const m = { ...c, ...patch };
    return { name: m.name, kind: m.kind, boardToken: m.boardToken, useEuEndpoint: m.useEuEndpoint, isEnabled: m.isEnabled };
  }

  toggle(c: CompanyDto): void {
    this.listError.set(null);
    this.api.updateCompany(c.id, this.input(c, { isEnabled: !c.isEnabled })).subscribe({
      next: (updated) => this.companies.update((all) => all.map((x) => (x.id === updated.id ? updated : x))),
      error: (e: unknown) => this.listError.set(problemMessage(e, `Could not update ${c.name}.`)),
    });
  }

  test(c: CompanyDto): void {
    this.tests.update((t) => ({ ...t, [c.id]: 'running' }));
    this.api.testCompany(c.id).subscribe({
      next: (r) => this.tests.update((t) => ({ ...t, [c.id]: r })),
      error: (e: unknown) =>
        this.tests.update((t) => ({ ...t, [c.id]: { ok: false, jobCount: null, error: problemMessage(e, 'The test could not run.') } })),
    });
  }

  testResult(c: CompanyDto): CompanyTestResult | null {
    const t = this.tests()[c.id];
    return t && t !== 'running' ? t : null;
  }

  isTesting(c: CompanyDto): boolean {
    return this.tests()[c.id] === 'running';
  }

  askRemove(c: CompanyDto): void {
    this.confirmRemove.set(c.id);
  }

  remove(c: CompanyDto): void {
    this.listError.set(null);
    this.api.deleteCompany(c.id).subscribe({
      next: () => {
        this.confirmRemove.set(null);
        this.companies.update((all) => all.filter((x) => x.id !== c.id));
      },
      error: (e: unknown) => {
        this.confirmRemove.set(null);
        this.listError.set(problemMessage(e, `Could not remove ${c.name}.`));
      },
    });
  }

  add(): void {
    this.addError.set(null);
    this.addFieldErrors.set({});
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.adding.set(true);
    this.api
      .addCompany({
        name: v.name.trim(),
        kind: v.kind,
        boardToken: v.boardToken.trim(),
        useEuEndpoint: v.kind === 'Lever' && v.useEuEndpoint,
        isEnabled: true,
      })
      .subscribe({
        next: (created) => {
          this.adding.set(false);
          this.companies.update((all) => [...all, created].sort((a, b) => a.name.localeCompare(b.name)));
          this.form.reset({ name: '', kind: v.kind, boardToken: '', useEuEndpoint: false });
        },
        error: (e: unknown) => {
          this.adding.set(false);
          const errors = fieldErrors(e);
          this.addFieldErrors.set(errors);
          this.addError.set(
            Object.keys(errors).length > 0 ? 'Please fix the highlighted fields.' : problemMessage(e, 'Could not add this company.'),
          );
        },
      });
  }

  fieldError(name: string): string | null {
    const server = this.addFieldErrors()[name]?.[0];
    if (server) return server;
    const c = this.form.get(name);
    if (!c || !c.touched || !c.errors) return null;
    if (c.errors['required']) return 'This field is required.';
    if (c.errors['pattern']) return "Letters, digits, '-', '_' and '.' only.";
    if (c.errors['maxlength']) return 'Too long.';
    return null;
  }

  get selectedKind(): JobSourceKind {
    return this.form.controls.kind.value;
  }
}
