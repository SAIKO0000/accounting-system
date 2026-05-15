# Test And Acceptance Plan

## Purpose

This plan defines the minimum test coverage required before the upgraded system
can replace the legacy Sage workflow for v1 core finance.

## Schema And Database Tests

- Apply `database/postgres-core-finance-schema.sql` to a clean PostgreSQL
  database.
- Run `tools/test-postgres-schema.ps1` against a disposable PostgreSQL database.
- Verify all schemas, types, tables, constraints, indexes, roles, and
  permissions are created.
- Confirm money columns use fixed precision numeric types.
- Confirm invalid journal lines are rejected:
  - negative debit,
  - negative credit,
  - both debit and credit populated,
  - neither debit nor credit populated.
- Confirm duplicate account codes are rejected within a company.
- Confirm duplicate journal numbers are rejected within a company.

## Posting Engine Tests

- Post a balanced two-line journal.
- Post a balanced multi-line journal.
- Reject a journal where total debit does not equal total credit.
- Reject a journal with fewer than two lines.
- Reject posting to inactive accounts.
- Reject posting into closed or locked periods.
- Reject posting by a user without `journal.post`.
- Confirm posted journals cannot be edited.
- Reverse a posted journal and confirm an equal and opposite journal is created.
- Confirm reversal links to the original journal.

## AP Tests

- Create a vendor.
- Record a vendor bill and verify the AP document and posted journal are created.
- Record a vendor payment and verify open amount decreases.
- Reject payment greater than open amount.
- Confirm AP detail reconciles to GL control account.

## AR Tests

- Create a customer.
- Record a customer invoice and verify the AR document and posted journal are created.
- Record a customer receipt and verify open amount decreases.
- Reject receipt greater than open amount.
- Confirm AR detail reconciles to GL control account.

## Bank Reconciliation Tests

- Create a bank account.
- List bank reconciliation candidate lines for posted bank journal lines.
- Match posted bank journal lines to statement lines.
- Record bank reconciliation adjustment through controlled posting.
- Close a reconciliation.
- Reject edits to a closed reconciliation except through a controlled reopen or
  reversal workflow.

## Reporting Tests

- Generate trial balance for a period and confirm total debit equals total
  credit.
- Generate general ledger detail and confirm opening, movement, and closing
  balances.
- Generate balance sheet and income statement for agreed periods.
- Generate aged AP and aged AR.
- Confirm aged AP and aged AR bucket current open documents correctly.
- Export reports to Excel and PDF.
- Confirm report parameters and run metadata are recorded.

## Migration Tests

- Run the read-only Sage inspection script against an authorized company file.
- Import authorized CSV extracts into raw migration staging.
- Preview and apply staged chart-of-accounts transform with explicit account
  class mapping.
- Preview and apply staged customer/vendor transforms.
- Preview and apply staged journal transform with explicit signed-amount
  polarity.
- Extract row counts for core tables and compare to migration staging counts.
- Migrate chart of accounts and preserve legacy IDs.
- Migrate journals and preserve legacy references.
- Reconcile migrated trial balance against Sage.
- Reconcile migrated balance sheet and income statement against accepted
  reports.
- Reconcile AP and AR aging totals against accepted reports.
- Classify every variance as mapping issue, report option difference, manual
  workbook adjustment, rounding/currency difference, or unresolved.

## Security Tests

- Confirm unauthenticated users cannot access financial data.
- Confirm users without permissions cannot post, reverse, import, export, or
  administer settings.
- Confirm authentication success and failure events are logged.
- Confirm authorization failures are logged.
- Confirm unknown local development identities receive `401` and write an
  `authorization_failure` audit event.
- Confirm financial mutations create audit events.
- Confirm report views and exports create audit events.
- Confirm audit events cannot be modified through normal application paths.
- Confirm exported files do not include secrets or masked sensitive fields.

## Cutover Acceptance

The new system is ready for v1 cutover only when:

- Finance signs off on canonical report reconciliation.
- Core GL, AP, AR, bank reconciliation, and reporting workflows pass user
  acceptance testing.
- Backup and restore tests pass.
- Security acceptance tests pass.
- Legacy Sage files are archived read-only.
- A rollback plan exists for the cutover window.
