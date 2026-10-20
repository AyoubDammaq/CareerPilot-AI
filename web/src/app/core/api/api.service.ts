import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DashboardDto, LoginResponse, ProfileDto } from '../models';

/** Thin typed wrapper over the backend HTTP API. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  login(password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.base}/auth/login`, { password });
  }

  getProfile(): Observable<ProfileDto> {
    return this.http.get<ProfileDto>(`${this.base}/profile`);
  }

  saveProfile(profile: ProfileDto): Observable<ProfileDto> {
    return this.http.put<ProfileDto>(`${this.base}/profile`, profile);
  }

  getDashboard(): Observable<DashboardDto> {
    return this.http.get<DashboardDto>(`${this.base}/dashboard`);
  }

  /** Unauthenticated liveness probe (served outside /api). */
  healthLive(): Observable<unknown> {
    return this.http.get('/health/live', { responseType: 'text' });
  }
}
