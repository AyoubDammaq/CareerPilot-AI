import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { AuthService } from '../../core/auth/auth.service';

export function loginErrorMessage(e: unknown): string {
  if (e instanceof HttpErrorResponse) {
    if (e.status === 401) return 'Incorrect password.';
    if (e.status === 429) return 'Too many attempts. Wait a little while and try again.';
    if (e.status === 0) return 'Cannot reach the server. Check that the API is running.';
  }
  return 'Sign-in failed. Please try again.';
}

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="login-wrap">
      <form class="card login-card" (ngSubmit)="submit()" novalidate>
        <h1>CareerPilot AI</h1>
        <p class="muted">Sign in to your personal job-search workspace.</p>
        <div class="field">
          <label for="password">Password</label>
          <input id="password" type="password" [formControl]="password" autocomplete="current-password" aria-describedby="login-error" />
        </div>
        <div id="login-error" role="alert" aria-live="assertive">
          @if (error()) {
            <p class="alert alert-error">{{ error() }}</p>
          }
        </div>
        <button type="submit" class="btn btn-primary btn-block" [disabled]="busy()">{{ busy() ? 'Signing in…' : 'Sign in' }}</button>
        @if (apiDown()) {
          <p class="muted small" role="status">The API did not answer its health check, so signing in may fail.</p>
        }
      </form>
    </main>
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly password = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly apiDown = signal(false);

  constructor() {
    if (this.auth.isAuthenticated()) void this.router.navigateByUrl('/dashboard');
    this.api.healthLive().subscribe({ error: () => this.apiDown.set(true) });
  }

  submit(): void {
    if (this.password.invalid) {
      this.error.set('Enter your password.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.auth.login(this.password.value).subscribe({
      next: () => {
        const ret = this.route.snapshot.queryParamMap.get('returnUrl');
        // Only follow in-app relative targets.
        void this.router.navigateByUrl(ret && ret.startsWith('/') && !ret.startsWith('//') ? ret : '/dashboard');
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.password.setValue('');
        this.error.set(loginErrorMessage(e));
      },
    });
  }
}
