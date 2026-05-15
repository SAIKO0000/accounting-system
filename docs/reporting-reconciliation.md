# Reporting And Reconciliation Plan

## Reporting Objective

Reports must become reproducible outputs from the PostgreSQL accounting system,
not manual reconstructions from spreadsheets. Existing Sage reports and finance
workbooks remain validation references during migration.

## V1 Reports

- Trial balance
- General ledger detail
- Balance sheet
- Income statement
- Aged payables
- Aged receivables
- Bank reconciliation summary
- Cash ledger
- Audit log export

## Report Requirements

- Filter by company and date range.
- Respect fiscal periods.
- Support export to Excel and PDF.
- Provide drill-down from summary to journal lines.
- Include report run metadata:
  - report name
  - parameters
  - user
  - timestamp
  - company
  - generated file ID where applicable

## Accounting Report Rules

Trial balance:

- Sum posted journal lines through the report date.
- Display debit or credit normal balance by account type.
- Confirm total debits equal total credits.

General ledger:

- Show opening balance, period movements, and closing balance.
- Include journal number, source, date, memo, debit, credit, and running balance.

Balance sheet:

- Include assets, liabilities, and equity.
- Retained/current earnings treatment must be reviewed with the accountant
  during implementation.

Income statement:

- Include revenue and expense accounts for selected period.
- Support month-to-date and year-to-date views after v1 if required.

Aged AP/AR:

- Group by vendor/customer.
- Show current and aging buckets.
- Aging bucket defaults must be validated against legacy settings.

## Migration Reconciliation

For each canonical legacy report:

1. Capture the source file, report date, parameters, and generated timestamp.
2. Generate equivalent output from the new system.
3. Compare totals and material line-level differences.
4. Classify differences as:
   - migration mapping issue,
   - legacy report option difference,
   - spreadsheet manual adjustment,
   - expected rounding/currency difference,
   - unresolved issue.
5. Require finance signoff before cutover.

## Workbook Handling

Monthly Excel reports are not automatically treated as source-of-truth data.
Each workbook tab must be classified:

- Generated from Sage without adjustment
- Sage export plus manual formatting
- Sage export plus manual accounting adjustment
- Management-only analysis
- External data source

Only validated accounting adjustments should become transactions in the new
system.
