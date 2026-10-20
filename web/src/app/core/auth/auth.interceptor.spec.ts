import { HttpClient } from '@angular/common/http';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AUTH_STORAGE_KEY, AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;
  let auth: AuthService;
  let navigate: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    sessionStorage.clear();
    sessionStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify({ token: 'tok', expiresAt: new Date(Date.now() + 3600_000).toISOString() }));
    navigate = vi.fn().mockResolvedValue(true);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigate, url: '/profile' } },
      ],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => ctrl.verify());

  it('adds the bearer header to API calls', () => {
    http.get('/api/profile').subscribe();
    const req = ctrl.expectOne('/api/profile');
    expect(req.request.headers.get('Authorization')).toBe('Bearer tok');
    req.flush({});
  });

  it('does not add the header to the login call or non-API urls', () => {
    http.post('/api/auth/login', {}).subscribe();
    expect(ctrl.expectOne('/api/auth/login').request.headers.has('Authorization')).toBe(false);
    http.get('https://example.com/x').subscribe();
    expect(ctrl.expectOne('https://example.com/x').request.headers.has('Authorization')).toBe(false);
  });

  it('on 401 clears the session, redirects to /login with returnUrl and rethrows', () => {
    let status = 0;
    http.get('/api/profile').subscribe({ error: (e) => (status = e.status) });
    ctrl.expectOne('/api/profile').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(status).toBe(401);
    expect(auth.isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/profile' } });
  });

  it('does not log out on other errors', () => {
    http.get('/api/profile').subscribe({ error: () => undefined });
    ctrl.expectOne('/api/profile').flush({}, { status: 409, statusText: 'Conflict' });
    expect(auth.isAuthenticated()).toBe(true);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not treat a failed login (401) as session expiry', () => {
    http.post('/api/auth/login', {}).subscribe({ error: () => undefined });
    ctrl.expectOne('/api/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(navigate).not.toHaveBeenCalled();
    expect(auth.isAuthenticated()).toBe(true);
  });
});
