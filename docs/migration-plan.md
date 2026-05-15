# Migration Plan

## Objective

Move legacy Sage accounting data into the modern PostgreSQL model without losing
historical traceability or financial accuracy.

## Source Systems

Primary sources:

- Production `.SDB` company database files.
- Paired `.SDW` workgroup/security files.
- Sage-generated reports and exports.
- Monthly finance workbooks.

Supporting sources:

- `Forms/SCHEMA.INI` export definitions.
- `Manuals/DataDict.pdf` database dictionary.
- Template `.TPL` chart-of-account files.

## Required Access

Migration requires authorized access to the active production company files or
approved Sage export tooling. The migration process must not attempt to bypass
credentials or modify production Sage files.

See `docs/legacy-data-inventory.md` for the current file inventory and
inspection status.

## Migration Principles

- Treat legacy data as read-only.
- Preserve legacy IDs and table names as external references.
- Normalize accounting data into explicit debit/credit journal lines.
- Keep raw extracted data for audit and troubleshooting.
- Reconcile outputs before declaring the new system authoritative.
- Do not infer Sage posting semantics without report validation.

## Extraction Scope

Extract these areas first:

- Company identity and fiscal dates
- Users and permissions
- Chart of accounts
- Linked system accounts
- Currencies and exchange rates
- General journal headers and lines
- Tax detail
- Customers and AR transactions
- Vendors and AP transactions
- Bank accounts and reconciliation records
- Report options and key exported report outputs

Defer these until core finance is reconciled:

- Payroll
- Inventory
- Detailed project accounting
- Custom form layouts
- Non-core Crystal Reports

## Validation Reports

Select and lock a canonical validation set before migration implementation:

- Trial balance
- General ledger detail
- Balance sheet
- Income statement
- Aged payables
- Aged receivables
- Bank reconciliation summary
- Cash ledger
- Any board-approved monthly finance workbook tabs used for reporting

## Migration Stages

1. Inventory active company files and identify the system of record.
2. Obtain authorized read-only database or export access.
3. Extract raw tables to a staging area.
4. Profile source data for row counts, date ranges, account ranges, currencies,
   fiscal periods, and locked/reconciled periods.
5. Map legacy accounts, customers, vendors, tax codes, journals, and subledger
   records to the normalized PostgreSQL model.
6. Load master data.
7. Load opening balances and historical journals.
8. Load AP, AR, and bank reconciliation data.
9. Generate validation reports from the new system.
10. Compare results to canonical Sage reports and finance workbooks.
11. Investigate and document differences.
12. Repeat until finance signs off.

## Reconciliation Rules

- The migrated trial balance must match the accepted legacy trial balance for
  selected cutoff dates.
- Balance sheet and income statement totals must match accepted reports.
- AP and AR aging totals must match accepted reports by vendor/customer and
  total.
- Any manual workbook adjustment must be explicitly identified as either:
  - a transaction to migrate,
  - a reporting-only adjustment, or
  - an excluded historical note.

## Cutover Strategy

Use parallel run before cutover:

- Legacy Sage remains source of truth during migration validation.
- New system imports a frozen or repeated snapshot.
- Finance users compare reports for at least one month-end cycle.
- Cutover occurs only after written signoff on core finance reports.

## Open Issues

- Production database credentials are not yet available.
- The active Gentrimed company file must be confirmed.
- The exact Philippine healthcare tax/reporting requirements need accountant
  validation.
- Spreadsheet-only adjustments must be discovered during report validation.
