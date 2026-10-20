import { AbstractControl, FormArray, FormControl, FormGroup, Validators } from '@angular/forms';
import {
  EducationDto,
  EmploymentType,
  LanguageDto,
  PreferencesDto,
  ProfileDto,
  ProjectDto,
  SkillDto,
  SkillLevel,
  WorkExperienceDto,
  WorkMode,
} from '../../core/models';

const str = () => new FormControl('', { nonNullable: true });
const reqStr = () => new FormControl('', { nonNullable: true, validators: [Validators.required] });
const bool = (v = false) => new FormControl(v, { nonNullable: true });
const list = <T extends string = string>() => new FormControl<T[]>([], { nonNullable: true });

export function createExperienceGroup() {
  return new FormGroup({
    id: new FormControl<string | null>(null),
    company: reqStr(),
    title: reqStr(),
    location: str(),
    startDate: reqStr(),
    endDate: str(),
    description: str(),
    achievements: new FormArray<FormControl<string>>([]),
    isVerified: bool(),
  });
}

export function createEducationGroup() {
  return new FormGroup({
    id: new FormControl<string | null>(null),
    institution: reqStr(),
    degree: str(),
    field: str(),
    startDate: str(),
    endDate: str(),
  });
}

export function createSkillGroup() {
  return new FormGroup({
    id: new FormControl<string | null>(null),
    name: reqStr(),
    category: str(),
    level: new FormControl<SkillLevel | ''>('', { nonNullable: true }),
    isVerified: bool(),
  });
}

export function createProjectGroup() {
  return new FormGroup({
    id: new FormControl<string | null>(null),
    name: reqStr(),
    description: str(),
    url: str(),
    technologies: list(),
    results: new FormArray<FormControl<string>>([]),
    isVerified: bool(),
  });
}

export function createLanguageGroup() {
  return new FormGroup({
    id: new FormControl<string | null>(null),
    name: reqStr(),
    level: str(),
  });
}

export function createPreferencesGroup() {
  return new FormGroup({
    targetCountries: list(),
    targetCities: list(),
    workModes: list<WorkMode>(),
    willingToRelocate: bool(),
    requiresVisaSponsorship: bool(),
    includeInternationalRemote: bool(),
    employmentTypes: list<EmploymentType>(),
    targetTitles: list(),
    workingLanguages: list(),
    includeSeniorRoles: bool(),
    experienceLevel: str(),
    minSalary: new FormControl<number | null>(null),
    preferredSalary: new FormControl<number | null>(null),
    salaryCurrency: str(),
  });
}

export type ExperienceGroup = ReturnType<typeof createExperienceGroup>;
export type EducationGroup = ReturnType<typeof createEducationGroup>;
export type SkillGroup = ReturnType<typeof createSkillGroup>;
export type ProjectGroup = ReturnType<typeof createProjectGroup>;
export type LanguageGroup = ReturnType<typeof createLanguageGroup>;

export function createProfileForm() {
  return new FormGroup({
    version: new FormControl(0, { nonNullable: true }),
    fullName: reqStr(),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    phone: str(),
    headline: str(),
    summary: str(),
    country: str(),
    city: str(),
    githubUrl: str(),
    linkedinUrl: str(),
    websiteUrl: str(),
    workExperiences: new FormArray<ExperienceGroup>([]),
    education: new FormArray<EducationGroup>([]),
    skills: new FormArray<SkillGroup>([]),
    projects: new FormArray<ProjectGroup>([]),
    languages: new FormArray<LanguageGroup>([]),
    preferences: createPreferencesGroup(),
  });
}

export type ProfileForm = ReturnType<typeof createProfileForm>;

const strArray = (values: string[]) => new FormArray(values.map((v) => new FormControl(v, { nonNullable: true })));

/** Replaces the whole form content with the DTO and marks it pristine. */
export function patchFormFromDto(form: ProfileForm, dto: ProfileDto): void {
  const fill = <T extends AbstractControl>(arr: FormArray<T>, items: T[]) => {
    arr.clear({ emitEvent: false });
    items.forEach((i) => arr.push(i, { emitEvent: false }));
  };

  fill(
    form.controls.workExperiences,
    dto.workExperiences.map((w) => {
      const g = createExperienceGroup();
      g.patchValue({
        id: w.id,
        company: w.company,
        title: w.title,
        location: w.location ?? '',
        startDate: w.startDate ?? '',
        endDate: w.endDate ?? '',
        description: w.description ?? '',
        isVerified: w.isVerified,
      });
      (w.achievements ?? []).forEach((a) => g.controls.achievements.push(new FormControl(a, { nonNullable: true })));
      return g;
    }),
  );
  fill(
    form.controls.education,
    dto.education.map((e) => {
      const g = createEducationGroup();
      g.patchValue({
        id: e.id,
        institution: e.institution,
        degree: e.degree ?? '',
        field: e.field ?? '',
        startDate: e.startDate ?? '',
        endDate: e.endDate ?? '',
      });
      return g;
    }),
  );
  fill(
    form.controls.skills,
    dto.skills.map((s) => {
      const g = createSkillGroup();
      g.patchValue({ id: s.id, name: s.name, category: s.category ?? '', level: s.level ?? '', isVerified: s.isVerified });
      return g;
    }),
  );
  fill(
    form.controls.projects,
    dto.projects.map((p) => {
      const g = createProjectGroup();
      g.patchValue({
        id: p.id,
        name: p.name,
        description: p.description ?? '',
        url: p.url ?? '',
        technologies: [...(p.technologies ?? [])],
        isVerified: p.isVerified,
      });
      (p.results ?? []).forEach((r) => g.controls.results.push(new FormControl(r, { nonNullable: true })));
      return g;
    }),
  );
  fill(
    form.controls.languages,
    dto.languages.map((l) => {
      const g = createLanguageGroup();
      g.patchValue({ id: l.id, name: l.name, level: l.level ?? '' });
      return g;
    }),
  );

  const p = dto.preferences;
  form.controls.preferences.patchValue({
    ...p,
    targetCountries: [...p.targetCountries],
    targetCities: [...p.targetCities],
    workModes: [...p.workModes],
    employmentTypes: [...p.employmentTypes],
    targetTitles: [...p.targetTitles],
    workingLanguages: [...p.workingLanguages],
    experienceLevel: p.experienceLevel ?? '',
    salaryCurrency: p.salaryCurrency ?? '',
  });

  form.patchValue({
    version: dto.version,
    fullName: dto.fullName,
    email: dto.email,
    phone: dto.phone ?? '',
    headline: dto.headline ?? '',
    summary: dto.summary ?? '',
    country: dto.country ?? '',
    city: dto.city ?? '',
    githubUrl: dto.githubUrl ?? '',
    linkedinUrl: dto.linkedinUrl ?? '',
    websiteUrl: dto.websiteUrl ?? '',
  });
  form.markAsPristine();
  form.markAsUntouched();
  form.updateValueAndValidity();
}

const trim = (s: string | null | undefined) => (s ?? '').trim();
const orNull = (s: string | null | undefined) => {
  const t = trim(s);
  return t === '' ? null : t;
};
const nonBlank = (xs: string[]) => xs.map(trim).filter((x) => x !== '');

/** Maps the form to the API contract. Blank optional text becomes null; blank list lines are dropped. */
export function formToDto(form: ProfileForm): ProfileDto {
  const v = form.getRawValue();
  const p = v.preferences;
  const preferences: PreferencesDto = {
    targetCountries: nonBlank(p.targetCountries),
    targetCities: nonBlank(p.targetCities),
    workModes: [...p.workModes],
    willingToRelocate: p.willingToRelocate,
    requiresVisaSponsorship: p.requiresVisaSponsorship,
    includeInternationalRemote: p.includeInternationalRemote,
    employmentTypes: [...p.employmentTypes],
    targetTitles: nonBlank(p.targetTitles),
    workingLanguages: nonBlank(p.workingLanguages),
    includeSeniorRoles: p.includeSeniorRoles,
    experienceLevel: trim(p.experienceLevel),
    minSalary: p.minSalary ?? null,
    preferredSalary: p.preferredSalary ?? null,
    salaryCurrency: trim(p.salaryCurrency),
  };
  const workExperiences: WorkExperienceDto[] = v.workExperiences.map((w) => ({
    id: w.id,
    company: trim(w.company),
    title: trim(w.title),
    location: trim(w.location),
    startDate: w.startDate,
    endDate: orNull(w.endDate),
    description: trim(w.description),
    achievements: nonBlank(w.achievements),
    isVerified: w.isVerified,
  }));
  const education: EducationDto[] = v.education.map((e) => ({
    id: e.id,
    institution: trim(e.institution),
    degree: trim(e.degree),
    field: trim(e.field),
    startDate: orNull(e.startDate),
    endDate: orNull(e.endDate),
  }));
  const skills: SkillDto[] = v.skills.map((s) => ({
    id: s.id,
    name: trim(s.name),
    category: trim(s.category),
    level: s.level === '' ? null : s.level,
    isVerified: s.isVerified,
  }));
  const projects: ProjectDto[] = v.projects.map((pr) => ({
    id: pr.id,
    name: trim(pr.name),
    description: trim(pr.description),
    url: orNull(pr.url),
    technologies: nonBlank(pr.technologies),
    results: nonBlank(pr.results),
    isVerified: pr.isVerified,
  }));
  const languages: LanguageDto[] = v.languages.map((l) => ({ id: l.id, name: trim(l.name), level: trim(l.level) }));

  return {
    fullName: trim(v.fullName),
    email: trim(v.email),
    phone: orNull(v.phone),
    headline: trim(v.headline),
    summary: trim(v.summary),
    country: trim(v.country),
    city: trim(v.city),
    githubUrl: orNull(v.githubUrl),
    linkedinUrl: orNull(v.linkedinUrl),
    websiteUrl: orNull(v.websiteUrl),
    version: v.version,
    workExperiences,
    education,
    skills,
    projects,
    languages,
    preferences,
  };
}

export interface ServerFieldError {
  path: string;
  messages: string[];
  /** true when the path could not be matched to a visible control */
  unmapped: boolean;
}

/** "workExperiences[0].endDate" -> ["workExperiences","0","endDate"]; also lower-cases the first letter of names. */
export function splitErrorPath(path: string): string[] {
  const segs: string[] = [];
  for (const part of path.split('.')) {
    const m = /^([^[\]]*)((?:\[\d+\])*)$/.exec(part);
    if (!m) {
      segs.push(part);
      continue;
    }
    if (m[1]) segs.push(m[1].charAt(0).toLowerCase() + m[1].slice(1));
    for (const idx of m[2].matchAll(/\[(\d+)\]/g)) segs.push(idx[1]);
  }
  return segs.filter((s) => s !== '');
}

/**
 * Attaches server messages to the matching controls (as a `server` error)
 * and returns every error with a flag for those that had no control.
 * The `server` error clears itself as soon as the user edits the field.
 */
export function applyServerErrors(form: ProfileForm, errors: Record<string, string[]> | undefined): ServerFieldError[] {
  const result: ServerFieldError[] = [];
  for (const [path, raw] of Object.entries(errors ?? {})) {
    const messages = Array.isArray(raw) ? raw : [String(raw)];
    const segs = splitErrorPath(path);
    // Longest prefix that resolves to a leaf control (e.g. "preferences.targetCities[1]" -> the chip list).
    let target: AbstractControl | null = null;
    for (let n = segs.length; n > 0 && !target; n--) {
      const c = form.get(segs.slice(0, n));
      if (c instanceof FormControl) target = c;
    }
    if (target) {
      const existing = (target.errors?.['server'] as string[] | undefined) ?? [];
      target.setErrors({ ...(target.errors ?? {}), server: [...existing, ...messages] });
      target.markAsTouched();
      result.push({ path, messages, unmapped: false });
    } else {
      result.push({ path, messages, unmapped: true });
    }
  }
  return result;
}
