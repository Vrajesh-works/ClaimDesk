import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="topbar">
      <a class="brand" routerLink="/">ClaimDesk</a>
      @if (auth.user(); as u) {
        <nav aria-label="Main">
          @if (auth.hasRole('Adjuster', 'Admin')) {
            <a routerLink="/queue" routerLinkActive="active" data-testid="nav-queue">Queue</a>
          }
          <a routerLink="/claims" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }" data-testid="nav-claims">Claims</a>
          @if (auth.hasRole('Claimant')) {
            <a routerLink="/claims/new" routerLinkActive="active">File a claim</a>
          }
          @if (auth.hasRole('Adjuster', 'Admin')) {
            <a routerLink="/alerts" routerLinkActive="active">SLA alerts</a>
          }
          @if (auth.hasRole('Admin')) {
            <a routerLink="/admin/users" routerLinkActive="active">Users</a>
          }
        </nav>
        <div class="who">
          <span data-testid="current-user">{{ u.displayName }}</span>
          <span class="role">{{ u.role }}</span>
          <button type="button" class="btn small" (click)="auth.logout()" data-testid="logout">Sign out</button>
        </div>
      }
    </header>
    <main>
      <router-outlet />
    </main>
  `,
})
export class AppComponent {
  protected readonly auth = inject(AuthService);
}
