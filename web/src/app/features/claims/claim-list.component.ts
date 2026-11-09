import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { startWith, switchMap, catchError, of, tap } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { ClaimsApi } from '../../core/claims-api.service';
import { ALL_STATUSES, STATUS_LABELS, describeError } from '../../core/labels';
import { ClaimStatus, ClaimSummary } from '../../core/models';
import { ClaimTableComponent } from './claim-table.component';

@Component({
  selector: 'app-claim-list',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, ClaimTableComponent],
  template: `
    <header class="page-head">
      <h1>{{ auth.hasRole('Claimant') ? 'My claims' : 'All claims' }}</h1>
      @if (auth.hasRole('Claimant')) {
        <a routerLink="/claims/new" class="btn primary" data-testid="new-claim">File a claim</a>
      }
    </header>

    <label class="inline">
      Status
      <select [formControl]="status" data-testid="status-filter">
        <option [ngValue]="null">All</option>
        @for (s of statuses; track s) {
          <option [ngValue]="s">{{ labels[s] }}</option>
        }
      </select>
    </label>

    @if (error()) {
      <p class="alert" role="alert">{{ error() }}</p>
    }
    <app-claim-table [claims]="claims() ?? []" emptyText="No claims match this filter." />
  `,
})
export class ClaimListComponent {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(ClaimsApi);

  readonly statuses = ALL_STATUSES;
  readonly labels = STATUS_LABELS;
  readonly status = new FormControl<ClaimStatus | null>(null);
  readonly error = signal<string | null>(null);

  readonly claims = toSignal<ClaimSummary[]>(
    this.status.valueChanges.pipe(
      startWith(this.status.value),
      tap(() => this.error.set(null)),
      switchMap((s) =>
        this.api.list(s).pipe(
          catchError((err: unknown) => {
            this.error.set(describeError(err));
            return of([] as ClaimSummary[]);
          }),
        ),
      ),
    ),
  );
}
