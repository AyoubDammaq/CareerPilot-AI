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
