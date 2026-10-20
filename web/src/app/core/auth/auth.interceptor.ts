import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

const isApiCall = (url: string) => url.startsWith(environment.apiBaseUrl + '/');
const isLogin = (url: string) => url.startsWith(`${environment.apiBaseUrl}/auth/login`);

/** Adds the bearer token to API calls and sends the user to /login on 401. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!isApiCall(req.url) || isLogin(req.url)) {
    return next(req);
  }

  const token = auth.getToken();
  const authed = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(authed).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && err.status === 401) {
        auth.logout();
        const returnUrl = router.url && !router.url.startsWith('/login') ? router.url : undefined;
        void router.navigate(['/login'], { queryParams: returnUrl ? { returnUrl } : {} });
      }
      return throwError(() => err);
    }),
  );
};
