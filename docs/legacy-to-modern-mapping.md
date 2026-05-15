# Legacy To Modern Mapping

## Purpose

This document maps known Sage Simply Accounting tables to the target PostgreSQL
core finance model. It is a migration guide, not a guarantee that every
production field is understood. All mappings must be validated against actual
production extracts and accepted reports.

## Company And Fiscal Setup

| Legacy source | Modern target | Notes |
| --- | --- | --- |
| `tCompany` | `core.companies` | Company name, country, base currency, fiscal metadata. |
| `tCompany.dtSDate`, `dtFDate` | `core.fiscal_years`, `core.fiscal_periods` | Period generation must be validated with finance users. |
| `tCurrncy` | Future currency table or company currency settings | V1 can start with base currency unless production uses multi-currency. |
| `tCurrExR` | Future exchange-rate table | Defer if production reports are single-currency. |

## Chart Of Accounts

| Legacy source | Modern target | Notes |
| --- | --- | --- |
| `tAccount.lId` | `core.accounts.legacy_account_id` | Preserve original account ID. |
| `tAccount.sName` | `core.accounts.name` | Account display name. |
| `tAccount.nAcctClass` | `core.accounts.legacy_account_class` | Must be mapped to modern account nature. |
| `tAccount.bInactive` | `core.accounts.status` | Inactive legacy accounts should remain visible for history. |
| `tAccount.bDoBRec` | `core.accounts.is_bank_account` | Confirm with `tBRInfo` and finance users. |
| `tLinkAct` | Configuration/settings table in later schema | Linked accounts drive default posting behavior. |

## General Ledger

| Legacy source | Modern target | Notes |
| --- | --- | --- |
| `tJourEnt` | `core.journals` | Journal header. |
| `tJourEnt.lId` | `core.journals.legacy_journal_id` | Preserve original ID. |
| `tJourEnt.dtJourDate` | `core.journals.journal_date` | Posting date. |
| `tJourEnt.nModule`, `nType` | `source_module`, future source type mapping | Requires mapping table after production profiling. |
| `tJourEnt.sSource` | `core.journals.source_reference` | Legacy source/reference. |
| `tJourEnt.sComment` | `core.journals.memo` | Journal memo. |
| `tJEntAct` | `core.journal_lines` | Journal detail lines. |
| `tJEntAct.lAcctId` | `core.journal_lines.account_id` | Join through mapped account. |
| `tJEntAct.dAmount` | `legacy_signed_amount`, normalized debit/credit | Do not convert without account-class validation. |

## Taxes And Projects

| Legacy source | Modern target | Notes |
| --- | --- | --- |
| `tTaxAuth` | Tax authority configuration in later schema | V1 schema has simplified tax codes. |
| `tTaxCode` | `core.tax_codes` | Tax code and description. |
| `tTaxDtl` | Reporting/tax detail table in later schema | Needed if statutory tax reporting is in scope. |
| `tJEntTax` | Tax details linked to journal lines | Expand schema during tax implementation. |
| `tProject` | Future project module | Defer unless v1 reports require it. |
| `tJEntPrj` | Future project allocation lines | Preserve in raw migration staging. |

## Customers And AR

| Legacy source | Modern target | Notes |
| --- | --- | --- |
| `tCustomr` | `core.business_partners` with `partner_type = customer` | Preserve customer ID. |
| `tCusTr` | `core.ar_documents` and journals | Header-level AR transaction. |
| `tCusTrDt` | AR settlement/payment detail | Additional allocation table may be needed. |

## Vendors And AP

| Legacy source | Modern target | Notes |
| --- | --- | --- |
| `tVendor` | `core.business_partners` with `partner_type = vendor` | Preserve vendor ID. |
| `tVenTr` | `core.ap_documents` and journals | Header-level AP transaction. |
| `tVenTrDt` | AP settlement/payment detail | Additional allocation table may be needed. |

## Bank Reconciliation

| Legacy source | Modern target | Notes |
| --- | --- | --- |
| `tBRInfo` | Bank reconciliation settings | May become bank account configuration. |
| `tBRSum` | `core.bank_reconciliations` | Reconciliation summary. |
| `tBRTr` | `core.bank_reconciliation_lines` | Transaction status and cleared amounts. |
| `tDeposit` | Bank deposit workflow in later schema | Preserve raw extract for validation. |

## Users And Audit

| Legacy source | Modern target | Notes |
| --- | --- | --- |
| `tUser` | `core.users`, `core.user_roles` | Legacy access numbers require mapping to modern roles. |
| `tUserLog` | `core.audit_events` or operational login history | Keep raw values for historical traceability. |

## Reporting

| Legacy source | Modern target | Notes |
| --- | --- | --- |
| `tRptOpts`, `tRptCols`, `tRptFrmt` | Report presets after v1 | Useful but not required for core accounting correctness. |
| `Forms/*.rpt` | Modern report definitions | Use as visual/layout references only. |
| Monthly workbooks | Validation references | Classify each workbook tab before treating it as source data. |

## Required Mapping Decisions During Migration

- Legacy account class to modern account nature.
- Legacy module/type codes to modern source modules.
- Legacy signed amount conversion into debit/credit.
- Opening balance treatment by fiscal year and account nature.
- Manual workbook adjustment handling.
- AP/AR settlement allocation model.
- Tax authority and tax-code rules for Philippine healthcare reporting.
