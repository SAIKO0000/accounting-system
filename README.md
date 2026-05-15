# Accounting System Modernization

This repository contains the modernization foundation for the legacy Sage
Simply Accounting Pro v13.0 desktop accounting system found in the surrounding
`winsim` folders.

The current legacy system is treated as a production accounting source, not as
source code to refactor. The modernization target is a private-cloud modular
monolith that replaces the core finance workflows first:

- Chart of accounts
- General ledger and immutable journal posting
- Accounts payable
- Accounts receivable
- Vendor and customer masters
- Bank reconciliation
- Financial reporting
- Users, permissions, and audit trail
- Legacy data migration and reconciliation

## Repository Contents

- [Product Requirements Document](docs/prd.md)
- [Project Memory And Progress Tracker](docs/project-memory.md)
- [Legacy System Understanding](docs/legacy-system-understanding.md)
- [Legacy Data Inventory](docs/legacy-data-inventory.md)
- [Architecture Direction](docs/architecture-direction.md)
- [Migration Plan](docs/migration-plan.md)
- [Security and Compliance Requirements](docs/security-compliance.md)
- [Reporting and Reconciliation Plan](docs/reporting-reconciliation.md)
- [Implementation Roadmap](docs/implementation-roadmap.md)
- [Implementation Status](docs/implementation-status.md)
- [Test and Acceptance Plan](docs/test-plan.md)
- [PostgreSQL Core Finance Schema](database/postgres-core-finance-schema.sql)
- [PostgreSQL Schema Validation SQL](database/schema-validation.sql)

## Current Decision

The upgraded system should be implemented as a private-cloud modular monolith
with a PostgreSQL system of record and a server-side posting engine that
enforces double-entry accounting. Microservices are intentionally out of scope
for v1 because core finance requires strong transactional consistency.

## Important Constraint

Production `.SDB` company databases are credential-protected. Migration work
must use authorized access and reconcile extracted data against accepted Sage
reports and monthly finance workbooks before the new system becomes the source
of truth.

## Local Schema Test

After PostgreSQL is installed, run a disposable apply-test:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\test-postgres-schema.ps1 -Password "<postgres-password>"
```

The script drops and recreates only the `accounting_schema_test` database.

Create a local development database for the API:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\setup-dev-database.ps1 -Password "<postgres-password>"
```

The script drops and recreates only the `accounting_dev` database.

Import an authorized CSV extract into raw migration staging:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\import-legacy-csv-staging.ps1 `
  -CsvPath ".\exports\tAccount.csv" `
  -SourceName "authorized-sage-export" `
  -SourceTable "tAccount" `
  -SourceKeyColumn "lId" `
  -Password "<postgres-password>"
```

This preserves raw rows as JSONB staging records; it does not post or transform
accounting data into live tables.

Preview and apply a staged chart-of-accounts transform:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\transform-staged-accounts.ps1 `
  -MigrationBatchId 1 `
  -CompanyId 1 `
  -ClassMappingPath ".\config\account-class-map.json" `
  -Password "<postgres-password>"

powershell -ExecutionPolicy Bypass -File .\tools\transform-staged-accounts.ps1 `
  -MigrationBatchId 1 `
  -CompanyId 1 `
  -ClassMappingPath ".\config\account-class-map.json" `
  -Password "<postgres-password>" `
  -Apply
```

The account-class mapping file is required so Sage class semantics are reviewed
explicitly instead of guessed in code.

Preview and apply staged customer/vendor transforms:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\transform-staged-partners.ps1 `
  -MigrationBatchId 2 `
  -CompanyId 1 `
  -PartnerType customer `
  -SourceTable "tCustomr" `
  -Password "<postgres-password>"

powershell -ExecutionPolicy Bypass -File .\tools\transform-staged-partners.ps1 `
  -MigrationBatchId 3 `
  -CompanyId 1 `
  -PartnerType vendor `
  -SourceTable "tVendor" `
  -Password "<postgres-password>" `
  -Apply
```

Preview and apply staged journal transforms:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\transform-staged-journals.ps1 `
  -MigrationBatchId 4 `
  -CompanyId 1 `
  -PositiveAmountSide Debit `
  -Password "<postgres-password>"

powershell -ExecutionPolicy Bypass -File .\tools\transform-staged-journals.ps1 `
  -MigrationBatchId 4 `
  -CompanyId 1 `
  -PositiveAmountSide Debit `
  -Password "<postgres-password>" `
  -Apply
```

`PositiveAmountSide` must be validated against accepted Sage reports before any
real migration run.

## Build And Run

Build the solution:

```powershell
$env:DOTNET_CLI_HOME=(Resolve-Path '.').Path
dotnet build AccountingSystem.sln
```

Run the core accounting test runner:

```powershell
$env:DOTNET_CLI_HOME=(Resolve-Path '.').Path
dotnet run --project .\tests\Accounting.Core.Tests\Accounting.Core.Tests.csproj
```

Run the API locally:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-api-dev.ps1 -PostgresPassword "<postgres-password>"
```

Local development requests use `X-Accounting-User: api-dev` by default. This is
a development identity bridge only; production authentication still needs OIDC.

Useful API endpoints:

- `GET /`
- `GET /api/companies/{companyId}/accounts`
- `GET /api/companies/{companyId}/vendors`
- `GET /api/companies/{companyId}/customers`
- `GET /api/companies/{companyId}/bank-accounts`
- `GET /api/companies/{companyId}/ap-documents`
- `GET /api/companies/{companyId}/ar-documents`
- `POST /api/ap-documents/bills`
- `POST /api/ap-documents/{documentId}/payments`
- `POST /api/ar-documents/invoices`
- `POST /api/ar-documents/{documentId}/receipts`
- `GET /api/companies/{companyId}/bank-reconciliations`
- `GET /api/bank-reconciliations/{reconciliationId}`
- `POST /api/bank-reconciliations`
- `GET /api/companies/{companyId}/bank-accounts/{bankAccountId}/candidate-lines?throughDate=2026-05-31`
- `POST /api/bank-reconciliations/{reconciliationId}/lines`
- `POST /api/journals/validate`
- `POST /api/journals/post`
- `POST /api/journals/{id}/reverse`
- `GET /api/journals/{id}`
- `GET /api/companies/{companyId}/journals`
- `GET /api/companies/{companyId}/reports/trial-balance?asOfDate=2026-05-15`
- `GET /api/companies/{companyId}/reports/aged-payables?asOfDate=2026-07-20`
- `GET /api/companies/{companyId}/reports/aged-receivables?asOfDate=2026-07-20`
- `GET /api/companies/{companyId}/reports/general-ledger?fromDate=2026-05-01&toDate=2026-07-20`
- `GET /api/companies/{companyId}/audit-events?limit=100`
