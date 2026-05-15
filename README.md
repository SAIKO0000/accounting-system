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
powershell -ExecutionPolicy Bypass -File .\tools\run-api-dev.ps1
```

Useful API endpoints:

- `GET /`
- `GET /api/accounts`
- `POST /api/journals/validate`
- `POST /api/journals/post`
- `POST /api/journals/{id}/reverse`
- `GET /api/journals`
