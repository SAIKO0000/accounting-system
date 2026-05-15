# Legacy Data Inventory

Inventory date: 2026-05-15

## Purpose

This inventory records the visible Sage company database files and the result of
read-only inspection with known default credentials. It is an input to migration
planning, not a finance-approved data extract.

## Company Files In `C:\project1\winsim`

| Company database | Size | Last modified | Paired workgroup | Inspection status |
| --- | ---: | --- | --- | --- |
| `Generic Company.SDB` | 3,948,544 bytes | 2024-09-12 17:05 | `Generic Company.SDW` | Opened with `sysadmin` and blank password |
| `Gentrimed Cathlab Company.SDB` | 3,928,064 bytes | 2025-10-16 10:08 | `Gentrimed Cathlab Company.SDW` | Opened with `sysadmin` and blank password |
| `Gentrimed Cathlab.SDB` | 6,397,952 bytes | 2026-05-15 13:57 | `Gentrimed Cathlab.SDW` | Credential-protected |
| `Gentrimed-Cathlab.SDB` | 4,857,856 bytes | 2026-04-10 08:38 | `Gentrimed-Cathlab.SDW` | Credential-protected |
| `Gentrimedical Center and Hospital, Inc.SDB` | 82,677,760 bytes | 2026-05-15 18:15 | `Gentrimedical Center and Hospital, Inc.SDW` | Credential-protected |

## Read-Only Inspection Results

### Generic Company

This appears to be a sample or template-like company file with some journal
activity.

| Core table | Rows |
| --- | ---: |
| `tCompany` | 1 |
| `tAccount` | 110 |
| `tJourEnt` | 12 |
| `tJEntAct` | 133 |
| `tCustomr` | 0 |
| `tCusTr` | 0 |
| `tVendor` | 0 |
| `tVenTr` | 0 |
| `tBRInfo` | 0 |
| `tBRSum` | 0 |
| `tBRTr` | 0 |
| `tUser` | 1 |

### Gentrimed Cathlab Company

This file opens but appears to contain only template/setup data, not operational
transactions.

| Core table | Rows |
| --- | ---: |
| `tCompany` | 1 |
| `tAccount` | 110 |
| `tJourEnt` | 0 |
| `tJEntAct` | 0 |
| `tCustomr` | 0 |
| `tCusTr` | 0 |
| `tVendor` | 0 |
| `tVenTr` | 0 |
| `tBRInfo` | 0 |
| `tBRSum` | 0 |
| `tBRTr` | 0 |
| `tUser` | 1 |

## Protected Files

The following files could not be opened with default credentials and require
authorized access before migration can proceed:

- `Gentrimed Cathlab.SDB`
- `Gentrimed-Cathlab.SDB`
- `Gentrimedical Center and Hospital, Inc.SDB`

The largest and most recently modified file is
`Gentrimedical Center and Hospital, Inc.SDB`; it is a strong candidate for the
active system of record, but that must be confirmed with finance or operations.

## Migration Implications

- The inspectable files are useful for schema and tooling validation but are not
  enough for production migration.
- The production-like files are protected by Sage workgroup credentials.
- Migration cannot safely proceed to financial validation until authorized
  access is available.
- The active company file must be identified before selecting canonical reports.
