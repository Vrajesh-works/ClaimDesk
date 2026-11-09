import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { roleGuard } from './auth.guard';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { LoginResponse, Role } from './models';

function loginAs(role: Role, http: HttpTestingController, auth: AuthService): void {
  auth.login('u@claimdesk.local', 'pw').subscribe();
  const res: LoginResponse = {
    accessToken: 'token-123',
    expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    user: { id: '1', email: 'u@claimdesk.local', displayName: 'U', role },
  };
  http.expectOne('/api/auth/login').flush(res);
}

describe('auth', () => {
  let http: HttpTestingController;
  let auth: AuthService;
  let client: HttpClient;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    client = TestBed.inject(HttpClient);
  });

  afterEach(() => http.verify());

  it('attaches the bearer token to API calls only', () => {
    loginAs('Claimant', http, auth);
    client.get('/api/claims').subscribe();
    client.get('/assets/x.json').subscribe();
    expect(http.expectOne('/api/claims').request.headers.get('Authorization')).toBe('Bearer token-123');
    expect(http.expectOne('/assets/x.json').request.headers.has('Authorization')).toBeFalse();
  });

  it('signs out when the API returns 401', () => {
    loginAs('Adjuster', http, auth);
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    client.get('/api/queue').subscribe({ error: () => undefined });
    http.expectOne('/api/queue').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(auth.isLoggedIn()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('role guard sends anonymous users to login and wrong roles home', () => {
    const run = (...roles: Role[]) =>
      TestBed.runInInjectionContext(() => roleGuard(...roles)({} as never, {} as never)) as boolean | UrlTree;

    expect((run('Adjuster') as UrlTree).toString()).toBe('/login');

    loginAs('Claimant', http, auth);
    expect(run('Claimant')).toBeTrue();
    expect((run('Adjuster', 'Admin') as UrlTree).toString()).toBe('/claims');
  });
});
