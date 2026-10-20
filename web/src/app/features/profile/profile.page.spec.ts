import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { sampleProfile } from '../../testing/fixtures';
import { ProfilePage } from './profile.page';

describe('ProfilePage', () => {
  let ctrl: HttpTestingController;
  let fixture: ComponentFixture<ProfilePage>;
  let el: HTMLElement;

  async function settle() {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ProfilePage);
    el = fixture.nativeElement;
    fixture.detectChanges();
    ctrl.expectOne('/api/profile').flush(sampleProfile());
    await settle();
  });

  const input = (id: string) => el.querySelector<HTMLInputElement>('#' + id)!;
  const type = (id: string, value: string) => {
    const i = input(id);
    i.value = value;
    i.dispatchEvent(new Event('input'));
  };
  const saveBtn = () => el.querySelector<HTMLButtonElement>('.save-bar button')!;

  it('loads the profile into the form', () => {
    expect(input('fullName').value).toBe('Ada Lovelace');
    expect(input('exp0-company').value).toBe('Analytical Engines');
    expect(input('exp0-ach1').value).toBe('Second algorithm');
    expect(el.querySelectorAll('.chip').length).toBeGreaterThan(0);
    expect(saveBtn().disabled).toBe(true);
    expect(el.textContent).toContain('I confirm this is true');
  });

  it('tracks dirty state and enables Save', async () => {
    type('fullName', 'Ada B');
    await settle();
    expect(fixture.componentInstance.dirty()).toBe(true);
    expect(saveBtn().disabled).toBe(false);
    expect(el.querySelector('.dirty-indicator')!.textContent).toContain('Unsaved');
  });

  it('adds and removes items through the UI', async () => {
    const addSkill = Array.from(el.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent!.trim() === 'Add skill')!;
    addSkill.click();
    await settle();
    expect(input('skill1-name')).toBeTruthy();
    expect(fixture.componentInstance.dirty()).toBe(true);

    el.querySelector<HTMLButtonElement>('button[aria-label="Remove skill 2"]')!.click();
    await settle();
    expect(input('skill1-name')).toBeNull();
    expect(input('skill0-name')).toBeTruthy();
  });

  it('PUTs the DTO with the loaded version and resets dirty on success', async () => {
    type('fullName', 'Ada B');
    await settle();
    saveBtn().click();
    const req = ctrl.expectOne('/api/profile');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.version).toBe(7);
    expect(req.request.body.fullName).toBe('Ada B');
    req.flush(sampleProfile({ fullName: 'Ada B', version: 8 }));
    await settle();
    expect(fixture.componentInstance.dirty()).toBe(false);
    expect(el.querySelector('.alert-ok')!.textContent).toContain('Profile saved');
    expect(fixture.componentInstance.form.controls.version.value).toBe(8);
  });

  it('shows server validation errors next to the right fields', async () => {
    type('fullName', 'Ada B');
    await settle();
    saveBtn().click();
    ctrl.expectOne('/api/profile').flush(
      { title: 'Validation', errors: { email: ['Email taken'], 'workExperiences[0].endDate': ['End before start'], 'preferences.minSalary': ['Must be positive'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await settle();
    const textOf = (id: string) => el.querySelector('#' + id)!.textContent;
    expect(textOf('email-err')).toContain('Email taken');
    expect(textOf('exp0-end-err')).toContain('End before start');
    expect(textOf('pref-min-err')).toContain('Must be positive');
    expect(input('email').getAttribute('aria-invalid')).toBe('true');
    expect(el.querySelector('.alert-error')!.textContent).toContain('rejected');
    // editing clears the message
    type('email', 'new@example.com');
    await settle();
    expect(textOf('email-err')).not.toContain('Email taken');
  });

  it('handles 409 with a reload option that refetches and discards edits', async () => {
    type('fullName', 'Mine');
    await settle();
    saveBtn().click();
    ctrl.expectOne('/api/profile').flush({ title: 'Conflict' }, { status: 409, statusText: 'Conflict' });
    await settle();
    expect(el.textContent).toContain('changed elsewhere');
    const reload = Array.from(el.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent!.includes('Reload latest'))!;
    reload.click();
    const req: TestRequest = ctrl.expectOne('/api/profile');
    expect(req.request.method).toBe('GET');
    req.flush(sampleProfile({ fullName: 'Theirs', version: 9 }));
    await settle();
    expect(input('fullName').value).toBe('Theirs');
    expect(fixture.componentInstance.dirty()).toBe(false);
  });

  it('does not send an invalid form (missing required field)', async () => {
    type('fullName', '');
    await settle();
    saveBtn().click();
    ctrl.expectNone('/api/profile');
    await settle();
    expect(el.querySelector('[role="alert"], .alert-error')).toBeTruthy();
    expect(el.querySelector('#fullName-err')!.textContent).toContain('required');
  });

  it('canDeactivate prompts only when dirty and signed in', async () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    vi.spyOn(TestBed.inject(AuthService), 'isAuthenticated').mockReturnValue(true);
    const page = fixture.componentInstance;
    expect(page.canDeactivate()).toBe(true);
    expect(confirm).not.toHaveBeenCalled();

    type('fullName', 'X');
    await settle();
    expect(page.canDeactivate()).toBe(false);
    expect(confirm).toHaveBeenCalledTimes(1);
    confirm.mockReturnValue(true);
    expect(page.canDeactivate()).toBe(true);
    confirm.mockRestore();
  });

  it('skips the prompt when the session is gone (forced logout)', async () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    type('fullName', 'X');
    await settle();
    expect(fixture.componentInstance.canDeactivate()).toBe(true);
    expect(confirm).not.toHaveBeenCalled();
    confirm.mockRestore();
  });
});
