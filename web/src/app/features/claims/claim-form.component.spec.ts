import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { Router, provideRouter } from '@angular/router';
import { ClaimFormComponent, notInFuture } from './claim-form.component';

describe('ClaimFormComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ClaimFormComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
  });

  it('starts invalid and does not call the API on an empty submit', () => {
    const fixture = TestBed.createComponent(ClaimFormComponent);
    fixture.detectChanges();
    const form = fixture.componentInstance.form;
    expect(form.valid).toBeFalse();
    fixture.componentInstance.submit();
    expect(fixture.componentInstance.error()).toBe('Fix the highlighted fields.');
    TestBed.inject(HttpTestingController).verify();
  });

  it('enforces the same limits as the API', () => {
    const c = TestBed.createComponent(ClaimFormComponent).componentInstance.form.controls;
    c.policyNumber.setValue('AB');
    expect(c.policyNumber.valid).toBeFalse();
    c.claimedAmount.setValue(0);
    expect(c.claimedAmount.valid).toBeFalse();
    c.claimedAmount.setValue(1_000_001);
    expect(c.claimedAmount.valid).toBeFalse();
    c.description.setValue('too short');
    expect(c.description.valid).toBeFalse();
    c.vehicleYear.setValue(1979);
    expect(c.vehicleYear.valid).toBeFalse();
  });

  it('rejects incident dates in the future', () => {
    const tomorrow = new Date(Date.now() + 2 * 86_400_000).toISOString().slice(0, 10);
    expect(notInFuture(new FormControl(tomorrow) as never)).toEqual({ future: true });
    expect(notInFuture(new FormControl('2024-05-01') as never)).toBeNull();
  });

  it('files the claim and opens it', () => {
    const fixture = TestBed.createComponent(ClaimFormComponent);
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    const cmp = fixture.componentInstance;
    cmp.form.patchValue({
      policyNumber: 'POL-1',
      incidentDate: '2024-05-01',
      claimedAmount: 1200,
      description: 'Hail damage on the roof and hood.',
      make: 'Toyota',
    });
    cmp.submit();

    const req = TestBed.inject(HttpTestingController).expectOne('/api/claims');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.category).toBe('Sedan');
    req.flush({ id: 'abc' });
    expect(router.navigate).toHaveBeenCalledWith(['/claims', 'abc'], { state: { uploadErrors: [] } });
  });
});
