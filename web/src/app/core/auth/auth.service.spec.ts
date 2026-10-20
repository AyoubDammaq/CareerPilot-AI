import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AUTH_STORAGE_KEY, AuthService } from './auth.service';

const future = () => new Date(Date.now() + 3600_000).toISOString();
const past = () => new Date(Date.now() - 3600_000).toISOString();

function setup(): { auth: AuthService; http: HttpTestingController } {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { auth: TestBed.inject(AuthService), http: TestBed.inject(HttpTestingController) };
}

describe('AuthService', () => {
  beforeEach(() => sessionStorage.clear());

  it('is signed out by default', () => {
    const { auth } = setup();
    expect(auth.getToken()).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('stores the token in memory and sessionStorage on login', () => {
    const { auth, http } = setup();
    const exp = future();
    auth.login('pw').subscribe();
    const req = http.expectOne('/api/auth/login');
    expect(req.request.body).toEqual({ password: 'pw' });
    req.flush({ token: 'abc', expiresAt: exp });

    expect(auth.getToken()).toBe('abc');
    expect(auth.signedIn()).toBe(true);
    expect(JSON.parse(sessionStorage.getItem(AUTH_STORAGE_KEY)!)).toEqual({ token: 'abc', expiresAt: exp });
  });

  it('restores a valid session from sessionStorage', () => {
    sessionStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify({ token: 'kept', expiresAt: future() }));
    const { auth } = setup();
    expect(auth.getToken()).toBe('kept');
  });

  it('ignores and removes an expired stored session', () => {
    sessionStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify({ token: 'old', expiresAt: past() }));
    const { auth } = setup();
    expect(auth.getToken()).toBeNull();
    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
  });

  it('ignores corrupt stored data', () => {
    sessionStorage.setItem(AUTH_STORAGE_KEY, '{not json');
    const { auth } = setup();
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('clears the token once it expires', () => {
    vi.useFakeTimers();
    try {
      vi.setSystemTime(new Date('2030-01-01T00:00:00Z'));
      const { auth, http } = setup();
      auth.login('pw').subscribe();
      http.expectOne('/api/auth/login').flush({ token: 't', expiresAt: '2030-01-01T01:00:00Z' });
      expect(auth.getToken()).toBe('t');
      vi.setSystemTime(new Date('2030-01-01T01:00:01Z'));
      expect(auth.getToken()).toBeNull();
      expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    } finally {
      vi.useRealTimers();
    }
  });

  it('logout clears memory and storage', () => {
    const { auth, http } = setup();
    auth.login('pw').subscribe();
    http.expectOne('/api/auth/login').flush({ token: 't', expiresAt: future() });
    auth.logout();
    expect(auth.getToken()).toBeNull();
    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
  });

  it('does not store anything when login fails', () => {
    const { auth, http } = setup();
    let status = 0;
    auth.login('bad').subscribe({ error: (e) => (status = e.status) });
    http.expectOne('/api/auth/login').flush({ title: 'no' }, { status: 401, statusText: 'Unauthorized' });
    expect(status).toBe(401);
    expect(auth.isAuthenticated()).toBe(false);
  });
});
