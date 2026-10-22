export interface NavItem {
  path: string;
  label: string;
  /** null when the page is functional in this phase */
  phase: string | null;
  blurb: string;
}

export const NAV_ITEMS: NavItem[] = [
  { path: 'dashboard', label: 'Dashboard', phase: null, blurb: 'Profile completeness and what to do next.' },
  { path: 'profile', label: 'Career Profile', phase: null, blurb: 'The facts about you that everything else is built from.' },
  { path: 'job-search', label: 'Job Search', phase: null, blurb: 'Choose the company job boards to search and run a search.' },
  { path: 'opportunities', label: 'Job Opportunities', phase: null, blurb: 'Review the jobs found for you, with the reasons for each filter.' },
  { path: 'application-review', label: 'Application Review', phase: null, blurb: 'Check and approve each application before you send it.' },
  { path: 'applications', label: 'My Applications', phase: null, blurb: 'Every application, its status, and what needs your attention.' },
  { path: 'cv-library', label: 'Import CV', phase: null, blurb: 'Bring in an existing CV and choose what to add to your profile.' },
  { path: 'analytics', label: 'Analytics', phase: null, blurb: 'See how your search is going, based on real activity.' },
  { path: 'integrations', label: 'Integrations', phase: 'later', blurb: 'Connect external services.' },
  { path: 'settings', label: 'Settings', phase: 'later', blurb: 'Application preferences.' },
];

/** "Available in Phase 2-3" / "Available in a later phase" */
export function phaseLabel(phase: string): string {
  return phase === 'later' ? 'Available in a later phase' : `Available in Phase ${phase.replace('-', '–')}`;
}
