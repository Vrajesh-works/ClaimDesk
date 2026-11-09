import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { Role } from './models';

/**
 * Route guard by role. This is a usability check only; the API enforces every rule
 * with its own policies and resource-based authorization.
 */
export function roleGuard(...roles: Role[]): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.token()) {
      return router.createUrlTree(['/login']);
    }
    if (roles.length === 0 || auth.hasRole(...roles)) {
      return true;
    }
    return router.createUrlTree([auth.homeFor(auth.user()?.role)]);
  };
}
