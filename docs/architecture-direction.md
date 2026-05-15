# Architecture Direction

## Decision

Build the upgraded accounting system as a private-cloud modular monolith with
PostgreSQL as the system of record and a server-side double-entry posting
engine.

## Rationale

Accounting systems need strong consistency across journals, subledgers,
balances, taxes, bank reconciliation, audit trail, and reports. Splitting these
flows into microservices in v1 would introduce distributed transaction and
reconciliation complexity without clear business value.

A modular monolith keeps one deployable application and one transactional
database while still separating domain modules internally.

## Recommended Stack

- Frontend: Next.js with TypeScript.
- Backend: ASP.NET Core preferred; NestJS acceptable if the delivery team
  standardizes on TypeScript.
- Database: PostgreSQL.
- Authentication: OIDC/OAuth 2.0 provider with MFA capability.
- Reporting: server-side reporting queries/views with Excel and PDF export.
- Deployment: private cloud with managed PostgreSQL, encrypted backups, and
  point-in-time recovery.
- Observability: structured logs, metrics, traces, and OpenTelemetry-compatible
  correlation.

## Domain Modules

- Identity and access
- Company and fiscal calendar
- Chart of accounts
- Posting engine
- General ledger
- Accounts payable
- Accounts receivable
- Bank reconciliation
- Tax codes
- Reporting
- Migration
- Audit trail

## Core Accounting Model

The new system should store journals using explicit debit and credit columns,
not a Sage-style single signed amount. Legacy imports may preserve Sage signed
amounts as raw source data, but normalized posted entries must be represented as
debits and credits.

Every posted journal must satisfy:

- At least two journal lines.
- Total debit equals total credit.
- No negative debit or credit values.
- Each line has exactly one non-zero side.
- Posted entries are immutable.
- Corrections are made through linked reversals.

## Data Flow

1. The browser client submits a draft transaction.
2. The backend validates permissions and input.
3. Domain services translate the workflow into accounting lines.
4. The posting engine validates period, accounts, currency, debit/credit
   balance, and subledger rules.
5. A database transaction writes journal header, lines, subledger records, and
   audit events.
6. Reports read from normalized ledger tables and reporting views.

## Interface Direction

The internal API should be resource-oriented and explicit:

- `POST /journal-drafts`
- `POST /journal-drafts/{id}/post`
- `POST /posted-journals/{id}/reverse`
- `GET /reports/trial-balance`
- `GET /reports/general-ledger`
- `GET /reports/balance-sheet`
- `GET /reports/income-statement`
- `GET /reports/aged-payables`
- `GET /reports/aged-receivables`

Public third-party APIs are deferred until v1 internal workflows are stable.

## Deployment Shape

V1 should run as:

- One web frontend.
- One backend application.
- One PostgreSQL database.
- One background worker process.
- One object storage location for exports/import packages.
- One centralized logging/monitoring stack.

This can be deployed with containers, but Kubernetes is optional and should only
be used if the operations team already supports it.
