import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CompanyDto,
  CompanyInput,
  CompanyTestResult,
  DashboardDto,
  JobDetailDto,
  JobFilters,
  JobSummaryDto,
  LoginResponse,
  PagedResult,
  ProfileDto,
  SearchRunDto,
  UserDecision,
} from '../models';

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

  // ---- job discovery ----------------------------------------------------------------------------

  listCompanies(): Observable<CompanyDto[]> {
    return this.http.get<CompanyDto[]>(`${this.base}/sources/companies`);
  }

  addCompany(input: CompanyInput): Observable<CompanyDto> {
    return this.http.post<CompanyDto>(`${this.base}/sources/companies`, input);
  }

  updateCompany(id: string, input: CompanyInput): Observable<CompanyDto> {
    return this.http.put<CompanyDto>(`${this.base}/sources/companies/${id}`, input);
  }

  deleteCompany(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/sources/companies/${id}`);
  }

  testCompany(id: string): Observable<CompanyTestResult> {
    return this.http.post<CompanyTestResult>(`${this.base}/sources/companies/${id}/test`, {});
  }

  startSearch(): Observable<SearchRunDto> {
    return this.http.post<SearchRunDto>(`${this.base}/search-runs`, {});
  }

  listRuns(): Observable<SearchRunDto[]> {
    return this.http.get<SearchRunDto[]>(`${this.base}/search-runs`);
  }

  getRun(id: string): Observable<SearchRunDto> {
    return this.http.get<SearchRunDto>(`${this.base}/search-runs/${id}`);
  }

  listJobs(filters: JobFilters): Observable<PagedResult<JobSummaryDto>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filters)) {
      if (value !== undefined && value !== null && value !== '') params = params.set(key, String(value));
    }
    return this.http.get<PagedResult<JobSummaryDto>>(`${this.base}/jobs`, { params });
  }

  getJob(id: string): Observable<JobDetailDto> {
    return this.http.get<JobDetailDto>(`${this.base}/jobs/${id}`);
  }

  setDecision(id: string, decision: UserDecision): Observable<JobSummaryDto> {
    return this.http.put<JobSummaryDto>(`${this.base}/jobs/${id}/decision`, { decision });
  }

  /** Unauthenticated liveness probe (served outside /api). */
  healthLive(): Observable<unknown> {
    return this.http.get('/health/live', { responseType: 'text' });
  }
}
