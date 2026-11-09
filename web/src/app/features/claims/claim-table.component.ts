import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ClaimSummary } from '../../core/models';
import { RiskBadgeComponent, StatusBadgeComponent } from '../../shared/status-badge.component';

@Component({
  selector: 'app-claim-table',
  standalone: true,
  imports: [RouterLink, CurrencyPipe, DatePipe, StatusBadgeComponent, RiskBadgeComponent],
  template: `
    @if (claims().length === 0) {
      <p class="empty">{{ emptyText() }}</p>
    } @else {
      <div class="table-wrap">
        <table data-testid="claim-table">
          <thead>
            <tr>
              <th>Claim</th>
              <th>Status</th>
              <th class="num">Risk</th>
              <th class="num">Amount</th>
              <th>Policy</th>
              <th>Submitted</th>
              <th>Last activity</th>
            </tr>
          </thead>
          <tbody>
            @for (c of claims(); track c.id) {
              <tr [attr.data-claim-number]="c.number">
                <td>
                  <a [routerLink]="['/claims', c.id]">{{ c.number }}</a>
                  @if (c.flaggedForReview) {
                    <span class="flag" title="Auto-flagged for review">flagged</span>
                  }
                </td>
                <td><app-status-badge [status]="c.status" /></td>
                <td class="num"><app-risk-badge [score]="c.riskScore" /></td>
                <td class="num">{{ c.claimedAmount | currency: 'USD' }}</td>
                <td>{{ c.policyNumber }}</td>
                <td>{{ c.submittedAt | date: 'mediumDate' }}</td>
                <td>{{ c.lastActivityAt | date: 'short' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
})
export class ClaimTableComponent {
  readonly claims = input.required<ClaimSummary[]>();
  readonly emptyText = input('No claims yet.');
}
