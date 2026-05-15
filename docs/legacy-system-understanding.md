# Legacy System Understanding

## Summary

The existing accounting system is a packaged Sage Simply Accounting Pro v13.0
Windows desktop installation from around 2006. It is not a conventional
application source-code repository. The executable, proprietary DLLs, Jet
database files, Crystal Reports assets, templates, manuals, backups, and
finance workbooks together define the current operating environment.

The visible system should be treated as business-critical accounting software.
The modernization effort should not assume that internal behavior can be safely
changed or reproduced without validating against real reports and accounting
outputs.

## Evidence From The Folder

- `winsim.exe` is the main Simply Accounting executable.
- `sa_bus.dll` appears to hold core business/accounting behavior.
- `sa_dblyr.dll` and `sa_dbdrv.dll` provide database access behavior.
- `sa_rpt.dll` provides reporting behavior.
- `sa_imprt.dll` and `sa_exprt.dll` support import/export workflows.
- Company data is stored in `.SDB` files with Microsoft Jet database headers.
- Paired `.SDW` files act as Jet workgroup/security databases.
- `.LDB` files are Jet lock files created when company data is open.
- `Forms/*.rpt` are Crystal Reports definitions.
- `Forms/*.sfm` are form layouts.
- `Forms/SCHEMA.INI` defines exported report schemas.
- `Template/*.TPL` files define industry chart-of-account templates.
- Monthly Excel/PPT reports appear to be downstream management reporting
  artifacts.

## Business Workflows Supported

The installed system supports traditional accounting workflows:

- Company setup and fiscal periods
- Chart of accounts
- General journal
- Accounts payable and vendors
- Accounts receivable and customers
- Sales invoices, receipts, purchase orders, and payments
- Bank accounts and bank reconciliation
- Inventory
- Payroll
- Projects and departments
- Tax codes and tax authorities
- Standard reports and exports

## Observed Database Model

The sample company file opens with default credentials and confirms the schema
shape. Production Gentrimed files are protected and require authorized access.

Important tables include:

- `tCompany`: company identity, fiscal dates, country, readiness flags.
- `tAccount`: chart of accounts, account classes, balance fields, bank flags.
- `tJourEnt`: journal entry headers.
- `tJEntAct`: journal account lines.
- `tJEntTax`: tax details linked to journal lines.
- `tJEntPrj`: project allocation details.
- `tLinkAct`: linked system accounts such as cash, AR, AP, retained earnings,
  discounts, freight, payroll, and tax accounts.
- `tCustomr`, `tCusTr`, `tCusTrDt`: customer master and AR activity.
- `tVendor`, `tVenTr`, `tVenTrDt`: vendor master and AP activity.
- `tBRInfo`, `tBRTr`, `tBRSum`: bank reconciliation configuration, transaction
  status, and reconciliation summaries.
- `tRptOpts`, `tRptCols`, `tRptFrmt`: report options, columns, and formatting.
- `tUser`, `tUserLog`: application users, permissions, and session logging.

## Transaction Flow

1. A user opens a company file through the desktop application.
2. The paired `.SDW` workgroup file participates in database access control.
3. The app loads company settings, fiscal dates, linked accounts, currencies,
   and user permissions.
4. A user enters a transaction through a module such as GL, AP, AR, receipt,
   payment, payroll, inventory, or bank reconciliation.
5. The system writes a journal header to `tJourEnt`.
6. Account movements are written to `tJEntAct`.
7. Tax, project, currency, customer, vendor, inventory, payroll, or bank records
   are written where relevant.
8. Summary balances and reporting data are maintained.
9. Reports are rendered through Crystal Reports or exported to files.

## Critical Accounting Caution

`tJEntAct.dAmount` must not be interpreted as "positive debit, negative credit"
without account-class context. Sample data shows positive values on both asset
and revenue-side movements. Migration must validate balances and financial
statements against Sage reports rather than relying on naive signed summation.

## Known Constraints

- Production Gentrimed databases are credential-protected.
- Business rules are embedded in proprietary binaries and cannot be fully read.
- Report workbooks may include manual adjustments outside the Sage database.
- The legacy system relies on Jet file locking and network file placement.
- The original readme warns that unsafe network hosting can damage accounting
  data.
