import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, concatMap, from, map, of, switchMap, toArray } from 'rxjs';
import { ClaimsApi } from '../../core/claims-api.service';
import { EVIDENCE_ACCEPT, checkEvidence, formatBytes } from '../../core/evidence-rules';
import { OPTIONS, describeError } from '../../core/labels';
import { FileClaimRequest } from '../../core/models';

/** Incident date must be a real date that is not in the future. */
export function notInFuture(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value;
  if (!value) {
    return null;
  }
  const date = new Date(`${value}T00:00:00`);
  if (Number.isNaN(date.getTime())) {
    return { date: true };
  }
  const endOfToday = new Date();
  endOfToday.setHours(23, 59, 59, 999);
  return date > endOfToday ? { future: true } : null;
}

const CURRENT_YEAR = new Date().getFullYear();

@Component({
  selector: 'app-claim-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <header class="page-head">
      <h1>File a claim</h1>
      <a routerLink="/claims" class="btn">Cancel</a>
    </header>

    <form [formGroup]="form" (ngSubmit)="submit()" novalidate class="card" data-testid="claim-form">
      <fieldset>
        <legend>Incident</legend>
        <div class="grid">
          <label>
            Policy number
            <input formControlName="policyNumber" placeholder="POL-123456" data-testid="policyNumber" />
            @if (invalid('policyNumber')) {
              <span class="field-error">3 to 30 characters.</span>
            }
          </label>
          <label>
            Incident date
            <input type="date" formControlName="incidentDate" [max]="today" data-testid="incidentDate" />
            @if (invalid('incidentDate')) {
              <span class="field-error">
                {{ form.controls.incidentDate.hasError('future') ? 'Date cannot be in the future.' : 'Pick the incident date.' }}
              </span>
            }
          </label>
          <label>
            Claimed amount (USD)
            <input type="number" formControlName="claimedAmount" min="1" step="0.01" data-testid="claimedAmount" />
            @if (invalid('claimedAmount')) {
              <span class="field-error">Between 1 and 1,000,000.</span>
            }
          </label>
        </div>
        <label>
          What happened
          <textarea formControlName="description" rows="4" data-testid="description"></textarea>
          <span class="hint">{{ form.controls.description.value.length }} / 2000</span>
          @if (invalid('description')) {
            <span class="field-error">Describe the incident in at least 10 characters.</span>
          }
        </label>
      </fieldset>

      <fieldset>
        <legend>Vehicle and policy</legend>
        <div class="grid">
          <label>
            Make
            <input formControlName="make" placeholder="Honda" data-testid="make" />
            @if (invalid('make')) {
              <span class="field-error">Make is required.</span>
            }
          </label>
          <label>
            Vehicle year
            <input type="number" formControlName="vehicleYear" data-testid="vehicleYear" />
            @if (invalid('vehicleYear')) {
              <span class="field-error">Between 1980 and {{ maxYear }}.</span>
            }
          </label>
          <label>
            Category
            <select formControlName="category" data-testid="category">
              @for (o of options.category; track o.value) { <option [value]="o.value">{{ o.label }}</option> }
            </select>
          </label>
          <label>
            Base policy
            <select formControlName="basePolicy" data-testid="basePolicy">
              @for (o of options.basePolicy; track o.value) { <option [value]="o.value">{{ o.label }}</option> }
            </select>
          </label>
          <label>
            Accident area
            <select formControlName="accidentArea" data-testid="accidentArea">
              @for (o of options.accidentArea; track o.value) { <option [value]="o.value">{{ o.label }}</option> }
            </select>
          </label>
          <label>
            At fault
            <select formControlName="fault" data-testid="fault">
              @for (o of options.fault; track o.value) { <option [value]="o.value">{{ o.label }}</option> }
            </select>
          </label>
          <label>
            Agent
            <select formControlName="agentType" data-testid="agentType">
              @for (o of options.agentType; track o.value) { <option [value]="o.value">{{ o.label }}</option> }
            </select>
          </label>
          <label>
            Policy start to accident
            <select formControlName="daysPolicyAccident" data-testid="daysPolicyAccident">
              @for (o of options.daysPolicyAccident; track o.value) { <option [value]="o.value">{{ o.label }}</option> }
            </select>
          </label>
          <label>
            Last address change
            <select formControlName="addressChange" data-testid="addressChange">
              @for (o of options.addressChange; track o.value) { <option [value]="o.value">{{ o.label }}</option> }
            </select>
          </label>
          <label>
            Past claims
            <select formControlName="pastClaims" data-testid="pastClaims">
              @for (o of options.pastClaims; track o.value) { <option [value]="o.value">{{ o.label }}</option> }
            </select>
          </label>
        </div>
        <div class="checks">
          <label class="check"><input type="checkbox" formControlName="policeReportFiled" data-testid="policeReportFiled" /> Police report filed</label>
          <label class="check"><input type="checkbox" formControlName="witnessPresent" data-testid="witnessPresent" /> Witness present</label>
        </div>
      </fieldset>

      <fieldset>
        <legend>Evidence (optional)</legend>
        <p class="muted small">PDF, PNG or JPEG, up to 10 MB each. You can add more later.</p>
        <input type="file" multiple [accept]="accept" (change)="pickFiles($event)" data-testid="evidence-input" />
        @if (fileError()) {
          <span class="field-error" data-testid="evidence-error">{{ fileError() }}</span>
        }
        @if (files().length) {
          <ul class="files">
            @for (f of files(); track f.name) {
              <li>{{ f.name }} <span class="muted">({{ size(f.size) }})</span></li>
            }
          </ul>
        }
      </fieldset>

      @if (error()) {
        <p class="alert" role="alert" data-testid="form-error">{{ error() }}</p>
      }
      <div class="actions">
        <button type="submit" class="btn primary" [disabled]="busy()" data-testid="submit-claim">
          {{ busy() ? 'Submitting...' : 'Submit claim' }}
        </button>
      </div>
    </form>
  `,
})
export class ClaimFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ClaimsApi);
  private readonly router = inject(Router);

  readonly options = OPTIONS;
  readonly accept = EVIDENCE_ACCEPT;
  readonly maxYear = CURRENT_YEAR + 1;
  readonly today = new Date().toISOString().slice(0, 10);

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly fileError = signal<string | null>(null);
  readonly files = signal<File[]>([]);

  readonly form = this.fb.nonNullable.group({
    policyNumber: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(30)]],
    incidentDate: ['', [Validators.required, notInFuture]],
    claimedAmount: [0, [Validators.required, Validators.min(1), Validators.max(1_000_000)]],
    description: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(2000)]],
    make: ['', [Validators.required, Validators.maxLength(40)]],
    vehicleYear: [CURRENT_YEAR - 3, [Validators.required, Validators.min(1980), Validators.max(CURRENT_YEAR + 1)]],
    category: ['Sedan' as FileClaimRequest['category']],
    accidentArea: ['Urban' as FileClaimRequest['accidentArea']],
    fault: ['ThirdParty' as FileClaimRequest['fault']],
    basePolicy: ['Collision' as FileClaimRequest['basePolicy']],
    agentType: ['Internal' as FileClaimRequest['agentType']],
    policeReportFiled: [true],
    witnessPresent: [false],
    daysPolicyAccident: ['MoreThanThirty' as FileClaimRequest['daysPolicyAccident']],
    addressChange: ['NoChange' as FileClaimRequest['addressChange']],
    pastClaims: ['None' as FileClaimRequest['pastClaims']],
  });

  invalid(name: keyof typeof this.form.controls): boolean {
    const c = this.form.controls[name];
    return c.invalid && (c.touched || c.dirty);
  }

  size(bytes: number): string {
    return formatBytes(bytes);
  }

  pickFiles(event: Event): void {
    const input = event.target as HTMLInputElement;
    const picked = Array.from(input.files ?? []);
    const problem = picked.map((f) => (checkEvidence(f) ? `${f.name}: ${checkEvidence(f)}` : null)).find((m) => m);
    this.fileError.set(problem ?? null);
    this.files.set(problem ? [] : picked);
    if (problem) {
      input.value = '';
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.error.set('Fix the highlighted fields.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const request: FileClaimRequest = this.form.getRawValue();
    const files = this.files();

    this.api
      .file(request)
      .pipe(
        // The claim exists once filed, so an upload failure must not keep the user on this form
        // (resubmitting would file a duplicate). Failed uploads are reported on the claim page.
        switchMap((claim) =>
          from(files).pipe(
            concatMap((f) =>
              this.api.uploadEvidence(claim.id, f).pipe(
                map(() => null),
                catchError((err: unknown) => of(`${f.name}: ${describeError(err)}`)),
              ),
            ),
            toArray(),
            map((results) => ({ claim, uploadErrors: results.filter((r): r is string => r !== null) })),
          ),
        ),
      )
      .subscribe({
        next: ({ claim, uploadErrors }) =>
          void this.router.navigate(['/claims', claim.id], { state: { uploadErrors } }),
        error: (err: unknown) => {
          this.error.set(describeError(err));
          this.busy.set(false);
        },
      });
  }
}
