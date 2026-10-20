export const SKILL_LEVELS = ['Beginner', 'Intermediate', 'Advanced', 'Expert'] as const;
export type SkillLevel = (typeof SKILL_LEVELS)[number];

export const WORK_MODES = ['Remote', 'Hybrid', 'OnSite'] as const;
export type WorkMode = (typeof WORK_MODES)[number];

export const EMPLOYMENT_TYPES = ['Permanent', 'FixedTerm', 'Contract', 'Internship', 'PartTime'] as const;
export type EmploymentType = (typeof EMPLOYMENT_TYPES)[number];

/** All dates are "yyyy-MM-dd" strings. New items are sent with id null. */
export interface WorkExperienceDto {
  id: string | null;
  company: string;
  title: string;
  location: string;
  startDate: string;
  endDate: string | null;
  description: string;
  achievements: string[];
  isVerified: boolean;
}

export interface EducationDto {
  id: string | null;
  institution: string;
  degree: string;
  field: string;
  startDate: string | null;
  endDate: string | null;
}

export interface SkillDto {
  id: string | null;
  name: string;
  category: string;
  level: SkillLevel | null;
  isVerified: boolean;
}

export interface ProjectDto {
  id: string | null;
  name: string;
  description: string;
  url: string | null;
  technologies: string[];
  results: string[];
  isVerified: boolean;
}

export interface LanguageDto {
  id: string | null;
  name: string;
  /** e.g. "B2", "Beginner" */
  level: string;
}

export interface PreferencesDto {
  targetCountries: string[];
  targetCities: string[];
  workModes: WorkMode[];
  willingToRelocate: boolean;
  requiresVisaSponsorship: boolean;
  includeInternationalRemote: boolean;
  employmentTypes: EmploymentType[];
  targetTitles: string[];
  workingLanguages: string[];
  includeSeniorRoles: boolean;
  experienceLevel: string;
  minSalary: number | null;
  preferredSalary: number | null;
  salaryCurrency: string;
}

export interface ProfileDto {
  fullName: string;
  email: string;
  phone: string | null;
  headline: string;
  summary: string;
  country: string;
  city: string;
  githubUrl: string | null;
  linkedinUrl: string | null;
  websiteUrl: string | null;
  /** Optimistic concurrency token: echo back exactly what was loaded. */
  version: number;
  workExperiences: WorkExperienceDto[];
  education: EducationDto[];
  skills: SkillDto[];
  projects: ProjectDto[];
  languages: LanguageDto[];
  preferences: PreferencesDto;
}
