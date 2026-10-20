/** RFC 7807 problem details as returned by the API. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
}

/** 400 validation response: field path -> messages, e.g. "workExperiences[0].endDate". */
export interface ValidationProblem extends ProblemDetails {
  errors?: Record<string, string[]>;
}
