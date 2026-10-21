import { HttpErrorResponse } from '@angular/common/http';

/** A readable message for a failed request, using the API's own title when it sent one. */
export function problemMessage(e: unknown, fallback: string): string {
  if (e instanceof HttpErrorResponse) {
    if (e.status === 0) return 'Cannot reach the server.';
    const title = (e.error as { title?: string } | null)?.title;
    if (typeof title === 'string' && title.length > 0 && e.status < 500) return title;
  }
  return fallback;
}

/** Field errors from a 400 response, keyed by the API's camelCase field name. */
export function fieldErrors(e: unknown): Record<string, string[]> {
  if (e instanceof HttpErrorResponse && e.status === 400) {
    const errors = (e.error as { errors?: Record<string, string[]> } | null)?.errors;
    if (errors && typeof errors === 'object') return errors;
  }
  return {};
}
