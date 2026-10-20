import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from '../api/api.service';
import { LoginResponse } from '../models';

export const AUTH_STORAGE_KEY = 'careerpilot.auth';

interface StoredAuth {
  token: string;
  expiresAt: string;
}

/**
 * Holds the bearer token in memory and mirrors it to sessionStorage so a page
 * reload keeps the session. Expiry is checked every time the token is read.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly state = signal<StoredAuth | null>(this.restore());

  /** Reactive flag for templates. The authoritative check is isAuthenticated(). */
  readonly signedIn = computed(() => this.state() !== null);

  login(password: string): Observable<LoginResponse> {
    return this.api.login(password).pipe(tap((res) => this.setSession(res.token, res.expiresAt)));
  }

  logout(): void {
    this.state.set(null);
    try {
      sessionStorage.removeItem(AUTH_STORAGE_KEY);
    } catch {
      /* storage unavailable: memory state is already cleared */
    }
  }

  /** The token, or null when absent or expired (an expired token is cleared). */
  getToken(): string | null {
    const s = this.state();
    if (!s) return null;
    if (this.isExpired(s.expiresAt)) {
      this.logout();
      return null;
    }
    return s.token;
  }

  isAuthenticated(): boolean {
    return this.getToken() !== null;
  }

  private setSession(token: string, expiresAt: string): void {
    const s: StoredAuth = { token, expiresAt };
    this.state.set(s);
    try {
      sessionStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(s));
    } catch {
      /* keep the in-memory session only */
    }
  }

  private restore(): StoredAuth | null {
    try {
      const raw = sessionStorage.getItem(AUTH_STORAGE_KEY);
      if (!raw) return null;
      const parsed = JSON.parse(raw) as Partial<StoredAuth>;
      if (typeof parsed.token !== 'string' || typeof parsed.expiresAt !== 'string') {
        sessionStorage.removeItem(AUTH_STORAGE_KEY);
        return null;
      }
      if (this.isExpired(parsed.expiresAt)) {
        sessionStorage.removeItem(AUTH_STORAGE_KEY);
        return null;
      }
      return { token: parsed.token, expiresAt: parsed.expiresAt };
    } catch {
      return null;
    }
  }

  private isExpired(expiresAt: string): boolean {
    const t = Date.parse(expiresAt);
    // Unparseable expiry is treated as expired; 5s skew margin.
    return Number.isNaN(t) || t - 5000 <= Date.now();
  }
}
