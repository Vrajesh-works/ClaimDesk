import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ClaimsApi } from '../../core/claims-api.service';
import { describeError } from '../../core/labels';
import { NotificationItem } from '../../core/models';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [DatePipe, RouterLink],
  template: `
    <header class="page-head">
      <h1>SLA alerts</h1>
      <button type="button" class="btn" (click)="load()">Refresh</button>
    </header>
    <p class="muted">Raised by the background SLA worker: reminders for claims waiting too long and alerts for stuck claims.</p>
    @if (error()) {
      <p class="alert" role="alert">{{ error() }}</p>
    }
    @if (items().length === 0) {
      <p class="empty">No alerts.</p>
    } @else {
      <ul class="alerts">
        @for (n of items(); track n.id) {
          <li class="alert-item kind-{{ n.kind }}">
            <span class="badge">{{ n.kind }}</span>
            <a [routerLink]="['/claims', n.claimId]">{{ n.message }}</a>
            <span class="muted">{{ n.createdAt | date: 'short' }} - for {{ n.recipientRole }}</span>
          </li>
        }
      </ul>
    }
  `,
})
export class NotificationsComponent implements OnInit {
  private readonly api = inject(ClaimsApi);
  readonly items = signal<NotificationItem[]>([]);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.error.set(null);
    this.api.notifications().subscribe({
      next: (n) => this.items.set(n),
      error: (err: unknown) => this.error.set(describeError(err)),
    });
  }
}
