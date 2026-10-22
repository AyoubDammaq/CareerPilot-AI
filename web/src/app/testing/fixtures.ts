import { ProfileDto } from '../core/models';

export function sampleProfile(overrides: Partial<ProfileDto> = {}): ProfileDto {
  return {
    fullName: 'Ada Lovelace',
    email: 'ada@example.com',
    phone: null,
    headline: 'Engineer',
    summary: 'Builds things.',
    country: 'UK',
    city: 'London',
    githubUrl: null,
    linkedinUrl: 'https://linkedin.com/in/ada',
    websiteUrl: null,
    version: 7,
    workExperiences: [
      {
        id: 'w1', company: 'Analytical Engines', title: 'Programmer', location: 'London',
        startDate: '2020-01-01', endDate: null, description: 'Wrote programs',
        achievements: ['First algorithm', 'Second algorithm'], isVerified: true,
      },
    ],
    education: [{ id: 'e1', institution: 'Home', degree: 'BSc', field: 'Maths', startDate: null, endDate: '1835-06-01' }],
    skills: [{ id: 's1', name: 'Math', category: 'Core', level: 'Expert', isVerified: true }],
    projects: [{ id: 'p1', name: 'Notes', description: 'd', url: null, technologies: ['Paper'], results: ['Published'], isVerified: false }],
    languages: [{ id: 'l1', name: 'English', level: 'Native' }],
    preferences: {
      targetCountries: ['UK'], targetCities: ['London'], workModes: ['Remote', 'Hybrid'],
      willingToRelocate: true, requiresVisaSponsorship: false, includeInternationalRemote: true, employmentTypes: ['Permanent'],
      targetTitles: ['Engineer'], workingLanguages: ['English'], includeSeniorRoles: false,
      experienceLevel: 'Senior', minSalary: 50000, preferredSalary: null, salaryCurrency: 'GBP',
    },
    ...overrides,
  };
}

import type { ApplicationDto, CompanyDto, JobSummaryDto, SearchRunDto } from '../core/models';

export function sampleCompany(overrides: Partial<CompanyDto> = {}): CompanyDto {
  return {
    id: 'c1', name: 'Datadog', kind: 'Greenhouse', boardToken: 'datadog', useEuEndpoint: false, isEnabled: true,
    lastSyncAt: '2026-10-20T10:00:00Z', lastSuccessAt: '2026-10-20T10:00:00Z', lastError: null, lastJobCount: 42,
    ...overrides,
  };
}

export function sampleRun(overrides: Partial<SearchRunDto> = {}): SearchRunDto {
  return {
    id: 'r1', trigger: 'Manual', status: 'Succeeded', createdAt: '2026-10-20T10:00:00Z', startedAt: '2026-10-20T10:00:01Z',
    finishedAt: '2026-10-20T10:00:09Z', companiesTotal: 2, companiesSucceeded: 2, jobsFetched: 10, jobsNew: 4,
    jobsUpdated: 1, jobsClosed: 0, errors: [],
    ...overrides,
  };
}

export function sampleJob(overrides: Partial<JobSummaryDto> = {}): JobSummaryDto {
  return {
    id: 'j1', title: 'Backend Engineer', companyName: 'Datadog', locationText: 'Paris, France', country: 'France', city: 'Paris',
    workMode: null, employmentType: 'Full time', status: 'Open', geoFit: 'Match', geoNote: null, isRelevant: true,
    filterReasons: [], seniority: 'Unspecified', skills: ['C#', 'PostgreSQL'], publishedAt: '2026-09-01T00:00:00Z',
    discoveredAt: '2026-10-01T00:00:00Z', lastVerifiedAt: '2026-10-20T00:00:00Z', applicationDeadline: null,
    salaryMin: null, salaryMax: null, salaryCurrency: null, salaryPeriod: null, decision: 'None', language: 'en',
    mentionsSponsorship: false, mentionsRelocation: false, sourceUrl: 'https://careers.example.test/1',
    applyUrl: 'https://careers.example.test/1/apply', sources: ['Greenhouse'], matchScore: 82, matchTier: 'Strong',
    ...overrides,
  };
}

export function sampleApplication(overrides: Partial<ApplicationDto> = {}): ApplicationDto {
  return {
    id: 'a1', jobId: 'j1', jobTitle: 'Backend Engineer', companyName: 'Datadog', applyUrl: 'https://careers.example.test/1/apply',
    jobStatus: 'Open', status: 'Shortlisted', cvDocumentId: null, coverLetterDocumentId: null, createdAt: '2026-10-21T10:00:00Z',
    approvedAt: null, submittedAt: null, submissionNote: null, approvedBundleHash: null, allowedNext: ['AwaitingApproval', 'Withdrawn'], review: null,
    ...overrides,
  };
}

export const sampleReview: NonNullable<ApplicationDto['review']> = {
  bundleHash: 'abcdef0123456789abcdef',
  cv: {
    fullName: 'Ayoub Test', headline: '', email: 'a@example.test', phone: null, location: '', links: [], summary: '',
    experience: [{ title: 'Engineer', company: 'Sagemcom', location: '', period: 'Oct 2025 – Present', description: '', achievements: [] }],
    education: [], skills: ['C#'], projects: [], languages: [],
  },
  letter: {
    fullName: 'Ayoub Test', email: 'a@example.test', phone: null, location: '', company: 'Datadog', jobTitle: 'Backend Engineer',
    greeting: 'Dear Hiring Team,', paragraphs: ['First paragraph.', 'Second paragraph.'], closing: 'Kind regards', source: 'ai:m',
  },
  claims: { flags: [], clean: true },
  blockers: [],
  documentsStale: false,
};
