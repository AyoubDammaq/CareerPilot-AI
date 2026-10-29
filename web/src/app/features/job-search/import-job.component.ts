import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { ManualImportResult } from '../../core/models';
import { fieldErrors, problemMessage } from '../../shared/http-error';

/** Adds one job the owner found elsewhere: a public job-page address or pasted text. Nothing is applied to. */
@Component({
  selector: 'app-import-job',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card" aria-labelledby="imp-h">
      <h2 id="imp-h">Add a job you found elsewhere</h2>
      <p class="muted">
        For LinkedIn, Indeed, Keejob, Tanitjobs, Wellfound or an employer's own site. The job is scored like any other and marked as
        imported by hand: CareerPilot cannot check whether it is still open.
      </p>

      @if (error()) {
        <p class="alert alert-error" role="alert">{{ error() }}</p>
      }
      @if (result(); as r) {
        <div class="alert alert-ok" role="status">
          <p>
            {{ r.created ? 'Added' : 'Updated' }}: <strong>{{ r.title }}</strong> at {{ r.company }}.
            <a routerLink="/opportunities">Open Job Opportunities</a>
          </p>
          <ul>
            @for (w of r.warnings; track w) { <li>{{ w }}</li> }
          </ul>
        </div>
      }

      <form [formGroup]="urlForm" (ngSubmit)="importUrl()" novalidate>
        <div class="field">
          <label for="imp-url">Address of a public job page (https)</label>
          <input id="imp-url" type="url" formControlName="url" autocapitalize="none" spellcheck="false" aria-describedby="imp-url-help" />
          <span class="muted small" id="imp-url-help">
            Works only for pages that publish structured job data. LinkedIn, Indeed, Wellfound, Keejob and Tanitjobs pages are never
            fetched by CareerPilot: paste their text below.
          </span>
        </div>
        <div class="row">
          <button type="submit" class="btn btn-primary" [disabled]="busy()">{{ busy() ? 'Importing…' : 'Import from address' }}</button>
        </div>
      </form>

      <h3>Paste the job text</h3>
      <form [formGroup]="textForm" (ngSubmit)="importText()" novalidate>
        <div class="grid">
          <div class="field">
            <label for="imp-title">Job title</label>
            <input id="imp-title" type="text" formControlName="title" [attr.aria-invalid]="fe('title') ? true : null" />
            @if (fe('title'); as e) { <p class="field-error">{{ e }}</p> }
          </div>
          <div class="field">
            <label for="imp-company">Company</label>
            <input id="imp-company" type="text" formControlName="company" [attr.aria-invalid]="fe('company') ? true : null" />
            @if (fe('company'); as e) { <p class="field-error">{{ e }}</p> }
          </div>
          <div class="field">
            <label for="imp-location">Location (optional)</label>
            <input id="imp-location" type="text" formControlName="location" />
          </div>
          <div class="field">
            <label for="imp-link">Link to the original ad (optional)</label>
            <input id="imp-link" type="url" formControlName="url" autocapitalize="none" spellcheck="false" [attr.aria-invalid]="fe('url') ? true : null" />
            @if (fe('url'); as e) { <p class="field-error">{{ e }}</p> }
          </div>
        </div>
        <div class="field">
          <label for="imp-desc">Job description</label>
          <textarea id="imp-desc" rows="8" formControlName="description" [attr.aria-invalid]="fe('description') ? true : null"></textarea>
          @if (fe('description'); as e) { <p class="field-error">{{ e }}</p> }
        </div>
        <div class="row">
          <button type="submit" class="btn btn-primary" [disabled]="busy()">{{ busy() ? 'Importing…' : 'Import pasted job' }}</button>
        </div>
      </form>
    </section>
  `,
})
export class ImportJobComponent {
  private readonly api = inject(ApiService);

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<ManualImportResult | null>(null);
  readonly errors = signal<Record<string, string[]>>({});

  readonly urlForm = new FormGroup({
    url: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  readonly textForm = new FormGroup({
    title: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    company: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    location: new FormControl('', { nonNullable: true }),
    url: new FormControl('', { nonNullable: true }),
    description: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  fe(field: string): string | null {
    return this.errors()[field]?.[0] ?? null;
  }

  importUrl(): void {
    if (this.busy()) return;
    this.reset();
    if (this.urlForm.invalid) {
      this.errors.set({ url: ['Enter the address of the job page.'] });
      this.error.set('Enter the address of the job page.');
      return;
    }
    this.busy.set(true);
    this.api.importJobFromUrl(this.urlForm.getRawValue().url).subscribe({
      next: (r) => this.done(r, this.urlForm),
      error: (e: unknown) => this.failed(e),
    });
  }

  importText(): void {
    if (this.busy()) return;
    this.reset();
    if (this.textForm.invalid) {
      this.textForm.markAllAsTouched();
      this.error.set('Title, company and the job description are required.');
      return;
    }
    this.busy.set(true);
    this.api.importJobFromText(this.textForm.getRawValue()).subscribe({
      next: (r) => this.done(r, this.textForm),
      error: (e: unknown) => this.failed(e),
    });
  }

  private reset(): void {
    this.error.set(null);
    this.result.set(null);
    this.errors.set({});
  }

  private done(r: ManualImportResult, form: FormGroup): void {
    this.busy.set(false);
    this.result.set(r);
    form.reset();
  }

  private failed(e: unknown): void {
    this.busy.set(false);
    const fields = fieldErrors(e);
    this.errors.set(fields);
    const first = Object.values(fields)[0]?.[0];
    this.error.set(first ?? problemMessage(e, 'Could not import this job.'));
  }
}
