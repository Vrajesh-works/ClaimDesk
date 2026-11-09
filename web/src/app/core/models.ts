// Shapes mirror the API DTOs in ClaimDesk.Application.Claims.ClaimDtos.
// Enums travel as strings (the API registers JsonStringEnumConverter).

export type Role = 'Claimant' | 'Adjuster' | 'Admin';

export type ClaimStatus = 'Submitted' | 'InReview' | 'MoreInfoNeeded' | 'Approved' | 'Denied' | 'Paid';

export type ClaimAction = 'StartReview' | 'RequestInfo' | 'ProvideInfo' | 'Approve' | 'Deny' | 'MarkPaid';

export type ClaimEventType = 'Submitted' | 'StatusChanged' | 'EvidenceAdded' | 'RiskScored' | 'SlaReminder' | 'StuckAlert';

export type VehicleCategory = 'Sedan' | 'Sport' | 'Utility';
export type AccidentArea = 'Urban' | 'Rural';
export type Fault = 'PolicyHolder' | 'ThirdParty';
export type BasePolicy = 'Liability' | 'Collision' | 'AllPerils';
export type AgentType = 'Internal' | 'External';
export type DaysPolicyAccident = 'None' | 'OneToSeven' | 'EightToFifteen' | 'FifteenToThirty' | 'MoreThanThirty';
export type AddressChange = 'NoChange' | 'UnderSixMonths' | 'OneYear' | 'TwoToThreeYears' | 'FourToEightYears';
export type PastClaims = 'None' | 'One' | 'TwoToFour' | 'MoreThanFour';

export interface UserInfo {
  id: string;
  email: string;
  displayName: string;
  role: Role;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: UserInfo;
}

export interface VehicleDetails {
  make: string;
  vehicleYear: number;
  category: VehicleCategory;
  accidentArea: AccidentArea;
  fault: Fault;
  basePolicy: BasePolicy;
  agentType: AgentType;
  policeReportFiled: boolean;
  witnessPresent: boolean;
  daysPolicyAccident: DaysPolicyAccident;
  addressChange: AddressChange;
  pastClaims: PastClaims;
}

export interface FileClaimRequest extends VehicleDetails {
  policyNumber: string;
  incidentDate: string;
  claimedAmount: number;
  description: string;
}

export interface ClaimSummary {
  id: string;
  number: string;
  status: ClaimStatus;
  policyNumber: string;
  claimedAmount: number;
  riskScore: number;
  flaggedForReview: boolean;
  submittedAt: string;
  lastActivityAt: string;
}

export interface Evidence {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
}

export interface ClaimDetail {
  id: string;
  number: string;
  claimantId: string;
  status: ClaimStatus;
  policyNumber: string;
  incidentDate: string;
  claimedAmount: number;
  approvedAmount: number | null;
  description: string;
  vehicle: VehicleDetails;
  riskScore: number;
  riskReasons: string[];
  flaggedForReview: boolean;
  assignedAdjusterId: string | null;
  submittedAt: string;
  lastActivityAt: string;
  evidence: Evidence[];
  availableActions: ClaimAction[];
}

export interface ClaimEvent {
  id: number;
  type: ClaimEventType;
  fromStatus: ClaimStatus | null;
  toStatus: ClaimStatus | null;
  actorId: string | null;
  actorRole: string;
  note: string | null;
  occurredAt: string;
}

export interface TransitionRequest {
  note?: string | null;
  approvedAmount?: number | null;
}

export interface EvidenceLink {
  url: string;
  expiresAt: string;
}

export interface NotificationItem {
  id: string;
  claimId: string;
  kind: string;
  recipientRole: string;
  message: string;
  createdAt: string;
}

/** RFC 7807 problem body returned by the API on errors. */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
