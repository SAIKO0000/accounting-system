# Security And Compliance Requirements

## Security Baseline

The upgraded system handles financial records and must be designed as a
high-integrity internal business application.

Baseline references:

- OWASP ASVS for application security verification.
- OWASP authentication, authorization, and logging cheat sheets.
- NIST Secure Software Development Framework.
- SOC 2 Trust Services Criteria as a control model.
- PCI DSS only if future scope includes cardholder data.

## Authentication

- Use OIDC/OAuth 2.0 through a managed identity provider or Keycloak.
- Support MFA.
- Do not store passwords directly in the accounting application.
- Session expiration and reauthentication must be enforced for sensitive
  actions.
- Authentication successes and failures must be logged.
- Current local development API uses `X-Accounting-User` with seeded database
  users and roles. This is not production authentication and must be replaced by
  OIDC/MFA before real deployment.

## Authorization

Permissions must be checked server-side for every financial action.

Minimum v1 roles:

- System administrator
- Finance administrator
- GL accountant
- AP clerk
- AR clerk
- Bank reconciliation user
- Report viewer
- Auditor
- Migration operator

Permissions must distinguish:

- View
- Create draft
- Edit draft
- Post
- Reverse
- Approve
- Export
- Import
- Administer users
- Administer accounting settings

## Audit Trail

The accounting audit log is separate from operational logs.

Audit events must capture:

- Actor user ID
- Role or permission context
- Company ID
- Event type
- Entity type and ID
- Timestamp
- Source IP or session ID where available
- Before/after values for settings and master-data changes
- Reason/comment for reversals and privileged changes

Audit events are append-only from application code.

## Data Protection

- Encrypt traffic with TLS.
- Encrypt database and backups at rest.
- Store secrets in a managed secret store.
- Restrict production database access.
- Use least privilege for application database users.
- Avoid storing cardholder data in v1.
- Mask sensitive fields in logs and exports where required.

## Availability And Recovery

- Managed PostgreSQL backups with point-in-time recovery.
- Tested restore procedure.
- Export archive retention policy.
- Monitoring for failed jobs, failed backups, and database errors.
- Administrative procedure for emergency read-only access.

## Secure Development

- Dependency scanning.
- Static analysis.
- Code review for financial and authorization changes.
- Automated tests for posting invariants.
- Migration dry runs before production import.
- Security acceptance tests mapped to OWASP ASVS controls.

## Processing Integrity Controls

- Double-entry balance validation.
- Immutable posted journals.
- Closed-period posting restrictions.
- Controlled reversal workflow.
- Import validation and reconciliation summaries.
- Report generation reproducibility.
