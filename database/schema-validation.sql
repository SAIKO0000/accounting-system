\set ON_ERROR_STOP on

SELECT count(*) AS core_tables
FROM information_schema.tables
WHERE table_schema = 'core'
  AND table_type = 'BASE TABLE';

SELECT count(*) AS roles FROM core.roles;
SELECT count(*) AS permissions FROM core.permissions;
SELECT count(*) AS role_permissions FROM core.role_permissions;

INSERT INTO core.companies (name)
VALUES ('Schema Test Company')
RETURNING id, name, base_currency;

INSERT INTO core.users (external_identity_id, display_name, email)
VALUES ('test-user', 'Test User', 'test@example.local')
RETURNING id;

INSERT INTO core.accounts (company_id, code, name, nature)
VALUES
  (1, '1000', 'Cash', 'asset'),
  (1, '4000', 'Revenue', 'revenue')
RETURNING id, code, nature;

INSERT INTO core.journals (
  company_id,
  journal_number,
  journal_date,
  status,
  source_module,
  currency,
  created_by_user_id
)
VALUES (
  1,
  'JV-TEST-001',
  CURRENT_DATE,
  'draft',
  'general_ledger',
  'PHP',
  1
)
RETURNING id;

INSERT INTO core.journal_lines (journal_id, line_number, account_id, debit, credit)
VALUES
  (1, 1, 1, 100.00, 0),
  (1, 2, 2, 0, 100.00);

UPDATE core.journals
SET
  status = 'posted',
  posted_at = now(),
  posted_by_user_id = 1
WHERE id = 1;

SELECT * FROM core.journal_balances WHERE journal_id = 1;

INSERT INTO core.journals (
  company_id,
  journal_number,
  journal_date,
  status,
  source_module,
  currency,
  created_by_user_id
)
VALUES (
  1,
  'JV-TEST-DRAFT-INVALID',
  CURRENT_DATE,
  'draft',
  'general_ledger',
  'PHP',
  1
);

DO $$
BEGIN
  BEGIN
    INSERT INTO core.accounts (company_id, code, name, nature)
    VALUES (1, '1000', 'Duplicate Cash', 'asset');
    RAISE EXCEPTION 'duplicate account check failed to reject invalid data';
  EXCEPTION WHEN unique_violation THEN
    RAISE NOTICE 'duplicate account check passed';
  END;

  BEGIN
    INSERT INTO core.journal_lines (journal_id, line_number, account_id, debit, credit)
    SELECT id, 1, 1, 10.00, 5.00
    FROM core.journals
    WHERE journal_number = 'JV-TEST-DRAFT-INVALID';
    RAISE EXCEPTION 'dual-sided journal line check failed to reject invalid data';
  EXCEPTION WHEN check_violation THEN
    RAISE NOTICE 'dual-sided journal line check passed';
  END;

  BEGIN
    INSERT INTO core.journal_lines (journal_id, line_number, account_id, debit, credit)
    VALUES (1, 3, 1, 10.00, 0);
    RAISE EXCEPTION 'posted journal line immutability check failed to reject invalid data';
  EXCEPTION WHEN raise_exception THEN
    RAISE NOTICE 'posted journal line immutability check passed';
  END;

  BEGIN
    UPDATE core.journals SET memo = 'mutated after posting' WHERE id = 1;
    RAISE EXCEPTION 'posted journal immutability check failed to reject invalid data';
  EXCEPTION WHEN raise_exception THEN
    RAISE NOTICE 'posted journal immutability check passed';
  END;

  BEGIN
    INSERT INTO core.journals (
      company_id,
      journal_number,
      journal_date,
      status,
      source_module,
      currency,
      posted_at,
      posted_by_user_id
    )
    VALUES (
      1,
      'JV-TEST-UNBALANCED',
      CURRENT_DATE,
      'posted',
      'general_ledger',
      'PHP',
      now(),
      1
    );
    RAISE EXCEPTION 'posting validation failed to reject journal without lines';
  EXCEPTION WHEN raise_exception THEN
    RAISE NOTICE 'posting validation check passed';
  END;
END $$;
