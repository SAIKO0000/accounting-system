-- Core finance schema for the modernized accounting system.
-- This schema is intentionally focused on v1: company, accounts, journals,
-- AP, AR, bank reconciliation, users, permissions, audit, and migration refs.

CREATE SCHEMA IF NOT EXISTS core;

CREATE TYPE core.account_nature AS ENUM (
  'asset',
  'liability',
  'equity',
  'revenue',
  'expense'
);

CREATE TYPE core.account_status AS ENUM (
  'active',
  'inactive'
);

CREATE TYPE core.period_status AS ENUM (
  'open',
  'closed',
  'locked'
);

CREATE TYPE core.journal_status AS ENUM (
  'draft',
  'posted',
  'reversed'
);

CREATE TYPE core.subledger_type AS ENUM (
  'none',
  'accounts_payable',
  'accounts_receivable',
  'bank'
);

CREATE TYPE core.audit_event_type AS ENUM (
  'create',
  'update',
  'delete',
  'post',
  'reverse',
  'approve',
  'import',
  'export',
  'login',
  'logout',
  'authorization_failure',
  'permission_change'
);

CREATE TABLE core.companies (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  name TEXT NOT NULL,
  country_code CHAR(2) NOT NULL DEFAULT 'PH',
  base_currency CHAR(3) NOT NULL DEFAULT 'PHP',
  legacy_source_name TEXT,
  legacy_company_id TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (name)
);

CREATE TABLE core.fiscal_years (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  company_id BIGINT NOT NULL REFERENCES core.companies(id),
  name TEXT NOT NULL,
  starts_on DATE NOT NULL,
  ends_on DATE NOT NULL,
  status core.period_status NOT NULL DEFAULT 'open',
  CHECK (starts_on <= ends_on),
  UNIQUE (company_id, name),
  UNIQUE (company_id, starts_on, ends_on)
);

CREATE TABLE core.fiscal_periods (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  fiscal_year_id BIGINT NOT NULL REFERENCES core.fiscal_years(id),
  period_number INTEGER NOT NULL,
  name TEXT NOT NULL,
  starts_on DATE NOT NULL,
  ends_on DATE NOT NULL,
  status core.period_status NOT NULL DEFAULT 'open',
  CHECK (period_number BETWEEN 1 AND 13),
  CHECK (starts_on <= ends_on),
  UNIQUE (fiscal_year_id, period_number)
);

CREATE TABLE core.users (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  external_identity_id TEXT NOT NULL,
  display_name TEXT NOT NULL,
  email TEXT,
  is_active BOOLEAN NOT NULL DEFAULT true,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (external_identity_id),
  UNIQUE (email)
);

CREATE TABLE core.roles (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL
);

CREATE TABLE core.permissions (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code TEXT NOT NULL UNIQUE,
  description TEXT NOT NULL
);

CREATE TABLE core.role_permissions (
  role_id BIGINT NOT NULL REFERENCES core.roles(id) ON DELETE CASCADE,
  permission_id BIGINT NOT NULL REFERENCES core.permissions(id) ON DELETE CASCADE,
  PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE core.user_roles (
  user_id BIGINT NOT NULL REFERENCES core.users(id) ON DELETE CASCADE,
  company_id BIGINT NOT NULL REFERENCES core.companies(id) ON DELETE CASCADE,
  role_id BIGINT NOT NULL REFERENCES core.roles(id) ON DELETE CASCADE,
  PRIMARY KEY (user_id, company_id, role_id)
);

CREATE TABLE core.accounts (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  company_id BIGINT NOT NULL REFERENCES core.companies(id),
  code TEXT NOT NULL,
  name TEXT NOT NULL,
  nature core.account_nature NOT NULL,
  status core.account_status NOT NULL DEFAULT 'active',
  parent_account_id BIGINT REFERENCES core.accounts(id),
  is_posting_account BOOLEAN NOT NULL DEFAULT true,
  is_bank_account BOOLEAN NOT NULL DEFAULT false,
  legacy_account_id TEXT,
  legacy_account_class TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  CHECK (code <> ''),
  CHECK (name <> ''),
  CHECK (parent_account_id IS NULL OR parent_account_id <> id),
  UNIQUE (company_id, code)
);

CREATE TABLE core.tax_codes (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  company_id BIGINT NOT NULL REFERENCES core.companies(id),
  code TEXT NOT NULL,
  name TEXT NOT NULL,
  rate NUMERIC(9, 6) NOT NULL DEFAULT 0,
  liability_account_id BIGINT REFERENCES core.accounts(id),
  receivable_account_id BIGINT REFERENCES core.accounts(id),
  is_active BOOLEAN NOT NULL DEFAULT true,
  UNIQUE (company_id, code)
);

CREATE TABLE core.business_partners (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  company_id BIGINT NOT NULL REFERENCES core.companies(id),
  partner_type TEXT NOT NULL,
  name TEXT NOT NULL,
  contact_name TEXT,
  email TEXT,
  phone TEXT,
  tax_identifier TEXT,
  legacy_partner_id TEXT,
  is_active BOOLEAN NOT NULL DEFAULT true,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  CHECK (partner_type IN ('customer', 'vendor', 'both')),
  UNIQUE (company_id, partner_type, name)
);

CREATE TABLE core.journals (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  company_id BIGINT NOT NULL REFERENCES core.companies(id),
  fiscal_period_id BIGINT REFERENCES core.fiscal_periods(id),
  journal_number TEXT NOT NULL,
  journal_date DATE NOT NULL,
  status core.journal_status NOT NULL DEFAULT 'draft',
  source_module TEXT NOT NULL DEFAULT 'general_ledger',
  source_reference TEXT,
  memo TEXT,
  currency CHAR(3) NOT NULL,
  exchange_rate NUMERIC(18, 8) NOT NULL DEFAULT 1,
  reversed_journal_id BIGINT REFERENCES core.journals(id),
  posted_at TIMESTAMPTZ,
  posted_by_user_id BIGINT REFERENCES core.users(id),
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  created_by_user_id BIGINT REFERENCES core.users(id),
  legacy_journal_id TEXT,
  legacy_source_table TEXT,
  CHECK (exchange_rate > 0),
  CHECK (
    (status = 'posted' AND posted_at IS NOT NULL AND posted_by_user_id IS NOT NULL)
    OR status <> 'posted'
  ),
  UNIQUE (company_id, journal_number)
);

CREATE TABLE core.journal_lines (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  journal_id BIGINT NOT NULL REFERENCES core.journals(id) ON DELETE CASCADE,
  line_number INTEGER NOT NULL,
  account_id BIGINT NOT NULL REFERENCES core.accounts(id),
  debit NUMERIC(19, 4) NOT NULL DEFAULT 0,
  credit NUMERIC(19, 4) NOT NULL DEFAULT 0,
  description TEXT,
  business_partner_id BIGINT REFERENCES core.business_partners(id),
  tax_code_id BIGINT REFERENCES core.tax_codes(id),
  subledger_type core.subledger_type NOT NULL DEFAULT 'none',
  legacy_line_id TEXT,
  legacy_signed_amount NUMERIC(19, 4),
  CHECK (line_number > 0),
  CHECK (debit >= 0),
  CHECK (credit >= 0),
  CHECK ((debit > 0 AND credit = 0) OR (credit > 0 AND debit = 0)),
  UNIQUE (journal_id, line_number)
);

CREATE TABLE core.journal_balances (
  journal_id BIGINT PRIMARY KEY REFERENCES core.journals(id) ON DELETE CASCADE,
  total_debit NUMERIC(19, 4) NOT NULL,
  total_credit NUMERIC(19, 4) NOT NULL,
  line_count INTEGER NOT NULL,
  CHECK (line_count >= 2),
  CHECK (total_debit = total_credit)
);

CREATE TABLE core.ap_documents (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  company_id BIGINT NOT NULL REFERENCES core.companies(id),
  vendor_id BIGINT NOT NULL REFERENCES core.business_partners(id),
  journal_id BIGINT REFERENCES core.journals(id),
  document_number TEXT NOT NULL,
  document_date DATE NOT NULL,
  due_date DATE,
  original_amount NUMERIC(19, 4) NOT NULL,
  open_amount NUMERIC(19, 4) NOT NULL,
  status TEXT NOT NULL DEFAULT 'open',
  legacy_document_id TEXT,
  CHECK (original_amount >= 0),
  CHECK (open_amount >= 0),
  CHECK (status IN ('open', 'paid', 'void')),
  UNIQUE (company_id, vendor_id, document_number)
);

CREATE TABLE core.ar_documents (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  company_id BIGINT NOT NULL REFERENCES core.companies(id),
  customer_id BIGINT NOT NULL REFERENCES core.business_partners(id),
  journal_id BIGINT REFERENCES core.journals(id),
  document_number TEXT NOT NULL,
  document_date DATE NOT NULL,
  due_date DATE,
  original_amount NUMERIC(19, 4) NOT NULL,
  open_amount NUMERIC(19, 4) NOT NULL,
  status TEXT NOT NULL DEFAULT 'open',
  legacy_document_id TEXT,
  CHECK (original_amount >= 0),
  CHECK (open_amount >= 0),
  CHECK (status IN ('open', 'paid', 'void')),
  UNIQUE (company_id, customer_id, document_number)
);

CREATE TABLE core.bank_reconciliations (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  company_id BIGINT NOT NULL REFERENCES core.companies(id),
  bank_account_id BIGINT NOT NULL REFERENCES core.accounts(id),
  statement_ending_on DATE NOT NULL,
  statement_balance NUMERIC(19, 4) NOT NULL,
  status TEXT NOT NULL DEFAULT 'draft',
  closed_at TIMESTAMPTZ,
  closed_by_user_id BIGINT REFERENCES core.users(id),
  legacy_reconciliation_id TEXT,
  CHECK (status IN ('draft', 'closed', 'void')),
  UNIQUE (company_id, bank_account_id, statement_ending_on)
);

CREATE TABLE core.bank_reconciliation_lines (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  reconciliation_id BIGINT NOT NULL REFERENCES core.bank_reconciliations(id) ON DELETE CASCADE,
  journal_line_id BIGINT REFERENCES core.journal_lines(id),
  statement_reference TEXT,
  statement_date DATE,
  cleared_amount NUMERIC(19, 4) NOT NULL,
  status TEXT NOT NULL DEFAULT 'matched',
  CHECK (status IN ('matched', 'adjustment', 'outstanding', 'void'))
);

CREATE TABLE core.migration_batches (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  source_name TEXT NOT NULL,
  source_path TEXT,
  started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  completed_at TIMESTAMPTZ,
  status TEXT NOT NULL DEFAULT 'running',
  notes TEXT,
  CHECK (status IN ('running', 'completed', 'failed', 'void'))
);

CREATE TABLE core.migration_source_refs (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  migration_batch_id BIGINT NOT NULL REFERENCES core.migration_batches(id),
  source_table TEXT NOT NULL,
  source_key TEXT NOT NULL,
  target_table TEXT NOT NULL,
  target_id BIGINT NOT NULL,
  raw_hash TEXT,
  UNIQUE (migration_batch_id, source_table, source_key, target_table)
);

CREATE TABLE core.audit_events (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  company_id BIGINT REFERENCES core.companies(id),
  actor_user_id BIGINT REFERENCES core.users(id),
  event_type core.audit_event_type NOT NULL,
  entity_type TEXT NOT NULL,
  entity_id TEXT,
  event_timestamp TIMESTAMPTZ NOT NULL DEFAULT now(),
  source_ip INET,
  session_id TEXT,
  reason TEXT,
  metadata JSONB NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX idx_accounts_company ON core.accounts(company_id);
CREATE INDEX idx_journals_company_date ON core.journals(company_id, journal_date);
CREATE INDEX idx_journal_lines_journal ON core.journal_lines(journal_id);
CREATE INDEX idx_journal_lines_account ON core.journal_lines(account_id);
CREATE INDEX idx_ap_documents_company_vendor ON core.ap_documents(company_id, vendor_id);
CREATE INDEX idx_ar_documents_company_customer ON core.ar_documents(company_id, customer_id);
CREATE INDEX idx_audit_events_company_time ON core.audit_events(company_id, event_timestamp);
CREATE INDEX idx_audit_events_entity ON core.audit_events(entity_type, entity_id);

CREATE OR REPLACE FUNCTION core.assert_journal_can_post(target_journal_id BIGINT)
RETURNS VOID
LANGUAGE plpgsql
AS $$
DECLARE
  line_count INTEGER;
  total_debit NUMERIC(19, 4);
  total_credit NUMERIC(19, 4);
BEGIN
  SELECT
    COUNT(*),
    COALESCE(SUM(debit), 0),
    COALESCE(SUM(credit), 0)
  INTO line_count, total_debit, total_credit
  FROM core.journal_lines
  WHERE journal_id = target_journal_id;

  IF line_count < 2 THEN
    RAISE EXCEPTION 'journal % must have at least two lines before posting', target_journal_id;
  END IF;

  IF total_debit <> total_credit THEN
    RAISE EXCEPTION 'journal % is not balanced: debit %, credit %', target_journal_id, total_debit, total_credit;
  END IF;

  INSERT INTO core.journal_balances (journal_id, total_debit, total_credit, line_count)
  VALUES (target_journal_id, total_debit, total_credit, line_count)
  ON CONFLICT (journal_id) DO UPDATE
  SET
    total_debit = EXCLUDED.total_debit,
    total_credit = EXCLUDED.total_credit,
    line_count = EXCLUDED.line_count;
END;
$$;

CREATE OR REPLACE FUNCTION core.enforce_journal_posting_rules()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
  IF TG_OP = 'UPDATE' AND OLD.status = 'posted' THEN
    IF NEW.status <> 'reversed'
      OR NEW.reversed_journal_id IS DISTINCT FROM OLD.reversed_journal_id
      OR NEW.company_id <> OLD.company_id
      OR NEW.journal_number <> OLD.journal_number
      OR NEW.journal_date <> OLD.journal_date
      OR NEW.source_module <> OLD.source_module
      OR NEW.source_reference IS DISTINCT FROM OLD.source_reference
      OR NEW.memo IS DISTINCT FROM OLD.memo
      OR NEW.currency <> OLD.currency
      OR NEW.exchange_rate <> OLD.exchange_rate
      OR NEW.posted_at <> OLD.posted_at
      OR NEW.posted_by_user_id <> OLD.posted_by_user_id THEN
      RAISE EXCEPTION 'posted journal % is immutable; use reversal workflow', OLD.id;
    END IF;
  END IF;

  IF NEW.status = 'posted' AND (TG_OP = 'INSERT' OR OLD.status <> 'posted') THEN
    PERFORM core.assert_journal_can_post(NEW.id);
  END IF;

  RETURN NEW;
END;
$$;

CREATE TRIGGER trg_journals_posting_rules
BEFORE INSERT OR UPDATE ON core.journals
FOR EACH ROW
EXECUTE FUNCTION core.enforce_journal_posting_rules();

CREATE OR REPLACE FUNCTION core.prevent_posted_journal_line_mutation()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
  parent_status core.journal_status;
  target_journal_id BIGINT;
BEGIN
  target_journal_id = COALESCE(NEW.journal_id, OLD.journal_id);

  SELECT status INTO parent_status
  FROM core.journals
  WHERE id = target_journal_id;

  IF parent_status = 'posted' THEN
    RAISE EXCEPTION 'journal lines for posted journal % are immutable', target_journal_id;
  END IF;

  RETURN COALESCE(NEW, OLD);
END;
$$;

CREATE TRIGGER trg_journal_lines_immutable_after_posting
BEFORE INSERT OR UPDATE OR DELETE ON core.journal_lines
FOR EACH ROW
EXECUTE FUNCTION core.prevent_posted_journal_line_mutation();

-- Seed baseline permissions and roles.
INSERT INTO core.permissions (code, description) VALUES
  ('company.view', 'View company setup'),
  ('accounts.manage', 'Manage chart of accounts'),
  ('journal.view', 'View journals'),
  ('journal.create', 'Create journal drafts'),
  ('journal.post', 'Post journals'),
  ('journal.reverse', 'Reverse posted journals'),
  ('ap.manage', 'Manage accounts payable'),
  ('ar.manage', 'Manage accounts receivable'),
  ('bank_reconciliation.manage', 'Manage bank reconciliation'),
  ('reports.view', 'View financial reports'),
  ('reports.export', 'Export financial reports'),
  ('users.manage', 'Manage users and roles'),
  ('migration.run', 'Run migration imports'),
  ('audit.view', 'View audit trail')
ON CONFLICT (code) DO NOTHING;

INSERT INTO core.roles (code, name) VALUES
  ('system_admin', 'System Administrator'),
  ('finance_admin', 'Finance Administrator'),
  ('gl_accountant', 'GL Accountant'),
  ('ap_clerk', 'AP Clerk'),
  ('ar_clerk', 'AR Clerk'),
  ('bank_reconciliation_user', 'Bank Reconciliation User'),
  ('report_viewer', 'Report Viewer'),
  ('auditor', 'Auditor'),
  ('migration_operator', 'Migration Operator')
ON CONFLICT (code) DO NOTHING;

INSERT INTO core.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM core.roles r
CROSS JOIN core.permissions p
WHERE r.code = 'system_admin'
ON CONFLICT DO NOTHING;

INSERT INTO core.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM core.roles r
JOIN core.permissions p ON p.code IN (
  'company.view',
  'accounts.manage',
  'journal.view',
  'journal.create',
  'journal.post',
  'journal.reverse',
  'ap.manage',
  'ar.manage',
  'bank_reconciliation.manage',
  'reports.view',
  'reports.export',
  'audit.view'
)
WHERE r.code = 'finance_admin'
ON CONFLICT DO NOTHING;

INSERT INTO core.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM core.roles r
JOIN core.permissions p ON p.code IN (
  'company.view',
  'journal.view',
  'journal.create',
  'journal.post',
  'journal.reverse',
  'reports.view'
)
WHERE r.code = 'gl_accountant'
ON CONFLICT DO NOTHING;

INSERT INTO core.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM core.roles r
JOIN core.permissions p ON p.code IN (
  'company.view',
  'journal.view',
  'journal.create',
  'ap.manage',
  'reports.view'
)
WHERE r.code = 'ap_clerk'
ON CONFLICT DO NOTHING;

INSERT INTO core.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM core.roles r
JOIN core.permissions p ON p.code IN (
  'company.view',
  'journal.view',
  'journal.create',
  'ar.manage',
  'reports.view'
)
WHERE r.code = 'ar_clerk'
ON CONFLICT DO NOTHING;

INSERT INTO core.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM core.roles r
JOIN core.permissions p ON p.code IN (
  'company.view',
  'journal.view',
  'journal.create',
  'bank_reconciliation.manage',
  'reports.view'
)
WHERE r.code = 'bank_reconciliation_user'
ON CONFLICT DO NOTHING;

INSERT INTO core.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM core.roles r
JOIN core.permissions p ON p.code IN (
  'company.view',
  'reports.view',
  'reports.export'
)
WHERE r.code = 'report_viewer'
ON CONFLICT DO NOTHING;

INSERT INTO core.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM core.roles r
JOIN core.permissions p ON p.code IN (
  'company.view',
  'journal.view',
  'reports.view',
  'reports.export',
  'audit.view'
)
WHERE r.code = 'auditor'
ON CONFLICT DO NOTHING;

INSERT INTO core.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM core.roles r
JOIN core.permissions p ON p.code IN (
  'company.view',
  'migration.run',
  'reports.view',
  'audit.view'
)
WHERE r.code = 'migration_operator'
ON CONFLICT DO NOTHING;
