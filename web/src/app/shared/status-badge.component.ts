import { Component, computed, input } from '@angular/core';
import { STATUS_LABELS } from '../core/labels';
import { ClaimStatus } from '../core/models';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  template: `<span class="badge status-{{ status() }}" data-testid="status-badge">{{ label() }}</span>`,
})
export class StatusBadgeComponent {
  readonly status = input.required<ClaimStatus>();
  readonly label = computed(() => STATUS_LABELS[this.status()]);
}

@Component({
  selector: 'app-risk-badge',
  standalone: true,
  template: `<span class="risk risk-{{ level() }}" [title]="'Risk score ' + score()">{{ score() }}</span>`,
})
export class RiskBadgeComponent {
  readonly score = input.required<number>();
  readonly level = computed(() => (this.score() >= 50 ? 'high' : this.score() >= 25 ? 'medium' : 'low'));
}
