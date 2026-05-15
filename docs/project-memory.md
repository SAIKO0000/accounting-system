# Project Memory And Progress Tracker

Last updated: 2026-05-16  
Primary owner: `<name / team / LLM account>`  
Current phase: `<research | planning | prototype | implementation | validation | thesis writing>`

> Purpose: This file is the central handoff and continuity document for the accounting system modernization project. Keep it concise, current, and specific enough that a new developer or AI agent can continue without reconstructing context from scratch.

## 1. Project Overview

### Basic Information

- Project name: Accounting System Modernization
- Short description: Modern replacement for a legacy Sage Simply Accounting Pro v13.0 desktop accounting system.
- Thesis/research objective: `<state thesis objective here>`
- Target users: Finance staff, bookkeepers, finance managers, auditors, administrators.
- Current system scope: Core finance v1.
- Out of scope for now: Payroll, full inventory, multi-tenant SaaS, broad third-party integrations, AI automation.

### Core Technologies

- Backend: ASP.NET Core / .NET 9
- Domain logic: C#
- Database: PostgreSQL
- PostgreSQL driver: Npgsql
- Frontend direction: Next.js + TypeScript, not yet implemented
- Legacy system: Sage Simply Accounting Pro v13.0, Jet `.SDB/.SDW` company files
- Local tools: PowerShell, `psql`, .NET SDK

### Current Research Direction

- Modernize a legacy desktop accounting system into a private-cloud modular monolith.
- Preserve accounting correctness through double-entry posting, immutable journals, reversal workflows, audit logging, and validated migration.
- Use the legacy system as a reference source, not as code to refactor.

## 2. Current System Architecture

### Current Modern Architecture

```text
Browser UI (planned Next.js)
        |
        v
ASP.NET Core API
        |
        v
Accounting.Core domain library
        |
        v
PostgreSQL core finance schema
```

### Major Modules

- `Accounting.Core`: domain models and posting engine.
- `Accounting.Api`: HTTP API and PostgreSQL persistence.
- `database/`: PostgreSQL schema, validation SQL, development seed data.
- `tools/`: PowerShell scripts for schema testing, dev database setup, API launch, and Sage inspection.
- `docs/`: PRD, architecture, migration, security, reporting, implementation status, and this memory file.

### System Flow

1. User/API submits a journal draft.
2. API loads accounts from PostgreSQL.
3. `PostingEngine` validates account existence, account status, one-sided lines, and debit/credit balance.
4. API persists draft journal and lines inside a database transaction.
5. Database trigger validates posting invariants before journal status becomes `posted`.
6. Posted journals become immutable; corrections use reversal journals.
7. Reports query posted journal lines.

### Important Files And Folders

- `README.md`: repo entry point and run commands.
- `AccountingSystem.sln`: .NET solution.
- `src/Accounting.Core/AccountingDomain.cs`: core accounting records/enums.
- `src/Accounting.Core/PostingEngine.cs`: journal validation, posting, reversal logic.
- `src/Accounting.Api/Program.cs`: API routes.
- `src/Accounting.Api/AccountingRepository.cs`: PostgreSQL data access.
- `database/postgres-core-finance-schema.sql`: main schema.
- `database/schema-validation.sql`: database validation checks.
- `database/dev-seed.sql`: local development seed data.
- `tools/setup-dev-database.ps1`: creates local `accounting_dev`.
- `tools/test-postgres-schema.ps1`: applies and validates schema in disposable DB.
- `tools/inspect-sage-company.ps1`: read-only Sage `.SDB` inventory helper.
- `docs/prd.md`: product requirements.
- `docs/implementation-status.md`: current implementation summary.
- `docs/legacy-data-inventory.md`: visible Sage file inventory.

### Dependencies And Tools

- .NET SDK 9.x
- PostgreSQL 18.x local server
- Npgsql NuGet package
- PowerShell
- Microsoft ACE OLE DB provider for Sage/Jet inspection

## 3. Development Status

### Completed Features

- [x] Legacy system analysis documentation
- [x] PRD draft
- [x] Architecture direction
- [x] Migration plan
- [x] Security/compliance requirements
- [x] PostgreSQL core finance schema
- [x] Schema validation script
- [x] Local dev database setup script
- [x] Read-only Sage inspection script
- [x] ASP.NET Core solution scaffold
- [x] Core posting engine prototype
- [x] Journal reversal logic
- [x] PostgreSQL-backed API for accounts, journal posting, journal retrieval, reversal, and trial balance
- [x] Audit event writes for journal posting, journal reversal, AP bill/payment, AR invoice/receipt, and trial balance report views
- [x] PostgreSQL-backed AP bill and payment persistence
- [x] PostgreSQL-backed AR invoice and receipt persistence
- [x] AP/AR aging detail report endpoints for current open documents
- [x] General ledger detail report endpoint
- [x] Local development RBAC scaffold and authorization-failure audit events
- [x] Bank reconciliation draft and line-matching workflow
- [x] Generic raw migration staging table and CSV import script
- [x] Staged chart-of-accounts transform with explicit class mapping
- [x] Staged customer/vendor transform
- [x] Staged journal header/line transform with explicit signed-amount polarity
- [x] Console test runner for posting engine

### Partially Implemented Features

- [ ] Financial reports: trial balance, AP/AR aging, and general ledger detail are implemented; accountant review and final formatting are still needed.
- [ ] Audit trail: journal post, reversal, AP bill/payment, AR invoice/receipt, report views, and authorization failures are written; exports, imports, and settings changes are not yet covered.
- [ ] AP workflow: bill/payment persistence and current-open aging reports exist; accountant review and historical settlement allocation are still needed.
- [ ] AR workflow: invoice/receipt persistence and current-open aging reports exist; accountant review and historical settlement allocation are still needed.
- [ ] RBAC: local development permission checks are wired; production OIDC/MFA authentication is not implemented yet.
- [ ] Bank reconciliation: draft creation and line matching exist; close/finalization rules and adjustments still need accountant validation.
- [ ] Migration tooling: inspection script, raw CSV staging, account/customer/vendor/journal transforms exist; production extraction and subledger mapping transforms are not available yet.
- [ ] API persistence: accounts, vendors, customers, AP, AR, bank reconciliation draft matching, journals, audit, and trial balance implemented.

### Planned Features

- [ ] Bank reconciliation close/finalization rules
- [ ] Audit event writes for exports, imports, and settings changes
- [ ] Production OIDC/MFA authentication
- [ ] Next.js frontend
- [ ] Legacy AP/AR subledger migration mapping transforms
- [ ] Report export to Excel/PDF
- [ ] Accountant-validated Philippine healthcare tax/report rules

### Deprecated Or Removed Features

- `<feature>`: `<why removed / when / replacement>`

## 4. Task Tracking

Use this format for every meaningful task.

### Task: Add Audit Event Writes

- Priority: HIGH
- Status: DONE
- Description: Write `core.audit_events` records for journal post, reversal, failed authorization, report generation/export, migration import, and settings changes.
- Related files/modules:
  - `src/Accounting.Api/AccountingRepository.cs`
  - `database/postgres-core-finance-schema.sql`
- Dependencies:
  - Existing audit table
  - User identity source
- Assigned to: `<person / LLM>`
- Notes:
  - Journal post, journal reversal, AP bill/payment, AR invoice/receipt, and trial balance view events are implemented.
  - Failed authorization is implemented for local RBAC checks.
  - Exports, imports, and settings changes remain future audit coverage.

### Task: Add AP Persistence

- Priority: HIGH
- Status: DONE
- Description: Implement vendor bill and payment persistence using `core.ap_documents` and posted journals.
- Related files/modules:
  - `src/Accounting.Api`
  - `database/postgres-core-finance-schema.sql`
- Dependencies:
  - Posting engine
  - Business partner table
- Assigned to: `<person / LLM>`
- Notes:
  - Vendor bill creation writes the AP document and posted AP journal in one database transaction.
  - Vendor payment writes the payment journal and reduces `open_amount` in one database transaction.
  - Current-open AP aging exists; historical aging and accountant reconciliation still need review.

### Task: Add Authentication And RBAC

- Priority: HIGH
- Status: IN PROGRESS
- Description: Add OIDC-compatible authentication and server-side permission checks.
- Related files/modules:
  - `src/Accounting.Api`
  - `core.users`
  - `core.roles`
  - `core.permissions`
- Dependencies:
  - Chosen identity provider
- Assigned to: `<person / LLM>`
- Notes:
  - Server-side permission checks are wired using `X-Accounting-User` for local development.
  - Unknown identities receive `401` and write `authorization_failure` audit events.
  - Production OIDC/MFA remains pending.

### Task: Add AR Persistence

- Priority: HIGH
- Status: DONE
- Description: Implement customer invoice and receipt persistence using `core.ar_documents` and posted journals.
- Related files/modules:
  - `src/Accounting.Api`
  - `database/postgres-core-finance-schema.sql`
- Dependencies:
  - Posting engine
  - Business partner table
- Assigned to: `<person / LLM>`
- Notes:
  - Customer invoice creation writes the AR document and posted AR journal in one database transaction.
  - Customer receipt writes the receipt journal and reduces `open_amount` in one database transaction.
  - Current-open AR aging exists; historical aging and accountant reconciliation still need review.

### Task: Add AP/AR Aging Detail Reports

- Priority: HIGH
- Status: DONE
- Description: Add aged payables and aged receivables report endpoints with document-level current, 1-30, 31-60, 61-90, and over-90 buckets.
- Related files/modules:
  - `src/Accounting.Api/AccountingRepository.cs`
  - `src/Accounting.Api/Program.cs`
- Dependencies:
  - AP/AR document persistence
- Assigned to: `<person / LLM>`
- Notes:
  - Reports currently bucket current `open_amount` documents.
  - Historical as-of aging needs settlement allocation history before legacy month-end validation.

### Task: Add General Ledger Detail Report

- Priority: HIGH
- Status: DONE
- Description: Add a general ledger detail report with opening balances, period movement lines, running balances, and closing balances by account.
- Related files/modules:
  - `src/Accounting.Api/AccountingRepository.cs`
  - `src/Accounting.Api/Program.cs`
- Dependencies:
  - Posted journal persistence
- Assigned to: `<person / LLM>`
- Notes:
  - Report uses account normal balance rules for signed amount and running balance.
  - Accountant review is still needed for final report layout and legacy comparison.

### Task: Add Bank Reconciliation Draft Matching

- Priority: HIGH
- Status: DONE
- Description: Add bank account lookup, unreconciled bank journal line candidates, draft reconciliation creation, line matching, and reconciliation detail totals.
- Related files/modules:
  - `src/Accounting.Api/AccountingRepository.cs`
  - `src/Accounting.Api/Program.cs`
  - `database/postgres-core-finance-schema.sql`
- Dependencies:
  - Posted journal lines
  - Bank account flag on chart of accounts
- Assigned to: `<person / LLM>`
- Notes:
  - Closing/finalization is intentionally pending because opening statement balance, prior outstanding items, bank charges, and adjustment treatment need accountant validation.

### Task: Production Sage Access

- Priority: BLOCKER
- Status: BLOCKED
- Description: Obtain authorized access to protected production `.SDB/.SDW` files.
- Related files/modules:
  - `docs/legacy-data-inventory.md`
  - `tools/inspect-sage-company.ps1`
- Dependencies:
  - Finance/operations credentials or approved Sage export process
- Assigned to: `<person / LLM>`
- Notes:
  - Do not bypass credentials.

### Task: Add Raw Migration Staging

- Priority: HIGH
- Status: DONE
- Description: Add a generic staging table and CSV import tool for authorized legacy exports.
- Related files/modules:
  - `database/postgres-core-finance-schema.sql`
  - `database/schema-validation.sql`
  - `tools/import-legacy-csv-staging.ps1`
  - `docs/migration-plan.md`
- Dependencies:
  - Authorized CSV exports or approved Sage extraction process
- Assigned to: `<person / LLM>`
- Notes:
  - Rows are preserved as raw JSONB with source table, source key, source row number, and SHA-256 hash.
  - This does not transform or post accounting data into live tables.
  - Next migration step is a controlled chart-of-accounts transform from staged `tAccount` rows.

### Task: Add Staged Account Transform

- Priority: HIGH
- Status: DONE
- Description: Transform staged `tAccount` rows into `core.accounts` using an explicit JSON account-class mapping.
- Related files/modules:
  - `tools/transform-staged-accounts.ps1`
  - `docs/migration-plan.md`
- Dependencies:
  - Raw migration staging rows
  - Accountant-reviewed account class mapping
- Assigned to: `<person / LLM>`
- Notes:
  - Script previews validation by default and only writes when `-Apply` is supplied.
  - Upserts `core.accounts` and writes `core.migration_source_refs`.
  - Do not use production Sage class mappings until validated against real reports.

### Task: Add Staged Customer/Vendor Transform

- Priority: HIGH
- Status: DONE
- Description: Transform staged `tCustomr` and `tVendor` rows into `core.business_partners`.
- Related files/modules:
  - `tools/transform-staged-partners.ps1`
  - `docs/migration-plan.md`
- Dependencies:
  - Raw migration staging rows
  - Authorized customer/vendor extracts
- Assigned to: `<person / LLM>`
- Notes:
  - Script previews validation by default and only writes when `-Apply` is supplied.
  - Upserts `core.business_partners` and writes `core.migration_source_refs`.
  - Current field mapping is conservative: ID, name, contact, email, phone, tax ID, active flag.

### Task: Add Staged Journal Transform

- Priority: HIGH
- Status: DONE
- Description: Transform staged `tJourEnt` and `tJEntAct` rows into posted `core.journals` and `core.journal_lines`.
- Related files/modules:
  - `tools/transform-staged-journals.ps1`
  - `docs/migration-plan.md`
- Dependencies:
  - Raw migration staging rows
  - Staged account transform
  - Validated signed-amount polarity
- Assigned to: `<person / LLM>`
- Notes:
  - Script previews validation by default and only writes when `-Apply` is supplied.
  - Requires `-PositiveAmountSide Debit|Credit`; this must be validated against accepted Sage reports before real migration.
  - Applies only journals with mapped accounts, at least two lines, and balanced debit/credit totals.
  - Writes `core.migration_source_refs` for journal headers and lines.

### Task Template

```md
### Task: <title>

- Priority: <LOW | MEDIUM | HIGH | BLOCKER>
- Status: <TODO | IN PROGRESS | BLOCKED | REVIEW NEEDED | DONE>
- Description: <what needs to be done and why>
- Related files/modules:
  - `<path>`
- Dependencies:
  - `<dependency>`
- Assigned to: `<person / LLM / unassigned>`
- Notes:
  - `<important context>`
```

## 5. Thesis Alignment

### Methodology Mapping

| Thesis component | Current implementation status | Missing work | Risks/gaps |
| --- | --- | --- | --- |
| Legacy system analysis | Done | Validate active production DB with authorized access | Protected files may hide important workflows |
| Requirements analysis | Draft PRD exists | Stakeholder/accountant review | Requirements may miss local finance practices |
| System design | Architecture docs and schema exist | Frontend architecture and auth design | Overbuilding before validation |
| Prototype implementation | Backend prototype exists | AP/AR/bank/report modules | Prototype may drift from thesis scope |
| Data migration | Inventory and inspection script exist | Authorized extraction and reconciliation | Production access blocked |
| Validation | Schema and posting tests exist | User acceptance and financial reconciliation | Need canonical reports |

### Computing Contribution Notes

- Candidate contribution: A structured modernization framework for converting a legacy desktop accounting system into a validated private-cloud modular monolith.
- Candidate technical focus: Double-entry posting engine, immutable audit trail, and migration reconciliation from legacy Jet accounting data.
- Candidate evaluation: Compare migrated reports against legacy Sage outputs and assess usability/maintainability improvements.

## 6. Important Decisions And Rationale

### Decision: Use ASP.NET Core For Backend

- Status: Accepted
- Rationale: Strong fit for enterprise financial logic, type safety, long-term maintainability, and transactional backend services.
- Alternatives considered: Next.js-only backend, NestJS.
- Notes: Next.js remains recommended for frontend.

### Decision: Use PostgreSQL As System Of Record

- Status: Accepted
- Rationale: Strong relational integrity, transactions, constraints, reporting support, and audit-friendly design.
- Alternatives considered: SQL Server, MySQL/MariaDB, document database.
- Notes: PostgreSQL schema already applies and validates locally.

### Decision: Modular Monolith For V1

- Status: Accepted
- Rationale: Accounting workflows need strong consistency. Microservices would add distributed transaction risk too early.
- Alternatives considered: Microservices, service-oriented split.
- Notes: Internal module boundaries still matter.

### Decision: Preserve Legacy Sage As Historical Reference

- Status: Accepted
- Rationale: Production logic is proprietary and protected; reports must be validation references.
- Alternatives considered: Direct rewrite without validation.
- Notes: Migration requires authorized access.

### Rejected Ideas

- Microservices for v1 core posting: rejected due to unnecessary consistency risk.
- Replacing all Sage modules immediately: rejected due to high scope and migration risk.
- Treating Excel workbooks as automatically authoritative: rejected until workbook tabs are classified.

### Panelist / Adviser Feedback

| Date | Feedback | Response / change made | Status |
| --- | --- | --- | --- |
| YYYY-MM-DD | `<feedback>` | `<response>` | `<TODO | DONE>` |

## 7. Known Bugs / Internal Issues

### Issue: Production Sage Files Are Credential-Protected

- Affected module: Migration
- Severity: HIGH
- Reproduction notes: `tools/inspect-sage-company.ps1` opens sample/template files but fails on larger Gentrimed files without credentials.
- Suspected cause: Jet workgroup credentials differ from default `sysadmin`.
- Fix status: BLOCKED
- Notes: Authorized access required.

### Issue: API Requires Plain Connection String During Local Run

- Affected module: API dev tooling
- Severity: MEDIUM
- Reproduction notes: `tools/run-api-dev.ps1` requires `-PostgresPassword` or `ACCOUNTING_DB`.
- Suspected cause: No secret manager configured yet.
- Fix status: TODO
- Notes: Do not commit passwords.

### Issue Template

```md
### Issue: <title>

- Affected module: `<module>`
- Severity: <LOW | MEDIUM | HIGH | CRITICAL>
- Reproduction notes: `<steps>`
- Suspected cause: `<cause>`
- Fix status: <TODO | IN PROGRESS | BLOCKED | DONE>
- Notes: `<context>`
```

## 8. Research And Literature Notes

### Current Research Direction

- Legacy accounting system modernization
- Accounting data integrity
- Double-entry financial systems
- Audit trails and immutable transaction records
- Migration validation from legacy systems
- Private-cloud enterprise architecture

### Important Studies / Sources

| Source | Key idea | Relevance | Status |
| --- | --- | --- | --- |
| OWASP ASVS | Application security verification | Security requirements | Referenced |
| NIST SSDF | Secure software development practices | SDLC and thesis security basis | Referenced |
| PostgreSQL docs | Relational constraints/transactions | Data integrity | Referenced |
| Modern accounting platforms | Cloud, automation, APIs, auditability | Market comparison | Needs citation expansion |

### Experiment Plans

- Compare migrated trial balance against Sage trial balance.
- Compare migrated balance sheet and income statement against accepted reports.
- Validate double-entry posting invariants with automated tests.
- Evaluate user workflow completion time before/after modernization.
- Evaluate maintainability through modular architecture and test coverage.

### Validation Ideas

- Financial reconciliation accuracy
- User acceptance testing with finance staff
- Security control checklist
- Performance test for report generation
- Migration variance classification

## 9. AI / Developer Handoff Notes

### Current Project State

- Backend foundation exists and builds.
- PostgreSQL schema applies and validates.
- API and tools can enforce local RBAC, post/reverse journals, create/pay AP bills, create/receive AR invoices, draft/match bank reconciliations, generate trial balance, current-open AP/AR aging reports, general ledger detail, stage raw migration CSV rows, transform staged accounts/customers/vendors/journals, and write audit events from PostgreSQL-backed workflows.
- Production migration is blocked by protected Sage files.

### Last Worked On

- Added AP vendor lookup, AP document listing, AP bill creation, and AP payment endpoints.
- Added AR customer lookup, AR document listing, AR invoice creation, and AR receipt endpoints.
- AP bill creation now writes the posted AP journal and AP document atomically.
- AP payment now writes the payment journal and reduces document `open_amount` atomically.
- AR invoice creation now writes the posted AR journal and AR document atomically.
- AR receipt now writes the receipt journal and reduces document `open_amount` atomically.
- Added aged payables and aged receivables report endpoints.
- Added general ledger detail report endpoint.
- Added local development RBAC checks using seeded users/roles/permissions and the `X-Accounting-User` header.
- Added bank reconciliation draft and line-matching endpoints.
- Added generic raw migration staging table and CSV import script.
- Added staged chart-of-accounts transform script with explicit account-class mapping.
- Added staged customer/vendor transform script.
- Added staged journal header/line transform script with explicit signed-amount polarity.
- Smoke-tested AP and AR document creation, partial settlement, aging reports, general ledger detail, RBAC allow/deny behavior, bank reconciliation matching, raw CSV migration staging, staged account/customer/vendor/journal transforms, open amount updates, and audit event retrieval against `accounting_dev`.

### Immediate Next Tasks

1. Replace local development identity header with production OIDC/MFA authentication.
2. Obtain authorized Sage production access for migration.
3. Add bank reconciliation close/finalization rules after accounting validation.
4. Add settlement allocation history if historical aging is required.
5. Add AP/AR subledger transforms after journal migration assumptions are validated.

### Important Warnings

- Do not commit database passwords or connection strings.
- Do not modify legacy Sage `.SDB/.SDW` files.
- Do not assume Sage signed amounts map directly to debit/credit.
- Do not treat monthly workbooks as source-of-truth until classified.
- Do not expand to payroll/inventory before core finance validates.
- Do not call paid external APIs, hosted AI APIs, cloud services, or usage-billed services without explicit user approval; the project currently has no budget.

### Assumptions

- V1 scope is core finance.
- Deployment target is private cloud.
- Backend remains ASP.NET Core.
- Frontend will likely be Next.js later.
- PostgreSQL is the modern system of record.

### Unresolved Questions

- Which Gentrimed `.SDB` file is the official active system of record?
- Who owns/provides authorized Sage credentials or exports?
- What Philippine healthcare-specific reports are mandatory?
- What fiscal calendar and closing procedures does finance actually use?
- Which monthly workbook tabs contain manual adjustments?

## 10. Changelog / Progress Log

Add one entry per meaningful change. Keep entries short but specific.

### YYYY-MM-DD

- Changes made:
  - `<summary>`
- Files affected:
  - `<path>`
- Reason for change:
  - `<why>`
- Verification:
  - `<tests/checks>`
- Next actions:
  - `<next>`

### 2026-05-15

- Changes made:
  - Created modernization docs, PostgreSQL schema, schema tests, .NET solution, posting engine, API prototype, PostgreSQL persistence, audit event writes, AP bill/payment persistence, AR invoice/receipt persistence, bank reconciliation draft matching, AP/AR aging reports, general ledger detail reporting, local development RBAC, raw migration staging, staged account/customer/vendor/journal transforms, and migration inspection tools.
- Files affected:
  - `docs/`
  - `database/`
  - `src/`
  - `tests/`
  - `tools/`
- Reason for change:
  - Establish long-term modernization foundation and reduce context loss.
- Verification:
  - `dotnet build AccountingSystem.sln`
  - `dotnet run --project tests/Accounting.Core.Tests/Accounting.Core.Tests.csproj`
  - PostgreSQL schema apply-test
  - API smoke tests against `accounting_dev`, including audit event retrieval, AP bill/payment persistence, AR invoice/receipt persistence, AP/AR aging reports, general ledger detail, RBAC allow/deny behavior, bank reconciliation matching, raw CSV staging import, and staged account/customer/vendor/journal transforms
- Next actions:
  - Replace local identity bridge with production OIDC/MFA.
  - Add AP/AR subledger transforms.
  - Obtain authorized Sage production access.
