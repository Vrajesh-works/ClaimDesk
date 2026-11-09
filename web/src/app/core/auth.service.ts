import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { LoginResponse, Role, UserInfo } from './models';

const STORAGE_KEY = 'claimdesk.session';

interface Session {
  token: string;
  expiresAt: string;
  user: UserInfo;
}

/**
 * Holds the local JWT session. With Entra ID this becomes a thin wrapper over MSAL;
 * the rest of the app only reads `user()`, `token()` and `hasRole()`.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly session = signal<Session | null>(AuthService.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isLoggedIn = computed(() => this.session() !== null);

  token(): string | null {
    const s = this.session();
    if (!s) {
      return null;
    }
    if (new Date(s.expiresAt).getTime() <= Date.now()) {
      this.clear();
      return null;
    }
    return s.token;
  }

  hasRole(...roles: Role[]): boolean {
    const role = this.user()?.role;
    return role !== undefined && roles.includes(role);
  }

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', { email, password }).pipe(
      tap((res) => {
        const s: Session = { token: res.accessToken, expiresAt: res.expiresAt, user: res.user };
        this.session.set(s);
        try {
          sessionStorage.setItem(STORAGE_KEY, JSON.stringify(s));
        } catch {
          // Storage can be unavailable (private mode); the in-memory session still works.
        }
      }),
    );
  }

  logout(): void {
    this.clear();
    void this.router.navigate(['/login']);
  }

  /** Landing page for a role after login. */
  homeFor(role: Role | undefined): string {
    return role === 'Adjuster' || role === 'Admin' ? '/queue' : '/claims';
  }

  private clear(): void {
    this.session.set(null);
    try {
      sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore
    }
  }

  private static restore(): Session | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return null;
      }
      const s = JSON.parse(raw) as Session;
      return new Date(s.expiresAt).getTime() > Date.now() ? s : null;
    } catch {
      return null;
    }
  }
}
