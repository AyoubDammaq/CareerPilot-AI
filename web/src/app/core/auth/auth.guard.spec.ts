import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { authGuard } from './auth.guard';
import { AUTH_STORAGE_KEY } from './auth.service';

function run(url: string) {
  return TestBed.runInInjectionContext(() =>
    authGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
  );
}

describe('authGuard', () => {
  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
  });

  it('allows an authenticated user', () => {
    sessionStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify({ token: 't', expiresAt: new Date(Date.now() + 3600_000).toISOString() }));
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    expect(run('/profile')).toBe(true);
  });

  it('redirects an anonymous user to /login keeping the return url', () => {
    const r = run('/profile') as UrlTree;
    expect(r).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(r)).toBe('/login?returnUrl=%2Fprofile');
  });

  it('redirects without returnUrl from the root', () => {
    const r = run('/') as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(r)).toBe('/login');
  });

  it('rejects an expired session', () => {
    sessionStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify({ token: 't', expiresAt: new Date(Date.now() - 1000).toISOString() }));
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    expect(run('/dashboard')).toBeInstanceOf(UrlTree);
  });
});
