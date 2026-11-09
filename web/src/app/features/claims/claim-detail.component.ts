import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnChanges, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ClaimsApi } from '../../core/claims-api.service';
import { EVIDENCE_ACCEPT, checkEvidence, formatBytes } from '../../core/evidence-rules';
import { ACTIONS, STATUS_LABELS, describeError } from '../../core/labels';
import { ClaimAction, ClaimDetail, ClaimEvent, ClaimStatus } from '../../core/models';
import { RiskBadgeComponent, StatusBadgeComponent } from '../../shared/status-badge.component';

@Component({
  selector: 'app-claim-detail',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, CurrencyPipe, DatePipe, StatusBadgeComponent, RiskBadgeComponent],
  template: `
    @if (loadError()) {
      <p class="alert" role="alert" data-testid="load-error">{{ loadError() }}</p>
      <a routerLink="/claims" class="btn">Back to claims</a>
    }

    @if (claim(); as c) {
      <header class="page-head">
        <div>
          <h1 data-testid="claim-number">{{ c.number }}</h1>
          <p class="muted">Policy {{ c.policyNumber }} - filed {{ c.submittedAt | date: 'medium' }}</p>
        </div>
        <app-status-badge [status]="c.status" />
      </header>

      @if (notice()) {
        <p class="notice" role="status" data-testid="notice">{{ notice() }}</p>
      }

      <div class="detail-grid">
        <section class="card">
          <h2>Claim</h2>
          <dl class="facts">
            <dt>Incident date</dt><dd>{{ c.incidentDate | date: 'mediumDate' }}</dd>
            <dt>Claimed</dt><dd data-testid="claimed-amount">{{ c.claimedAmount | currency: 'USD' }}</dd>
            <dt>Approved</dt><dd data-testid="approved-amount">{{ c.approvedAmount === null ? '-' : (c.approvedAmount | currency: 'USD') }}</dd>
            <dt>Vehicle</dt><dd>{{ c.vehicle.vehicleYear }} {{ c.vehicle.make }} ({{ c.vehicle.category }})</dd>
            <dt>Base policy</dt><dd>{{ c.vehicle.basePolicy }}</dd>
            <dt>At fault</dt><dd>{{ c.vehicle.fault === 'PolicyHolder' ? 'Policy holder' : 'Third party' }}</dd>
            <dt>Police report</dt><dd>{{ c.vehicle.policeReportFiled ? 'Yes' : 'No' }}</dd>
            <dt>Witness</dt><dd>{{ c.vehicle.witnessPresent ? 'Yes' : 'No' }}</dd>
          </dl>
          <p class="description">{{ c.description }}</p>
        </section>

        <section class="card">
          <h2>Risk <app-risk-badge [score]="c.riskScore" /></h2>
          @if (c.flaggedForReview) {
            <p class="flag-banner" data-testid="flagged">Auto-flagged for review</p>
          }
          @if (c.riskReasons.length) {
            <ul class="reasons">
              @for (r of c.riskReasons; track r) { <li>{{ r }}</li> }
            </ul>
          } @else {
            <p class="muted">No risk rules fired.</p>
          }
        </section>

        <section class="card" data-testid="actions-panel">
          <h2>Actions</h2>
          @if (c.availableActions.length === 0) {
            <p class="muted" data-testid="no-actions">No actions available for your role in this status.</p>
          } @else {
            <div class="action-buttons">
              @for (a of c.availableActions; track a) {
                <button
                  type="button"
                  class="btn {{ actions[a].tone }}"
                  [class.selected]="selected() === a"
                  (click)="choose(a)"
                  [attr.data-testid]="'action-' + a"
                >
                  {{ actions[a].label }}
                </button>
              }
            </div>
          }

          @if (selected(); as a) {
            <form [formGroup]="actionForm" (ngSubmit)="confirm(a)" class="action-form" novalidate>
              @if (a === 'Approve') {
                <label>
                  Approved amount (USD)
                  <input type="number" step="0.01" formControlName="approvedAmount" data-testid="approved-amount-input" />
                  <span class="hint">Leave as claimed amount or adjust. Approvals over 25,000 need an admin.</span>
                </label>
              }
              <label>
                Note {{ actions[a].requiresNote ? '(required)' : '(optional)' }}
                <textarea formControlName="note" rows="3" data-testid="action-note"></textarea>
              </label>
              @if (actionForm.controls.note.invalid && actionForm.controls.note.touched) {
                <span class="field-error">A note is required for this action.</span>
              }
              @if (actionError()) {
                <p class="alert" role="alert" data-testid="action-error">{{ actionError() }}</p>
              }
              <div class="actions">
                <button type="submit" class="btn primary" [disabled]="busy()" data-testid="confirm-action">
                  Confirm {{ actions[a].label.toLowerCase() }}
                </button>
                <button type="button" class="btn" (click)="selected.set(null)">Cancel</button>
              </div>
            </form>
          }
        </section>

        <section class="card">
          <h2>Evidence</h2>
          @if (c.evidence.length === 0) {
            <p class="muted">No evidence uploaded.</p>
          } @else {
            <ul class="files" data-testid="evidence-list">
              @for (e of c.evidence; track e.id) {
                <li>
                  <button type="button" class="link" (click)="open(e.id)">{{ e.fileName }}</button>
                  <span class="muted">{{ e.contentType }}, {{ size(e.sizeBytes) }}, {{ e.uploadedAt | date: 'short' }}</span>
                </li>
              }
            </ul>
          }
          @if (canUpload(c.status)) {
            <label class="upload">
              Add evidence (PDF, PNG, JPEG, max 10 MB)
              <input type="file" [accept]="accept" (change)="upload($event)" [disabled]="uploading()" data-testid="evidence-upload" />
            </label>
          }
          @if (uploadError()) {
            <p class="alert" role="alert" data-testid="upload-error">{{ uploadError() }}</p>
          }
        </section>
      </div>

      <section class="card">
        <h2>Audit history</h2>
        <ol class="timeline" data-testid="history">
          @for (ev of history(); track ev.id) {
            <li [attr.data-event]="ev.type" [attr.data-to]="ev.toStatus">
              <div class="when">{{ ev.occurredAt | date: 'medium' }}</div>
              <div>
                <strong>{{ describe(ev) }}</strong>
                <span class="muted"> by {{ ev.actorRole }}</span>
                @if (ev.note) {
                  <p class="note">{{ ev.note }}</p>
                }
              </div>
            </li>
          }
        </ol>
      </section>
    }
  `,
})
export class ClaimDetailComponent implements OnInit, OnChanges {
  /** Bound from the :id route parameter (withComponentInputBinding). */
  readonly id = input.required<string>();

  private readonly api = inject(ClaimsApi);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly actions = ACTIONS;
  readonly accept = EVIDENCE_ACCEPT;

  readonly claim = signal<ClaimDetail | null>(null);
  readonly history = signal<ClaimEvent[]>([]);
  readonly loadError = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly selected = signal<ClaimAction | null>(null);
  readonly busy = signal(false);
  readonly actionError = signal<string | null>(null);
  readonly uploading = signal(false);
  readonly uploadError = signal<string | null>(null);

  readonly actionForm = this.fb.group({
    note: this.fb.control<string>('', { nonNullable: true }),
    approvedAmount: this.fb.control<number | null>(null, [Validators.min(0.01), Validators.max(1_000_000)]),
  });

  ngOnInit(): void {
    const state = this.router.getCurrentNavigation()?.extras.state ?? window.history.state;
    const uploadErrors = (state as { uploadErrors?: string[] } | null)?.uploadErrors;
    if (uploadErrors?.length) {
      this.uploadError.set(`Claim filed, but some evidence was not uploaded. ${uploadErrors.join(' ')}`);
    }
  }

  /** Runs on first bind and whenever the route moves to another claim (the component is reused). */
  ngOnChanges(): void {
    this.claim.set(null);
    this.loadError.set(null);
    this.selected.set(null);
    this.notice.set(null);
    this.load();
  }

  load(): void {
    forkJoin({ claim: this.api.get(this.id()), history: this.api.history(this.id()) }).subscribe({
      next: ({ claim, history }) => {
        this.claim.set(claim);
        this.history.set(history);
      },
      error: (err: unknown) => this.loadError.set(describeError(err)),
    });
  }

  choose(action: ClaimAction): void {
    this.selected.set(action);
    this.actionError.set(null);
    const note = this.actionForm.controls.note;
    note.setValidators(ACTIONS[action].requiresNote ? [Validators.required, Validators.maxLength(2000)] : [Validators.maxLength(2000)]);
    this.actionForm.reset({ note: '', approvedAmount: action === 'Approve' ? (this.claim()?.claimedAmount ?? null) : null });
  }

  confirm(action: ClaimAction): void {
    if (this.actionForm.invalid) {
      this.actionForm.markAllAsTouched();
      return;
    }
    const { note, approvedAmount } = this.actionForm.getRawValue();
    this.busy.set(true);
    this.actionError.set(null);
    this.api
      .transition(this.id(), action, {
        note: note.trim() || null,
        approvedAmount: action === 'Approve' ? approvedAmount : null,
      })
      .subscribe({
        next: (claim) => {
          this.busy.set(false);
          this.selected.set(null);
          this.notice.set(`${ACTIONS[action].label}: claim is now ${STATUS_LABELS[claim.status].toLowerCase()}.`);
          this.claim.set(claim);
          this.api.history(this.id()).subscribe((h) => this.history.set(h));
        },
        error: (err: unknown) => {
          this.busy.set(false);
          this.actionError.set(describeError(err));
        },
      });
  }

  canUpload(status: ClaimStatus): boolean {
    return status === 'Submitted' || status === 'InReview' || status === 'MoreInfoNeeded';
  }

  upload(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    const problem = checkEvidence(file);
    if (problem) {
      this.uploadError.set(problem);
      input.value = '';
      return;
    }
    this.uploading.set(true);
    this.uploadError.set(null);
    this.api.uploadEvidence(this.id(), file).subscribe({
      next: () => {
        this.uploading.set(false);
        input.value = '';
        this.notice.set(`Uploaded ${file.name}.`);
        this.load();
      },
      error: (err: unknown) => {
        this.uploading.set(false);
        input.value = '';
        this.uploadError.set(describeError(err));
      },
    });
  }

  open(evidenceId: string): void {
    this.api.evidenceLink(this.id(), evidenceId).subscribe({
      next: (link) => window.open(link.url, '_blank', 'noopener'),
      error: (err: unknown) => this.uploadError.set(describeError(err)),
    });
  }

  size(bytes: number): string {
    return formatBytes(bytes);
  }

  describe(ev: ClaimEvent): string {
    switch (ev.type) {
      case 'Submitted':
        return 'Claim submitted';
      case 'StatusChanged':
        return `${ev.fromStatus ? STATUS_LABELS[ev.fromStatus] : '?'} -> ${ev.toStatus ? STATUS_LABELS[ev.toStatus] : '?'}`;
      case 'EvidenceAdded':
        return 'Evidence added';
      case 'RiskScored':
        return 'Risk scored';
      case 'SlaReminder':
        return 'SLA reminder';
      case 'StuckAlert':
        return 'Stuck-claim alert';
    }
  }
}
