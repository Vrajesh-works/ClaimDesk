import { ClaimAction, ClaimStatus, ProblemDetails } from './models';
import { HttpErrorResponse } from '@angular/common/http';

export const STATUS_LABELS: Record<ClaimStatus, string> = {
  Submitted: 'Submitted',
  InReview: 'In review',
  MoreInfoNeeded: 'More info needed',
  Approved: 'Approved',
  Denied: 'Denied',
  Paid: 'Paid',
};

export const ALL_STATUSES = Object.keys(STATUS_LABELS) as ClaimStatus[];

export interface ActionMeta {
  label: string;
  path: string;
  /** Mirrors ClaimWorkflow.RequiresNote on the server. */
  requiresNote: boolean;
  tone: 'primary' | 'danger' | 'neutral';
}

export const ACTIONS: Record<ClaimAction, ActionMeta> = {
  StartReview: { label: 'Start review', path: 'start-review', requiresNote: false, tone: 'primary' },
  RequestInfo: { label: 'Request info', path: 'request-info', requiresNote: true, tone: 'neutral' },
  ProvideInfo: { label: 'Provide info', path: 'provide-info', requiresNote: true, tone: 'primary' },
  Approve: { label: 'Approve', path: 'approve', requiresNote: false, tone: 'primary' },
  Deny: { label: 'Deny', path: 'deny', requiresNote: true, tone: 'danger' },
  MarkPaid: { label: 'Mark paid', path: 'mark-paid', requiresNote: false, tone: 'primary' },
};

/** Select options for the Kaggle-shaped vehicle fields. Values are the API enum names. */
export const OPTIONS = {
  category: [
    { value: 'Sedan', label: 'Sedan' },
    { value: 'Sport', label: 'Sport' },
    { value: 'Utility', label: 'Utility' },
  ],
  accidentArea: [
    { value: 'Urban', label: 'Urban' },
    { value: 'Rural', label: 'Rural' },
  ],
  fault: [
    { value: 'PolicyHolder', label: 'Policy holder' },
    { value: 'ThirdParty', label: 'Third party' },
  ],
  basePolicy: [
    { value: 'Liability', label: 'Liability' },
    { value: 'Collision', label: 'Collision' },
    { value: 'AllPerils', label: 'All perils' },
  ],
  agentType: [
    { value: 'Internal', label: 'Internal' },
    { value: 'External', label: 'External' },
  ],
  daysPolicyAccident: [
    { value: 'None', label: 'Same day' },
    { value: 'OneToSeven', label: '1 to 7 days' },
    { value: 'EightToFifteen', label: '8 to 15 days' },
    { value: 'FifteenToThirty', label: '15 to 30 days' },
    { value: 'MoreThanThirty', label: 'More than 30 days' },
  ],
  addressChange: [
    { value: 'NoChange', label: 'No change' },
    { value: 'UnderSixMonths', label: 'Under 6 months' },
    { value: 'OneYear', label: 'About 1 year' },
    { value: 'TwoToThreeYears', label: '2 to 3 years' },
    { value: 'FourToEightYears', label: '4 to 8 years' },
  ],
  pastClaims: [
    { value: 'None', label: 'None' },
    { value: 'One', label: '1' },
    { value: 'TwoToFour', label: '2 to 4' },
    { value: 'MoreThanFour', label: 'More than 4' },
  ],
} as const;

/** Turns an API error into one readable line. */
export function describeError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as ProblemDetails | null;
    if (body?.errors) {
      const first = Object.values(body.errors).flat()[0];
      if (first) {
        return first;
      }
    }
    if (body?.detail) {
      return body.detail;
    }
    if (body?.title) {
      return body.title;
    }
    if (error.status === 0) {
      return 'The API is unreachable.';
    }
    return `Request failed (${error.status}).`;
  }
  return 'Something went wrong.';
}
