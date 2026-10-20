import { FormControl } from '@angular/forms';
import { sampleProfile } from '../../testing/fixtures';
import {
  applyServerErrors,
  createEducationGroup,
  createExperienceGroup,
  createLanguageGroup,
  createProfileForm,
  createProjectGroup,
  createSkillGroup,
  formToDto,
  patchFormFromDto,
  splitErrorPath,
} from './profile-form';

describe('profile form', () => {
  it('round-trips a DTO unchanged (including version and ids)', () => {
    const form = createProfileForm();
    const dto = sampleProfile();
    patchFormFromDto(form, dto);
    expect(form.pristine).toBe(true);
    expect(formToDto(form)).toEqual(dto);
  });

  it('sends the international-remote preference the user chose', () => {
    const form = createProfileForm();
    patchFormFromDto(form, sampleProfile());
    expect(formToDto(form).preferences.includeInternationalRemote).toBe(true);

    form.controls.preferences.controls.includeInternationalRemote.setValue(false);
    expect(formToDto(form).preferences.includeInternationalRemote).toBe(false);
  });

  it('maps blank optional text to null and blank dates to null', () => {
    const form = createProfileForm();
    patchFormFromDto(form, sampleProfile());
    form.controls.phone.setValue('   ');
    form.controls.workExperiences.at(0).controls.endDate.setValue('');
    form.controls.education.at(0).controls.endDate.setValue('');
    form.controls.projects.at(0).controls.url.setValue('');
    form.controls.skills.at(0).controls.level.setValue('');
    const dto = formToDto(form);
    expect(dto.phone).toBeNull();
    expect(dto.workExperiences[0].endDate).toBeNull();
    expect(dto.education[0].endDate).toBeNull();
    expect(dto.projects[0].url).toBeNull();
    expect(dto.skills[0].level).toBeNull();
  });

  it('adds new items with id null and drops blank list lines', () => {
    const form = createProfileForm();
    patchFormFromDto(form, sampleProfile());
    const exp = createExperienceGroup();
    exp.patchValue({ company: ' Acme ', title: 'Dev', startDate: '2024-02-01' });
    form.controls.workExperiences.push(exp);
    exp.controls.achievements.push(new FormControl('Shipped', { nonNullable: true }));
    exp.controls.achievements.push(new FormControl('  ', { nonNullable: true }));

    const edu = createEducationGroup();
    edu.patchValue({ institution: 'Uni' });
    form.controls.education.push(edu);
    const skill = createSkillGroup();
    skill.patchValue({ name: 'Go', level: 'Advanced' });
    form.controls.skills.push(skill);
    const proj = createProjectGroup();
    proj.patchValue({ name: 'P', technologies: ['A', ' ', 'B'] });
    form.controls.projects.push(proj);
    const lang = createLanguageGroup();
    lang.patchValue({ name: 'French', level: 'B2' });
    form.controls.languages.push(lang);

    const dto = formToDto(form);
    expect(dto.workExperiences).toHaveLength(2);
    expect(dto.workExperiences[0].id).toBe('w1');
    expect(dto.workExperiences[1]).toMatchObject({ id: null, company: 'Acme', achievements: ['Shipped'], endDate: null, isVerified: false });
    expect(dto.education[1]).toMatchObject({ id: null, institution: 'Uni', startDate: null });
    expect(dto.skills[1]).toMatchObject({ id: null, name: 'Go', level: 'Advanced' });
    expect(dto.projects[1].technologies).toEqual(['A', 'B']);
    expect(dto.languages[1]).toEqual({ id: null, name: 'French', level: 'B2' });
  });

  it('removes items', () => {
    const form = createProfileForm();
    patchFormFromDto(form, sampleProfile());
    form.controls.workExperiences.removeAt(0);
    form.controls.skills.removeAt(0);
    const dto = formToDto(form);
    expect(dto.workExperiences).toEqual([]);
    expect(dto.skills).toEqual([]);
  });

  it('requires name and email', () => {
    const form = createProfileForm();
    expect(form.invalid).toBe(true);
    form.patchValue({ fullName: 'A', email: 'not-an-email' });
    expect(form.controls.email.errors?.['email']).toBeTruthy();
    form.patchValue({ email: 'a@b.co' });
    expect(form.valid).toBe(true);
  });

  it('keeps the loaded version, so a stale save is detectable by the server', () => {
    const form = createProfileForm();
    patchFormFromDto(form, sampleProfile({ version: 42 }));
    form.controls.fullName.setValue('Changed');
    expect(formToDto(form).version).toBe(42);
  });

  describe('server errors', () => {
    it('splits paths', () => {
      expect(splitErrorPath('workExperiences[0].endDate')).toEqual(['workExperiences', '0', 'endDate']);
      expect(splitErrorPath('preferences.minSalary')).toEqual(['preferences', 'minSalary']);
      expect(splitErrorPath('Email')).toEqual(['email']);
      expect(splitErrorPath('workExperiences[1].achievements[2]')).toEqual(['workExperiences', '1', 'achievements', '2']);
    });

    it('attaches messages to the right controls', () => {
      const form = createProfileForm();
      patchFormFromDto(form, sampleProfile());
      const issues = applyServerErrors(form, {
        email: ['Email is invalid'],
        summary: ['Too long', 'Really too long'],
        'workExperiences[0].endDate': ['End must be after start'],
        'preferences.minSalary': ['Must be positive'],
        'preferences.targetCities[0]': ['Bad city'],
        'workExperiences[0].achievements[1]': ['Too long'],
      });
      expect(issues.every((i) => !i.unmapped)).toBe(true);
      expect(form.controls.email.errors?.['server']).toEqual(['Email is invalid']);
      expect(form.controls.summary.errors?.['server']).toEqual(['Too long', 'Really too long']);
      expect(form.controls.workExperiences.at(0).controls.endDate.errors?.['server']).toEqual(['End must be after start']);
      expect(form.controls.preferences.controls.minSalary.errors?.['server']).toEqual(['Must be positive']);
      expect(form.controls.preferences.controls.targetCities.errors?.['server']).toEqual(['Bad city']);
      expect(form.controls.workExperiences.at(0).controls.achievements.at(1).errors?.['server']).toEqual(['Too long']);
      expect(form.controls.workExperiences.at(0).controls.endDate.touched).toBe(true);
    });

    it('reports unknown paths as unmapped', () => {
      const form = createProfileForm();
      const issues = applyServerErrors(form, { nothing: ['x'], 'skills[5].name': ['y'], '': ['z'] });
      expect(issues.map((i) => i.unmapped)).toEqual([true, true, true]);
    });

    it('clears a server error when the user edits the field', () => {
      const form = createProfileForm();
      patchFormFromDto(form, sampleProfile());
      applyServerErrors(form, { summary: ['Too long'] });
      expect(form.controls.summary.invalid).toBe(true);
      form.controls.summary.setValue('Shorter');
      expect(form.controls.summary.errors).toBeNull();
    });

    it('handles an undefined errors map', () => {
      expect(applyServerErrors(createProfileForm(), undefined)).toEqual([]);
    });
  });
});
