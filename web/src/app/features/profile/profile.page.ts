import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, ElementRef, HostListener, inject, signal, viewChild } from '@angular/core';
import { AbstractControl, FormControl, ReactiveFormsModule } from '@angular/forms';

import { ApiService } from '../../core/api/api.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  EMPLOYMENT_TYPES,
  EmploymentType,
  SKILL_LEVELS,
  ValidationProblem,
  WORK_MODES,
  WorkMode,
} from '../../core/models';
import { ChipInput } from '../../shared/chip-input';
import { FieldError } from '../../shared/field-error';
import {
  ServerFieldError,
  applyServerErrors,
  createEducationGroup,
  createExperienceGroup,
  createLanguageGroup,
  createProfileForm,
  createProjectGroup,
  createSkillGroup,
  formToDto,
  patchFormFromDto,
} from './profile-form';

const WORK_MODE_LABELS: Record<WorkMode, string> = { Remote: 'Remote', Hybrid: 'Hybrid', OnSite: 'On-site' };
const EMPLOYMENT_LABELS: Record<EmploymentType, string> = {
  Permanent: 'Permanent',
  FixedTerm: 'Fixed-term',
  Contract: 'Contract',
  Internship: 'Internship',
  PartTime: 'Part-time',
};

@Component({
  selector: 'app-profile-page',
  imports: [ReactiveFormsModule, ChipInput, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profile.page.html',
})
export class ProfilePage {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);

  readonly form = createProfileForm();
  readonly skillLevels = SKILL_LEVELS;
  readonly workModes = WORK_MODES;
  readonly employmentTypes = EMPLOYMENT_TYPES;
  readonly languageLevels = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2', 'Beginner', 'Intermediate', 'Advanced', 'Fluent', 'Native'];

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly dirty = signal(false);
  readonly saveMessage = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);
  readonly conflict = signal(false);
  readonly serverIssues = signal<ServerFieldError[]>([]);

  private readonly summary = viewChild<ElementRef<HTMLElement>>('statusPanel');

  constructor() {
    this.form.events.subscribe(() => {
      if (this.dirty() !== this.form.dirty) this.dirty.set(this.form.dirty);
    });
    this.load();
  }

  modeLabel = (m: WorkMode) => WORK_MODE_LABELS[m];
  employmentLabel = (t: EmploymentType) => EMPLOYMENT_LABELS[t];

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.api.getProfile().subscribe({
      next: (p) => {
        patchFormFromDto(this.form, p);
        this.resetMessages();
        this.dirty.set(false);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.loading.set(false);
        this.loadError.set(this.describe(e, 'Could not load your profile.'));
      },
    });
  }

  /** Discards local edits and reloads the stored profile (used after a 409). */
  reloadFromServer(): void {
    this.conflict.set(false);
    this.load();
  }

  save(): void {
    this.resetMessages();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.saveError.set('Some fields need attention before saving. They are marked below.');
      this.focusStatus();
      return;
    }
    this.saving.set(true);
    this.api.saveProfile(formToDto(this.form)).subscribe({
      next: (p) => {
        patchFormFromDto(this.form, p);
        this.dirty.set(false);
        this.saving.set(false);
        this.saveMessage.set('Profile saved.');
      },
      error: (e: unknown) => {
        this.saving.set(false);
        this.handleSaveError(e);
        this.focusStatus();
      },
    });
  }

  private handleSaveError(e: unknown): void {
    if (!(e instanceof HttpErrorResponse)) {
      this.saveError.set('Could not save your profile.');
      return;
    }
    if (e.status === 409) {
      this.conflict.set(true);
    } else if (e.status === 400) {
      const body = (e.error ?? {}) as ValidationProblem;
      const issues = applyServerErrors(this.form, body.errors);
      this.serverIssues.set(issues);
      this.saveError.set(
        issues.length > 0
          ? 'The server rejected some fields. They are marked below.'
          : (body.title ?? 'The server rejected the profile.'),
      );
    } else if (e.status === 401) {
      // The interceptor is already redirecting to /login.
      this.saveError.set('Your session expired. Sign in again; your edits on this page were not saved.');
    } else if (e.status === 0) {
      this.saveError.set('Cannot reach the server. Your edits are still here; try again.');
    } else {
      this.saveError.set(this.describe(e, 'Could not save your profile.'));
    }
  }

  private describe(e: unknown, fallback: string): string {
    if (e instanceof HttpErrorResponse) {
      if (e.status === 0) return 'Cannot reach the server.';
      const t = (e.error as { title?: string } | null)?.title;
      if (t) return t;
    }
    return fallback;
  }

  private resetMessages(): void {
    this.saveMessage.set(null);
    this.saveError.set(null);
    this.conflict.set(false);
    this.serverIssues.set([]);
  }

  private focusStatus(): void {
    queueMicrotask(() => this.summary()?.nativeElement.focus());
  }

  // ---- list editing -------------------------------------------------------
  private touchStructure(): void {
    this.form.markAsDirty();
    this.dirty.set(true);
  }

  addExperience(): void {
    this.form.controls.workExperiences.push(createExperienceGroup());
    this.touchStructure();
  }
  removeExperience(i: number): void {
    this.form.controls.workExperiences.removeAt(i);
    this.touchStructure();
  }
  addEducation(): void {
    this.form.controls.education.push(createEducationGroup());
    this.touchStructure();
  }
  removeEducation(i: number): void {
    this.form.controls.education.removeAt(i);
    this.touchStructure();
  }
  addSkill(): void {
    this.form.controls.skills.push(createSkillGroup());
    this.touchStructure();
  }
  removeSkill(i: number): void {
    this.form.controls.skills.removeAt(i);
    this.touchStructure();
  }
  addProject(): void {
    this.form.controls.projects.push(createProjectGroup());
    this.touchStructure();
  }
  removeProject(i: number): void {
    this.form.controls.projects.removeAt(i);
    this.touchStructure();
  }
  addLanguage(): void {
    this.form.controls.languages.push(createLanguageGroup());
    this.touchStructure();
  }
  removeLanguage(i: number): void {
    this.form.controls.languages.removeAt(i);
    this.touchStructure();
  }

  addLine(arr: { push(c: FormControl<string>): void }): void {
    arr.push(new FormControl('', { nonNullable: true }));
    this.touchStructure();
  }
  removeLine(arr: { removeAt(i: number): void }, i: number): void {
    arr.removeAt(i);
    this.touchStructure();
  }

  has<T>(c: FormControl<T[]>, v: T): boolean {
    return c.value.includes(v);
  }
  toggle<T>(c: FormControl<T[]>, v: T, checked: boolean): void {
    const next = c.value.filter((x) => x !== v);
    if (checked) next.push(v);
    c.setValue(next);
    c.markAsDirty();
  }

  /** Marks a control as visibly invalid (server error, or client error after interaction). */
  invalid(c: AbstractControl): boolean {
    return c.invalid && (c.touched || c.dirty || !!c.errors?.['server']);
  }

  // ---- leaving the page ---------------------------------------------------
  canDeactivate(): boolean {
    if (!this.dirty() || !this.auth.isAuthenticated()) return true;
    return window.confirm('You have unsaved changes to your profile. Leave this page and discard them?');
  }

  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(e: BeforeUnloadEvent): void {
    if (this.dirty()) {
      e.preventDefault();
      e.returnValue = '';
    }
  }
}

