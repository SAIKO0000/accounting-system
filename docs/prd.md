# Product Requirements Document

## Product Vision

Build a modern private-cloud accounting system for Gentrimed-style healthcare
finance operations that preserves trusted accounting workflows while improving
accessibility, auditability, reporting speed, security, and maintainability.

## Problem Statement

The current accounting operation depends on a 2006-era Windows desktop system,
Jet database files, workgroup security files, Crystal Reports, local/network
file locking, and recurring spreadsheet-based reporting. The system remains
business-critical, but it is difficult to secure, extend, audit, integrate, and
operate reliably as a modern financial platform.

## Target Users

- Finance and accounting staff
- Bookkeepers
- Finance managers
- Executives reviewing financial reports
- Auditors
- System administrators
- Future reporting and integration users

## Goals

- Replace the live Sage workflow for core finance.
- Enforce double-entry accounting for every posted transaction.
- Preserve historical accuracy through validated migration.
- Improve financial reporting, exports, and drill-down.
- Provide secure private-cloud browser access.
- Support audit trails, controlled reversals, approvals, and accountability.
- Reduce dependence on manual spreadsheets for recurring financial statements.

## Non-Goals For V1

- Full payroll replacement
- Full inventory replacement
- Multi-tenant SaaS commercialization
- AI-driven accounting automation
- Broad third-party marketplace
- Exact replacement of every legacy report and form
- Storage or processing of cardholder data

## V1 Feature Scope

- Company setup and fiscal periods
- Chart of accounts
- General journal entry
- Accounts payable
- Accounts receivable
- Vendor and customer masters
- Bank accounts and bank reconciliation
- Tax-code support for the required Philippine healthcare/accounting context
- Financial statements:
  - Trial balance
  - General ledger
  - Balance sheet
  - Income statement
  - Aged payables
  - Aged receivables
- User management and role-based access control
- Immutable audit log
- Reversal and correction workflow
- Legacy data migration and reconciliation reports
- Excel and PDF export

## High-Level Workflows

### Post Journal Entry

An authorized user drafts a journal entry, adds two or more journal lines, and
submits it for posting. The posting engine validates period status, account
status, debit/credit balance, permissions, and required metadata. Once posted,
the journal becomes immutable.

### Correct Posted Journal Entry

An authorized user creates a reversal of the original posted journal. The system
creates an equal and opposite posted entry and optionally links a replacement
entry. The original entry is never edited in place.

### Record Vendor Bill

An AP user records a vendor bill, selects expense or asset accounts, applies tax
codes where relevant, and posts the transaction. The system updates AP aging and
creates the required GL entry.

### Record Vendor Payment

An AP user selects open vendor bills, chooses a bank account, records payment
details, and posts the payment. The system updates AP balances and records the
bank-side entry.

### Record Customer Invoice

An AR user records a customer invoice, revenue lines, tax codes, and due date.
The system posts the AR and revenue/tax movement and updates receivables aging.

### Record Customer Receipt

An AR user applies a receipt against one or more open invoices. The system posts
cash/bank and AR movements.

### Reconcile Bank Account

An authorized user imports or enters statement lines, matches them to posted
transactions, records adjustments if needed, and closes a reconciliation period.

### Generate Financial Reports

Finance users generate trial balance, balance sheet, income statement, aged AP,
aged AR, and GL detail reports for selected periods. Reports must support
drill-down to source transactions and export to Excel/PDF.

## Functional Requirements

- The system must support multiple companies only if migration confirms more
  than one active operating company is required.
- Every posted accounting transaction must produce balanced debit and credit
  lines in base currency.
- Posted journals must be immutable.
- Closed periods must block normal posting.
- Corrections must use reversals and replacement entries.
- All financial mutations must require authenticated users and server-side
  authorization checks.
- Every create, update, post, reverse, import, export, permission change, and
  failed authorization must be auditable.
- Reports must be reproducible for a given period and filter set.

## Technical Requirements

- PostgreSQL is the system of record.
- Money must use fixed precision decimal types.
- Database constraints must enforce core accounting invariants where feasible.
- The posting engine must run inside a single database transaction.
- The application must expose an internal API for the web client.
- External APIs and webhooks are deferred until internal workflows stabilize.
- Background jobs must support migration, exports, scheduled reports, and
  reconciliation processing.
- The audit log must be append-only from application code.

## Security And Compliance Requirements

- OIDC/OAuth 2.0 authentication with MFA capability.
- Role-based permissions by module and action.
- Segregation of duties for admin, posting, approval, reporting, and migration
  where practical.
- Encryption in transit and at rest.
- Secrets managed outside application code.
- Security logging for authentication, authorization, financial mutations,
  exports, imports, and admin changes.
- Secure SDLC practices aligned to NIST SSDF.
- Application verification aligned to OWASP ASVS.
- SOC 2-style control mapping for security, availability, processing integrity,
  confidentiality, and privacy.
- PCI DSS remains out of scope unless cardholder data handling is intentionally
  introduced later.

## Migration Requirements

- Use authorized access to production `.SDB/.SDW` files or approved Sage export
  tooling.
- Extract company data, users, accounts, opening balances, journals, vendors,
  customers, AP, AR, tax records, bank reconciliation data, and report metadata
  where available.
- Preserve legacy table names and IDs as external references.
- Reconcile migrated trial balance, balance sheet, income statement, aged AP,
  aged AR, and GL detail against accepted Sage reports and monthly workbooks.
- Keep original Sage files read-only as historical records.

## Success Metrics

- Trial balance matches legacy source after migration.
- Balance sheet and income statement reconcile to accepted reports.
- AP and AR aging totals reconcile to accepted reports.
- Users can complete GL, AP, AR, bank reconciliation, and reporting workflows
  without Sage for v1 scope.
- Month-end reporting time decreases.
- Audit trail answers who, what, when, where, and why for posted transactions.
- Access-control tests show no unauthorized financial mutations.
- Backup and restore tests pass.
