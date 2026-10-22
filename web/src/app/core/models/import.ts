export interface ProposedContact {
  fullName: string | null;
  email: string | null;
  phone: string | null;
  githubUrl: string | null;
  linkedinUrl: string | null;
}

export interface ProposedExperience {
  company: string;
  title: string;
  location: string;
  startDate: string;
  endDate: string | null;
  description: string;
  achievements: string[];
}

export interface ProposedEducation { institution: string; degree: string; field: string }
export interface ProposedProject { name: string; description: string; technologies: string[]; results: string[] }
export interface ProposedLanguage { name: string; level: string }

export interface ImportProposal {
  contact: ProposedContact;
  experiences: ProposedExperience[];
  education: ProposedEducation[];
  skills: string[];
  projects: ProposedProject[];
  languages: ProposedLanguage[];
  notes: string[];
  aiUsed: boolean;
  textCharacters: number;
  profileVersion: number;
}

export interface ImportApplyInput {
  version: number;
  markVerified: boolean;
  contact: ProposedContact | null;
  experiences: ProposedExperience[];
  education: ProposedEducation[];
  skills: string[];
  projects: ProposedProject[];
  languages: ProposedLanguage[];
}

export interface ImportApplyResult {
  added: number;
  skipped: string[];
}

export interface AiStatus {
  enabled: boolean;
  provider: string;
  model: string;
  usedToday: number;
  dailyLimit: number;
}
