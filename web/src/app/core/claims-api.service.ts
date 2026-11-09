import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ACTIONS } from './labels';
import {
  ClaimAction,
  ClaimDetail,
  ClaimEvent,
  ClaimStatus,
  ClaimSummary,
  Evidence,
  EvidenceLink,
  FileClaimRequest,
  NotificationItem,
  TransitionRequest,
  UserInfo,
} from './models';

@Injectable({ providedIn: 'root' })
export class ClaimsApi {
  private readonly http = inject(HttpClient);

  list(status?: ClaimStatus | null): Observable<ClaimSummary[]> {
    const params = status ? new HttpParams().set('status', status) : undefined;
    return this.http.get<ClaimSummary[]>('/api/claims', { params });
  }

  get(id: string): Observable<ClaimDetail> {
    return this.http.get<ClaimDetail>(`/api/claims/${id}`);
  }

  history(id: string): Observable<ClaimEvent[]> {
    return this.http.get<ClaimEvent[]>(`/api/claims/${id}/history`);
  }

  file(request: FileClaimRequest): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>('/api/claims', request);
  }

  transition(id: string, action: ClaimAction, body: TransitionRequest): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(`/api/claims/${id}/${ACTIONS[action].path}`, body);
  }

  uploadEvidence(id: string, file: File): Observable<Evidence> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<Evidence>(`/api/claims/${id}/evidence`, form);
  }

  evidenceLink(id: string, evidenceId: string): Observable<EvidenceLink> {
    return this.http.get<EvidenceLink>(`/api/claims/${id}/evidence/${evidenceId}/link`);
  }

  queue(): Observable<ClaimSummary[]> {
    return this.http.get<ClaimSummary[]>('/api/queue');
  }

  notifications(): Observable<NotificationItem[]> {
    return this.http.get<NotificationItem[]>('/api/notifications');
  }

  users(): Observable<UserInfo[]> {
    return this.http.get<UserInfo[]>('/api/admin/users');
  }
}
