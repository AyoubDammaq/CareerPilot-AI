export type JobSourceKind = 'Greenhouse' | 'Lever' | 'Ashby';

export interface CompanyDto {
  id: string;
  name: string;
  kind: JobSourceKind;
  boardToken: string;
  useEuEndpoint: boolean;
  isEnabled: boolean;
  lastSyncAt: string | null;
  lastSuccessAt: string | null;
  lastError: string | null;
  lastJobCount: number | null;
}

export interface CompanyInput {
  name: string;
  kind: JobSourceKind;
  boardToken: string;
  useEuEndpoint: boolean;
  isEnabled: boolean;
}

export interface CompanyTestResult {
  ok: boolean;
  jobCount: number | null;
  error: string | null;
}

export type RunStatus = 'Queued' | 'Running' | 'Succeeded' | 'PartiallyFailed' | 'Failed';

export interface SearchRunDto {
  id: string;
  trigger: string;
  status: RunStatus;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  companiesTotal: number;
  companiesSucceeded: number;
  jobsFetched: number;
  jobsNew: number;
  jobsUpdated: number;
  jobsClosed: number;
  errors: string[];
}

export type JobStatus = 'Open' | 'Closed' | 'Expired' | 'Unverified';
export type GeoFit = 'Match' | 'NeedsReview' | 'NoMatch';
export type UserDecision = 'None' | 'Saved' | 'Excluded';

export interface JobSummaryDto {
  id: string;
  title: string;
  companyName: string;
  locationText: string;
  country: string;
  city: string;
  workMode: string | null;
  employmentType: string | null;
  status: JobStatus;
  geoFit: GeoFit;
  geoNote: string | null;
  isRelevant: boolean;
  filterReasons: string[];
  seniority: string;
  skills: string[];
  publishedAt: string | null;
  discoveredAt: string;
  lastVerifiedAt: string;
  applicationDeadline: string | null;
  salaryMin: number | null;
  salaryMax: number | null;
  salaryCurrency: string | null;
  salaryPeriod: string | null;
  decision: UserDecision;
  language: string | null;
  mentionsSponsorship: boolean;
  mentionsRelocation: boolean;
  sourceUrl: string;
  applyUrl: string;
  sources: string[];
}

export interface PostingDto {
  source: string;
  sourceUrl: string;
  applyUrl: string;
  isPresent: boolean;
  lastSeenAt: string;
}

export interface JobDetailDto {
  summary: JobSummaryDto;
  description: string;
  experienceYearsHint: number | null;
  postings: PostingDto[];
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface JobFilters {
  q?: string;
  country?: string;
  workMode?: string;
  status?: string;
  geoFit?: string;
  decision?: string;
  relevantOnly?: boolean;
  sort?: string;
  page?: number;
  pageSize?: number;
}
