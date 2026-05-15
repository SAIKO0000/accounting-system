# Implementation Roadmap

## Phase 0: Discovery And Access

- Confirm active production company files.
- Obtain authorized read-only access or approved Sage exports.
- Identify canonical month-end reports.
- Interview finance users for GL, AP, AR, bank reconciliation, and reporting.
- Confirm Philippine healthcare reporting and tax requirements with accounting
  stakeholders.

## Phase 1: Foundation

- Create the PostgreSQL schema for company, fiscal periods, accounts, journals,
  users, roles, audit log, AP, AR, and bank reconciliation.
- Implement authentication integration.
- Implement RBAC and server-side permission checks.
- Implement the posting engine with double-entry validation.
- Implement immutable posted journals and reversal workflow.
- Add structured logs and audit events.

## Phase 2: Core Finance Workflows

- Chart of accounts management.
- Journal draft and posting workflow.
- Vendor master and vendor bill/payment workflows.
- Customer master and invoice/receipt workflows.
- Bank account and reconciliation workflows.
- Core reports and exports.

## Phase 3: Migration Proof Of Concept

- Extract sample authorized data.
- Load chart of accounts and opening balances.
- Load journals and subledger records.
- Generate trial balance, balance sheet, income statement, aged AP, and aged AR.
- Compare against Sage outputs and document differences.

## Phase 4: Parallel Run

- Run migration repeatedly from frozen or controlled snapshots.
- Finance users compare reports for at least one month-end cycle.
- Resolve report and data differences.
- Train users on v1 workflows.
- Prepare cutover checklist and rollback plan.

## Phase 5: Cutover

- Freeze legacy posting at agreed cutoff.
- Run final migration.
- Validate balances and core reports.
- Set new system as source of truth for v1 scope.
- Retain legacy Sage files as read-only historical archive.

## Post-V1 Candidates

- Payroll
- Inventory
- Project accounting
- Additional statutory reports
- Bank-feed integrations
- External APIs and webhooks
- BI dashboards
- Advanced approval workflows
