import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CompanyDto,
  CompanyInput,
  CompanyTestResult,
  DashboardDto,
  AiStatus,
  ApplicationDto,
  CoverLetterDto,
  DocumentSummaryDto,
  DocumentDto,
  JobAnalysisDto,
  JobDetailDto,
  JobFilters,
  JobSummaryDto,
  ManualImportResult,
  ManualJobInput,
  LoginResponse,
  ImportApplyInput,
  ImportApplyResult,
  ImportProposal,
  PagedResult,
  Reminder,
  TrackingSummary,
  RescoreResult,
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

  importJobFromUrl(url: string): Observable<ManualImportResult> {
    return this.http.post<ManualImportResult>(`${this.base}/jobs/import/url`, { url });
  }

  importJobFromText(input: ManualJobInput): Observable<ManualImportResult> {
    return this.http.post<ManualImportResult>(`${this.base}/jobs/import/text`, input);
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

  analyzeJob(id: string, refresh = false): Observable<JobAnalysisDto> {
    return this.http.post<JobAnalysisDto>(`${this.base}/jobs/${id}/analysis`, {}, { params: refresh ? { refresh: true } : {} });
  }

  createCv(jobId: string): Observable<DocumentDto> {
    return this.http.post<DocumentDto>(`${this.base}/jobs/${jobId}/documents/cv`, {});
  }

  draftCoverLetter(jobId: string): Observable<CoverLetterDto> {
    return this.http.post<CoverLetterDto>(`${this.base}/jobs/${jobId}/documents/cover-letter`, {});
  }

  reviseCoverLetter(documentId: string, paragraphs: string[]): Observable<CoverLetterDto> {
    return this.http.post<CoverLetterDto>(`${this.base}/documents/${documentId}/revise`, { paragraphs });
  }

  trackingReminders(): Observable<Reminder[]> {
    return this.http.get<Reminder[]>(`${this.base}/tracking/reminders`);
  }

  trackingSummary(): Observable<TrackingSummary> {
    return this.http.get<TrackingSummary>(`${this.base}/tracking/summary`);
  }

  aiStatus(): Observable<AiStatus> {
    return this.http.get<AiStatus>(`${this.base}/ai/status`);
  }

  previewImport(file: File, useAi: boolean): Observable<ImportProposal> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('useAi', String(useAi));
    return this.http.post<ImportProposal>(`${this.base}/profile/import/preview`, form);
  }

  applyImport(input: ImportApplyInput): Observable<ImportApplyResult> {
    return this.http.post<ImportApplyResult>(`${this.base}/profile/import/apply`, input);
  }

  startApplication(jobId: string): Observable<ApplicationDto> {
    return this.http.post<ApplicationDto>(`${this.base}/jobs/${jobId}/application`, {});
  }

  listApplications(): Observable<ApplicationDto[]> {
    return this.http.get<ApplicationDto[]>(`${this.base}/applications`);
  }

  getApplication(id: string): Observable<ApplicationDto> {
    return this.http.get<ApplicationDto>(`${this.base}/applications/${id}`);
  }

  jobDocuments(jobId: string): Observable<DocumentSummaryDto[]> {
    return this.http.get<DocumentSummaryDto[]>(`${this.base}/jobs/${jobId}/documents`);
  }

  setApplicationDocuments(id: string, cvDocumentId: string, coverLetterDocumentId: string | null): Observable<ApplicationDto> {
    return this.http.put<ApplicationDto>(`${this.base}/applications/${id}/documents`, { cvDocumentId, coverLetterDocumentId });
  }

  applicationAction(id: string, action: 'request-review' | 'withdraw-approval' | 'ready' | 'confirm-submission'): Observable<ApplicationDto> {
    return this.http.post<ApplicationDto>(`${this.base}/applications/${id}/${action}`, {});
  }

  approveApplication(id: string, bundleHash: string, acknowledgeFlags: boolean): Observable<ApplicationDto> {
    return this.http.post<ApplicationDto>(`${this.base}/applications/${id}/approve`, { bundleHash, acknowledgeFlags });
  }

  recordSubmission(id: string, confirmed: boolean, note: string): Observable<ApplicationDto> {
    return this.http.post<ApplicationDto>(`${this.base}/applications/${id}/submission`, { confirmed, note });
  }

  setApplicationStatus(id: string, status: string): Observable<ApplicationDto> {
    return this.http.post<ApplicationDto>(`${this.base}/applications/${id}/status`, { status });
  }

  /** Downloads through HttpClient so the bearer token is sent (a plain link would not carry it). */
  downloadDocument(id: string, format: 'docx' | 'pdf' = 'docx'): Observable<HttpResponse<Blob>> {
    const query = format === 'pdf' ? '?format=pdf' : '';
    return this.http.get(`${this.base}/documents/${id}/download${query}`, { observe: 'response', responseType: 'blob' });
  }

  rescore(): Observable<RescoreResult> {
    return this.http.post<RescoreResult>(`${this.base}/jobs/rescore`, {});
  }

  setDecision(id: string, decision: UserDecision): Observable<JobSummaryDto> {
    return this.http.put<JobSummaryDto>(`${this.base}/jobs/${id}/decision`, { decision });
  }

  /** Unauthenticated liveness probe (served outside /api). */
  healthLive(): Observable<unknown> {
    return this.http.get('/health/live', { responseType: 'text' });
  }
}
