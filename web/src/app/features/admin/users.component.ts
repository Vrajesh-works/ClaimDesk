import { Component, OnInit, inject, signal } from '@angular/core';
import { ClaimsApi } from '../../core/claims-api.service';
import { describeError } from '../../core/labels';
import { UserInfo } from '../../core/models';

@Component({
  selector: 'app-users',
  standalone: true,
  template: `
    <h1>Users</h1>
    <p class="muted">Local demo accounts. With Entra ID, roles come from app role assignments instead.</p>
    @if (error()) {
      <p class="alert" role="alert">{{ error() }}</p>
    }
    <div class="table-wrap">
      <table>
        <thead>
          <tr><th>Name</th><th>Email</th><th>Role</th></tr>
        </thead>
        <tbody>
          @for (u of users(); track u.id) {
            <tr><td>{{ u.displayName }}</td><td>{{ u.email }}</td><td>{{ u.role }}</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class UsersComponent implements OnInit {
  private readonly api = inject(ClaimsApi);
  readonly users = signal<UserInfo[]>([]);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.users().subscribe({
      next: (u) => this.users.set(u),
      error: (err: unknown) => this.error.set(describeError(err)),
    });
  }
}
