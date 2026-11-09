import { Component, OnInit, inject, signal } from '@angular/core';
import { ClaimsApi } from '../../core/claims-api.service';
import { describeError } from '../../core/labels';
import { ClaimSummary } from '../../core/models';
import { ClaimTableComponent } from '../claims/claim-table.component';

@Component({
  selector: 'app-queue',
  standalone: true,
  imports: [ClaimTableComponent],
  template: `
    <header class="page-head">
      <h1>Review queue</h1>
      <button type="button" class="btn" (click)="load()">Refresh</button>
    </header>
    <p class="muted">Open claims, highest risk first, then oldest. Scores of 50 or more are auto-flagged.</p>
    @if (error()) {
      <p class="alert" role="alert">{{ error() }}</p>
    }
    <app-claim-table [claims]="claims()" emptyText="The queue is empty." />
  `,
})
export class QueueComponent implements OnInit {
  private readonly api = inject(ClaimsApi);
  readonly claims = signal<ClaimSummary[]>([]);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.error.set(null);
    this.api.queue().subscribe({
      next: (c) => this.claims.set(c),
      error: (err: unknown) => this.error.set(describeError(err)),
    });
  }
}
