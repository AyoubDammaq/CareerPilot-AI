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
  matchScore: number | null;
  matchTier: MatchTier | null;
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
  match: MatchResult | null;
  analysis: JobAnalysisDto | null;
  aiAvailable: boolean;
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
  minScore?: number;
  sort?: string;
  page?: number;
  pageSize?: number;
}

export type MatchTier = 'Strong' | 'Good' | 'Possible' | 'Weak';

export interface MatchComponent {
  key: string;
  label: string;
  maxPoints: number;
  points: number;
  known: boolean;
  note: string;
}

export interface MatchResult {
  score: number;
  tier: MatchTier;
  confidence: number;
  components: MatchComponent[];
  matchedSkills: string[];
  missingSkills: string[];
  warnings: string[];
}

export interface RescoreResult {
  scored: number;
  profileMissing: boolean;
}

export interface JobAnalysisDto {
  summary: string;
  strengths: string[];
  gaps: string[];
  redFlags: string[];
  questionsToAsk: string[];
  model: string;
  generatedAt: string;
  inputTokens: number;
  outputTokens: number;
}

export interface CvExperience { title: string; company: string; location: string; period: string; description: string; achievements: string[] }
export interface CvEducation { degree: string; field: string; institution: string; period: string }
export interface CvProject { name: string; description: string; url: string | null; technologies: string[]; results: string[] }
export interface CvLanguage { name: string; level: string }

export interface CvContent {
  fullName: string;
  headline: string;
  email: string;
  phone: string | null;
  location: string;
  links: string[];
  summary: string;
  experience: CvExperience[];
  education: CvEducation[];
  skills: string[];
  projects: CvProject[];
  languages: CvLanguage[];
}

export interface CvReport {
  omitted: string[];
  notOnCv: string[];
  warnings: string[];
}

export interface DocumentDto {
  id: string;
  jobId: string;
  kind: string;
  title: string;
  createdAt: string;
  contentHash: string;
  stale: boolean;
  content: CvContent;
  report: CvReport | null;
}

export interface ClaimFlag { kind: 'Skill' | 'UnverifiedSkill' | 'Figure' | 'Experience'; text: string; message: string }
export interface ClaimCheck { flags: ClaimFlag[]; clean: boolean }

export interface CoverLetterContent {
  fullName: string;
  email: string;
  phone: string | null;
  location: string;
  company: string;
  jobTitle: string;
  greeting: string;
  paragraphs: string[];
  closing: string;
  source: string;
}

export interface CoverLetterDto {
  id: string;
  jobId: string;
  title: string;
  createdAt: string;
  contentHash: string;
  stale: boolean;
  letter: CoverLetterContent;
  claims: ClaimCheck;
}

export interface ApplicationReview {
  bundleHash: string | null;
  cv: CvContent | null;
  letter: CoverLetterContent | null;
  claims: ClaimCheck | null;
  blockers: string[];
  documentsStale: boolean;
}

export interface ApplicationDto {
  id: string;
  jobId: string;
  jobTitle: string;
  companyName: string;
  applyUrl: string;
  jobStatus: string;
  status: string;
  cvDocumentId: string | null;
  coverLetterDocumentId: string | null;
  createdAt: string;
  approvedAt: string | null;
  submittedAt: string | null;
  submissionNote: string | null;
  approvedBundleHash: string | null;
  allowedNext: string[];
  review: ApplicationReview | null;
}

export interface DocumentSummaryDto {
  id: string;
  kind: 'Cv' | 'CoverLetter';
  title: string;
  createdAt: string;
  contentHash: string;
  stale: boolean;
  source: string | null;
}
