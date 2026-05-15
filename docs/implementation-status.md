# Implementation Status

Status date: 2026-05-16

## Completed

- Legacy Sage system analysis documents.
- Modernization PRD and architecture direction.
- PostgreSQL core finance schema.
- Repeatable PostgreSQL schema apply-test.
- Read-only Sage company inspection script.
- Legacy data inventory for visible company files.
- ASP.NET Core solution scaffold.
- Core accounting domain library.
- Posting engine prototype with:
  - explicit debit and credit lines,
  - account existence validation,
  - inactive-account validation,
  - one-sided line validation,
  - balanced journal validation,
  - posted journal creation,
  - reversal journal creation.
- Minimal API prototype with:
  - PostgreSQL-backed account list endpoint,
  - journal validation endpoint,
  - journal posting endpoint,
  - reversal endpoint,
  - posted journal list and detail endpoints,
  - trial balance endpoint,
  - vendor list endpoint,
  - customer list endpoint,
  - AP document list endpoint,
  - AR document list endpoint,
  - AP bill endpoint,
  - AP payment endpoint,
  - AR invoice endpoint,
  - AR receipt endpoint,
  - bank account list endpoint,
  - bank reconciliation list/detail endpoints,
  - bank reconciliation candidate line endpoint,
  - bank reconciliation draft and line matching endpoints,
  - aged payables report endpoint,
  - aged receivables report endpoint,
  - general ledger detail report endpoint.
- Audit event writes for journal posting, journal reversal, AP bill/payment,
  AR invoice/receipt, and trial balance report views.
- AP bill and payment persistence with linked posted journals and open amount
  tracking.
- AR invoice and receipt persistence with linked posted journals and open amount
  tracking.
- AP and AR aging/detail reports for current open documents.
- General ledger detail report with opening balance, period lines, running
  balance, and closing balance by account.
- Bank reconciliation draft and line-matching workflow for posted bank journal
  lines.
- Local development RBAC scaffold using `X-Accounting-User`, seeded roles, and
  server-side permission checks.
- Generic migration staging table and CSV import script for authorized legacy
  extracts.
- Staged chart-of-accounts transform script with explicit account-class mapping.
- Staged customer/vendor transform script for raw `tCustomr` and `tVendor`
  rows.
- Staged journal header/line transform script with explicit signed-amount
  polarity.
- Console test runner for core posting behavior.

## Verified

- `dotnet build AccountingSystem.sln` succeeds.
- `dotnet run --project tests\Accounting.Core.Tests\Accounting.Core.Tests.csproj` passes.
- PostgreSQL schema applies successfully to `accounting_schema_test`.
- PostgreSQL schema validation checks pass.
- API starts successfully with `tools/run-api-dev.ps1`.
- API persistence uses PostgreSQL through Npgsql.
- Audit smoke test confirms `post`, `reverse`, and `view` events are written.
- AP smoke test confirms vendor lookup, bill posting, partial payment, open
  amount update, and AP payment audit events.
- AR smoke test confirms customer lookup, invoice posting, partial receipt, open
  amount update, and AR receipt audit events.
- Aging report smoke test confirms aged payables and aged receivables return
  open document totals and write report view audit events.
- General ledger smoke test confirms account sections, movement lines, running
  balances, and report view audit events.
- RBAC smoke test confirms `api-dev` can access permitted endpoints, unknown
  users receive `401`, and authorization failures are audited.
- Bank reconciliation smoke test confirms bank account lookup, candidate line
  discovery, draft creation, line matching, cleared totals, and audit events.
- Migration staging smoke test confirms synthetic CSV rows import into
  `core.migration_staging_records` with a completed migration batch.
- Chart-of-accounts transform smoke test confirms staged `tAccount` rows preview
  cleanly, upsert into `core.accounts`, and create migration source references.
- Customer/vendor transform smoke test confirms staged partner rows preview
  cleanly, upsert into `core.business_partners`, and create migration source
  references.
- Journal transform smoke test confirms staged `tJourEnt` and `tJEntAct` rows
  preview cleanly, post a balanced journal, and create migration source
  references.

## Current Limitations

- API persistence covers accounts, vendors, customers, AP bill/payment documents,
  AR invoice/receipt documents, posted journals, journal lines, audit events,
  and trial balance.
- AP/AR aging reports currently use current `open_amount`; historical as-of
  aging requires settlement allocation history that is not modeled yet.
- Bank reconciliation supports draft creation and matching posted bank journal
  lines, but final close validation needs accountant-reviewed rules and opening
  statement balance treatment.
- RBAC is wired to API routes for local development, but production OIDC/MFA
  authentication is not implemented yet.
- Production Sage files remain credential-protected.
- No real production migration can proceed until authorized access or approved
  Sage exports are available.

## Next Engineering Steps

1. Add staged AP/AR document transforms after journal migration assumptions are
   validated.
2. Add AP/AR settlement allocation history if historical aging is required.
3. Replace the local development identity header with OIDC/MFA authentication.
4. Add bank reconciliation close/finalization rules.
5. Add report export audit events when export endpoints exist.
