import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { LoginPage } from './login.page';

describe('LoginPage', () => {
  // A successful sign-in is remembered for the tab; start every test signed out.
  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  function create() {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    const ctrl = TestBed.inject(HttpTestingController);
    const f = TestBed.createComponent(LoginPage);
    f.detectChanges();
    ctrl.expectOne('/health/live').flush({});
    return { f, ctrl, el: f.nativeElement as HTMLElement };
  }

  it('signs in when the form is submitted, and stops the browser submitting the page itself', () => {
    const { f, ctrl, el } = create();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const input = el.querySelector<HTMLInputElement>('#password')!;
    input.value = 'a-long-enough-password';
    input.dispatchEvent(new Event('input'));

    // Without preventDefault a real browser reloads the page to /login? and nothing happens.
    const submit = new Event('submit', { cancelable: true });
    el.querySelector('form')!.dispatchEvent(submit);
    expect(submit.defaultPrevented).toBe(true);

    const req = ctrl.expectOne('/api/auth/login');
    expect(req.request.body).toEqual({ password: 'a-long-enough-password' });
    req.flush({ token: 't', expiresAt: '2030-01-01T00:00:00Z' });
    expect(navigate).toHaveBeenCalledWith('/dashboard');
    f.destroy();
  });

  it('asks for a password instead of sending an empty one', () => {
    const { f, ctrl, el } = create();
    el.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    f.detectChanges();
    ctrl.expectNone('/api/auth/login');
    expect(el.textContent).toContain('Enter your password.');
  });

  it('says plainly when the password is wrong and when attempts are limited', () => {
    const { f, ctrl, el } = create();
    const input = el.querySelector<HTMLInputElement>('#password')!;
    input.value = 'wrong-password-1';
    input.dispatchEvent(new Event('input'));
    el.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    ctrl.expectOne('/api/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });
    f.detectChanges();
    expect(el.textContent).toContain('Incorrect password.');

    input.value = 'wrong-password-2';
    input.dispatchEvent(new Event('input'));
    el.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    ctrl.expectOne('/api/auth/login').flush({}, { status: 429, statusText: 'Too Many Requests' });
    f.detectChanges();
    expect(el.textContent).toContain('Too many attempts');
  });
});
