export interface Reminder {
  applicationId: string;
  kind: string;
  jobTitle: string;
  companyName: string;
  message: string;
  days: number;
}

export interface StatusCount { status: string; count: number }
export interface GroupStat { name: string; submitted: number; replied: number }
export interface WeekCount { weekStart: string; submitted: number }

export interface TrackingSummary {
  total: number;
  active: number;
  submitted: number;
  replied: number;
  rejected: number;
  interviewing: number;
  offers: number;
  responseRate: number | null;
  medianDaysToResponse: number | null;
  smallSample: boolean;
  byStatus: StatusCount[];
  byCountry: GroupStat[];
  byScoreBand: GroupStat[];
  weekly: WeekCount[];
  notes: string[];
}
