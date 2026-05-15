\set ON_ERROR_STOP on

INSERT INTO core.companies (name, country_code, base_currency, legacy_source_name)
VALUES ('Gentrimed Development Company', 'PH', 'PHP', 'dev-seed')
ON CONFLICT (name) DO NOTHING;

INSERT INTO core.users (external_identity_id, display_name, email)
VALUES ('api-dev', 'API Development User', 'api-dev@example.local')
ON CONFLICT (external_identity_id) DO NOTHING;

WITH company AS (
  SELECT id FROM core.companies WHERE name = 'Gentrimed Development Company'
),
dev_user AS (
  SELECT id FROM core.users WHERE external_identity_id = 'api-dev'
),
role AS (
  SELECT id FROM core.roles WHERE code = 'finance_admin'
)
INSERT INTO core.user_roles (user_id, company_id, role_id)
SELECT dev_user.id, company.id, role.id
FROM dev_user
CROSS JOIN company
CROSS JOIN role
ON CONFLICT DO NOTHING;

WITH company AS (
  SELECT id FROM core.companies WHERE name = 'Gentrimed Development Company'
)
INSERT INTO core.accounts (company_id, code, name, nature, is_bank_account)
SELECT company.id, account.code, account.name, account.nature::core.account_nature, account.is_bank_account
FROM company
CROSS JOIN (
  VALUES
    ('1000', 'Cash', 'asset', true),
    ('1200', 'Accounts Receivable', 'asset', false),
    ('2100', 'Accounts Payable', 'liability', false),
    ('4000', 'Revenue', 'revenue', false),
    ('5000', 'Operating Expense', 'expense', false)
) AS account(code, name, nature, is_bank_account)
ON CONFLICT (company_id, code) DO NOTHING;

WITH company AS (
  SELECT id FROM core.companies WHERE name = 'Gentrimed Development Company'
)
INSERT INTO core.business_partners (company_id, partner_type, name, contact_name, email, phone, tax_identifier)
SELECT company.id, partner.partner_type, partner.name, partner.contact_name, partner.email, partner.phone, partner.tax_identifier
FROM company
CROSS JOIN (
  VALUES
    ('vendor', 'Development Vendor', 'Vendor Contact', 'vendor@example.local', '+63-000-0000', 'DEV-VENDOR'),
    ('customer', 'Development Customer', 'Customer Contact', 'customer@example.local', '+63-000-0001', 'DEV-CUSTOMER')
) AS partner(partner_type, name, contact_name, email, phone, tax_identifier)
ON CONFLICT (company_id, partner_type, name) DO NOTHING;
