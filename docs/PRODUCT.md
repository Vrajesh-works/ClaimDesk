# ClaimDesk - Product Document

## Problem

Small and mid-size insurers and third-party administrators still move auto claims through
email threads, shared spreadsheets and PDF attachments. That causes three recurring problems:

1. **No reliable history.** Nobody can say who approved a claim, when, and why. Auditors and
   regulators ask for this, and the answer is usually "check the mailbox".
2. **Claims stall silently.** A claim waiting on an adjuster or on missing documents can sit for
   weeks before anyone notices, which hurts customers and breaches service-level agreements (SLAs).
3. **Review effort is spread evenly.** Adjusters work claims first-in-first-out, so a claim with
   classic fraud signals (brand-new policy, no police report, recent address change) gets the
   same attention as a routine fender-bender.

## Target users

| Role | Who | What they need |
| --- | --- | --- |
| Claimant | Policy holder filing an auto claim | File a claim in minutes, upload photos and documents, see status, answer requests for more information |
| Adjuster | Claims handler at the insurer | A queue ordered by risk, the full evidence and history for each claim, one-click transitions with notes |
| Admin | Claims supervisor / operations lead | Approve high-value claims, release payments, see SLA alerts and stuck claims |

## Core features (MVP, built today)

1. **Claim lifecycle state machine.** `Submitted -> InReview -> (MoreInfoNeeded <-> InReview) ->
   Approved | Denied`, then `Approved -> Paid`. Illegal transitions are rejected by the domain,
   not just hidden in the UI.
2. **Append-only audit trail.** Every transition, evidence upload, risk score and SLA alert writes
   an audit event with actor, role, timestamp, from/to status and note. Audit rows cannot be
   edited or deleted through the application.
3. **Role-based access.** Claimant, adjuster and admin roles enforced with ASP.NET Core
   policy-based and resource-based authorization (a claimant sees only their own claims). Every
   endpoint has an automated authorization test.
4. **Evidence upload.** PDF, JPEG and PNG files up to 10 MB, validated by file signature (magic
   bytes) as well as extension, stored behind an `IEvidenceStore` interface, downloaded through
   short-lived signed links (the local stand-in for Azure Blob SAS URLs).
5. **Rule-based risk flagging.** Explainable weighted rules over fields shaped like the Kaggle
   *Vehicle Claim Fraud Detection* dataset (fault, police report, witness, days between policy
   start and accident, address change, past claims, vehicle category, and so on). Each claim gets
   a 0-100 score, the list of rules that fired, and an "auto flag for review" bit.
6. **Adjuster queue sorted by risk.** Open claims ordered by risk score, then by age.
7. **SLA background worker.** A hosted service raises an SLA reminder when a submitted claim is
   not picked up in time and a stuck-claim alert when an open claim has had no activity for too
   long. Alerts are recorded in the audit trail and in an in-app notification feed.
8. **Angular 18 portal.** Reactive forms for login, filing a claim and adjuster actions; claim
   detail with evidence and history; adjuster queue.
9. **Operations basics.** Health checks (`/health/live`, `/health/ready`), OpenTelemetry traces
   and metrics (Aspire dashboard locally), Dockerfile per service, one-command `docker compose up`.

## Non-goals (for this MVP)

- Real cloud deployment (Azure App Service, Azure SQL, Blob Storage, Entra ID, Application
  Insights, Bicep/Terraform). The code is shaped so each is a later adapter; see
  [PLAN.md](PLAN.md#future-work).
- Payments integration. "Paid" is a status recorded by an admin, not a money movement.
- Machine-learning fraud models. Rules are deliberately explainable; an ML score is future work.
- Policy administration, underwriting, or premium calculation.
- Email/SMS delivery of notifications (notifications are in-app and logged).
- Multi-tenant isolation.

## Success metrics

For the MVP (verifiable today):

- 100% of API endpoints covered by an authorization test (enforced by a test that discovers
  endpoints and fails when one is missing from the matrix).
- The full lifecycle (file, upload evidence, review, request info, provide info, approve, pay)
  passes as a Playwright end-to-end test in CI.
- Every state change produces exactly one audit event; no API path can modify or delete one.
- CI green on every pull request: build, format check, unit tests, integration tests (including a
  real SQL Server via Testcontainers), Angular strict-template build and unit tests, E2E.

For a production pilot (how the product would be judged later):

- Median time from submission to first adjuster action under 1 business day.
- Fewer than 2% of open claims breaching the stuck-claim threshold in a week.
- Share of confirmed-fraud claims that were in the top risk decile of the queue (precision of the
  flagging rules against the labelled Kaggle data is the offline proxy).
