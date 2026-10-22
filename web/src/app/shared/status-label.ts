const LABELS: Record<string, string> = {
  Shortlisted: 'Choosing documents',
  AwaitingApproval: 'Waiting for your approval',
  Approved: 'Approved by you',
  PreparingApplication: 'Preparing',
  ReadyToSubmit: 'Ready for you to submit',
  Submitted: 'Submitted (confirmed by you)',
  SubmissionUnconfirmed: 'Submitted, not confirmed',
  RejectedByEmployer: 'Rejected by employer',
  InterviewScheduled: 'Interview scheduled',
  InterviewCompleted: 'Interview completed',
  OfferReceived: 'Offer received',
  OfferAccepted: 'Offer accepted',
  OfferDeclined: 'Offer declined',
  Withdrawn: 'Withdrawn',
  ClosedOrExpired: 'Closed or expired',
};

export function statusLabel(status: string): string {
  return LABELS[status] ?? status;
}
