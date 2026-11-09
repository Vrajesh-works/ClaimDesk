import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { describeError } from '../../core/labels';

interface DemoAccount {
  label: string;
  email: string;
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <section class="card narrow">
      <h1>Sign in</h1>
      <p class="muted">Local demo sign-in. In Azure this page is replaced by Microsoft Entra ID.</p>

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <label>
          Email
          <input type="email" formControlName="email" autocomplete="username" data-testid="login-email" />
        </label>
        @if (form.controls.email.touched && form.controls.email.invalid) {
          <span class="field-error">Enter a valid email.</span>
        }

        <label>
          Password
          <input type="password" formControlName="password" autocomplete="current-password" data-testid="login-password" />
        </label>
        @if (form.controls.password.touched && form.controls.password.invalid) {
          <span class="field-error">Password is required.</span>
        }

        @if (error()) {
          <p class="alert" role="alert" data-testid="login-error">{{ error() }}</p>
        }

        <button type="submit" class="btn primary" [disabled]="busy()" data-testid="login-submit">
          {{ busy() ? 'Signing in...' : 'Sign in' }}
        </button>
      </form>

      <h2>Demo accounts</h2>
      <p class="muted small">Password for every demo account: <code>{{ demoPassword }}</code></p>
      <div class="demo-accounts">
        @for (a of demoAccounts; track a.email) {
          <button type="button" class="btn" (click)="useDemo(a)">{{ a.label }}</button>
        }
      </div>
    </section>
  `,
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly demoPassword = 'ClaimDesk!2026';
  readonly demoAccounts: DemoAccount[] = [
    { label: 'Claimant', email: 'claimant@claimdesk.local' },
    { label: 'Adjuster', email: 'adjuster@claimdesk.local' },
    { label: 'Admin', email: 'admin@claimdesk.local' },
  ];

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  useDemo(account: DemoAccount): void {
    this.form.setValue({ email: account.email, password: this.demoPassword });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: (res) => void this.router.navigateByUrl(this.auth.homeFor(res.user.role)),
      error: (err: unknown) => {
        this.error.set(describeError(err));
        this.busy.set(false);
      },
    });
  }
}
