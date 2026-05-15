using Accounting.Core;
using Npgsql;
using System.Text.Json;

namespace Accounting.Api;

public sealed class AccountingRepository
{
    private const string DevelopmentUserExternalId = "api-dev";
    private readonly NpgsqlDataSource _dataSource;

    public AccountingRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<Account>> GetAccountsAsync(long companyId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, code, name, nature::text, status::text
            FROM core.accounts
            WHERE company_id = @company_id
            ORDER BY code;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("company_id", companyId);

        var accounts = new List<Account>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(new Account(
                Id: reader.GetInt64(0).ToString(),
                Code: reader.GetString(1),
                Name: reader.GetString(2),
                Nature: ParseAccountNature(reader.GetString(3)),
                IsActive: reader.GetString(4).Equals("active", StringComparison.OrdinalIgnoreCase)));
        }

        return accounts;
    }

    public async Task<IReadOnlyList<BankAccountRow>> GetBankAccountsAsync(long companyId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, code, name, status::text
            FROM core.accounts
            WHERE company_id = @company_id
              AND is_bank_account = true
            ORDER BY code;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("company_id", companyId);

        var accounts = new List<BankAccountRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(new BankAccountRow(
                reader.GetInt64(0).ToString(),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }

        return accounts;
    }

    public async Task<UserAuthorizationContext?> GetUserAuthorizationAsync(string externalIdentityId, long companyId, CancellationToken cancellationToken)
    {
        const string userSql = """
            SELECT id, external_identity_id, display_name, email, is_active
            FROM core.users
            WHERE external_identity_id = @external_identity_id;
            """;

        await using var userCommand = _dataSource.CreateCommand(userSql);
        userCommand.Parameters.AddWithValue("external_identity_id", externalIdentityId);

        await using var userReader = await userCommand.ExecuteReaderAsync(cancellationToken);
        if (!await userReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var userId = userReader.GetInt64(0);
        var displayName = userReader.GetString(2);
        var email = userReader.IsDBNull(3) ? null : userReader.GetString(3);
        var isActive = userReader.GetBoolean(4);
        await userReader.DisposeAsync();

        const string permissionsSql = """
            SELECT DISTINCT p.code
            FROM core.user_roles ur
            JOIN core.role_permissions rp ON rp.role_id = ur.role_id
            JOIN core.permissions p ON p.id = rp.permission_id
            WHERE ur.user_id = @user_id
              AND ur.company_id = @company_id
            ORDER BY p.code;
            """;

        await using var permissionsCommand = _dataSource.CreateCommand(permissionsSql);
        permissionsCommand.Parameters.AddWithValue("user_id", userId);
        permissionsCommand.Parameters.AddWithValue("company_id", companyId);

        var permissions = new List<string>();
        await using var permissionsReader = await permissionsCommand.ExecuteReaderAsync(cancellationToken);
        while (await permissionsReader.ReadAsync(cancellationToken))
        {
            permissions.Add(permissionsReader.GetString(0));
        }

        return new UserAuthorizationContext(
            userId.ToString(),
            externalIdentityId,
            displayName,
            email,
            isActive,
            permissions);
    }

    public async Task<IReadOnlyList<BusinessPartnerRow>> GetVendorsAsync(long companyId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, contact_name, email, phone, tax_identifier, is_active
            FROM core.business_partners
            WHERE company_id = @company_id
              AND partner_type IN ('vendor', 'both')
            ORDER BY name;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("company_id", companyId);

        var vendors = new List<BusinessPartnerRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            vendors.Add(new BusinessPartnerRow(
                reader.GetInt64(0).ToString(),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetBoolean(6)));
        }

        return vendors;
    }

    public async Task<IReadOnlyList<BusinessPartnerRow>> GetCustomersAsync(long companyId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, contact_name, email, phone, tax_identifier, is_active
            FROM core.business_partners
            WHERE company_id = @company_id
              AND partner_type IN ('customer', 'both')
            ORDER BY name;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("company_id", companyId);

        var customers = new List<BusinessPartnerRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            customers.Add(new BusinessPartnerRow(
                reader.GetInt64(0).ToString(),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetBoolean(6)));
        }

        return customers;
    }

    public async Task<IReadOnlyList<AccountsPayableDocumentRow>> GetAccountsPayableDocumentsAsync(long companyId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              ap.id,
              ap.vendor_id,
              bp.name,
              ap.journal_id,
              ap.document_number,
              ap.document_date,
              ap.due_date,
              ap.original_amount,
              ap.open_amount,
              ap.status,
              ap.legacy_document_id
            FROM core.ap_documents ap
            JOIN core.business_partners bp ON bp.id = ap.vendor_id
            WHERE ap.company_id = @company_id
            ORDER BY ap.document_date, ap.id;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("company_id", companyId);

        var documents = new List<AccountsPayableDocumentRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(ReadAccountsPayableDocument(reader));
        }

        return documents;
    }

    public async Task<long?> GetAccountsPayableDocumentCompanyIdAsync(long documentId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT company_id
            FROM core.ap_documents
            WHERE id = @document_id;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("document_id", documentId);

        return await command.ExecuteScalarAsync(cancellationToken) is long companyId
            ? companyId
            : null;
    }

    public async Task<IReadOnlyList<AccountsReceivableDocumentRow>> GetAccountsReceivableDocumentsAsync(long companyId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              ar.id,
              ar.customer_id,
              bp.name,
              ar.journal_id,
              ar.document_number,
              ar.document_date,
              ar.due_date,
              ar.original_amount,
              ar.open_amount,
              ar.status,
              ar.legacy_document_id
            FROM core.ar_documents ar
            JOIN core.business_partners bp ON bp.id = ar.customer_id
            WHERE ar.company_id = @company_id
            ORDER BY ar.document_date, ar.id;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("company_id", companyId);

        var documents = new List<AccountsReceivableDocumentRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(ReadAccountsReceivableDocument(reader));
        }

        return documents;
    }

    public async Task<long?> GetAccountsReceivableDocumentCompanyIdAsync(long documentId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT company_id
            FROM core.ar_documents
            WHERE id = @document_id;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("document_id", documentId);

        return await command.ExecuteScalarAsync(cancellationToken) is long companyId
            ? companyId
            : null;
    }

    public async Task<IReadOnlyList<PostedJournal>> GetPostedJournalsAsync(long companyId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id
            FROM core.journals
            WHERE company_id = @company_id
              AND status IN ('posted', 'reversed')
            ORDER BY journal_date, id;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("company_id", companyId);

        var journalIds = new List<long>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                journalIds.Add(reader.GetInt64(0));
            }
        }

        var journals = new List<PostedJournal>();
        foreach (var journalId in journalIds)
        {
            var journal = await GetPostedJournalAsync(journalId.ToString(), cancellationToken);
            if (journal is not null)
            {
                journals.Add(journal);
            }
        }

        return journals;
    }

    public async Task<PostedJournal?> GetPostedJournalAsync(string journalId, CancellationToken cancellationToken)
    {
        if (!long.TryParse(journalId, out var parsedJournalId))
        {
            return null;
        }

        const string journalSql = """
            SELECT
              id,
              company_id,
              journal_number,
              journal_date,
              status::text,
              source_module,
              currency,
              COALESCE(total_debit, 0),
              COALESCE(total_credit, 0),
              memo,
              source_reference,
              reversed_journal_id
            FROM core.journals
            LEFT JOIN core.journal_balances ON core.journal_balances.journal_id = core.journals.id
            WHERE core.journals.id = @journal_id;
            """;

        await using var journalCommand = _dataSource.CreateCommand(journalSql);
        journalCommand.Parameters.AddWithValue("journal_id", parsedJournalId);

        await using var journalReader = await journalCommand.ExecuteReaderAsync(cancellationToken);
        if (!await journalReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var id = journalReader.GetInt64(0).ToString();
        var companyId = journalReader.GetInt64(1).ToString();
        var journalNumber = journalReader.GetString(2);
        var journalDate = DateOnly.FromDateTime(journalReader.GetDateTime(3));
        var status = ParseJournalStatus(journalReader.GetString(4));
        var sourceModule = journalReader.GetString(5);
        var currency = journalReader.GetString(6);
        var totalDebit = journalReader.GetDecimal(7);
        var totalCredit = journalReader.GetDecimal(8);
        var memo = journalReader.IsDBNull(9) ? null : journalReader.GetString(9);
        var sourceReference = journalReader.IsDBNull(10) ? null : journalReader.GetString(10);
        var reversedJournalId = journalReader.IsDBNull(11) ? null : journalReader.GetInt64(11).ToString();

        await journalReader.DisposeAsync();

        var lines = await GetJournalLinesAsync(parsedJournalId, cancellationToken);

        return new PostedJournal(
            id,
            companyId,
            journalNumber,
            journalDate,
            status,
            sourceModule,
            currency,
            lines,
            totalDebit,
            totalCredit,
            memo,
            sourceReference,
            reversedJournalId);
    }

    public async Task<PostedJournal> SavePostedJournalAsync(PostedJournal journal, CancellationToken cancellationToken)
    {
        if (!long.TryParse(journal.CompanyId, out var companyId))
        {
            throw new InvalidOperationException("CompanyId must be a numeric database ID.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var userId = await EnsureDevelopmentUserAsync(connection, transaction, cancellationToken);
        var persistedJournalId = await InsertPostedJournalAsync(connection, transaction, journal, companyId, userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetPostedJournalAsync(persistedJournalId.ToString(), cancellationToken)
            ?? throw new InvalidOperationException($"Posted journal {persistedJournalId} could not be loaded after save.");
    }

    public async Task<AccountsPayableDocumentRow> SaveAccountsPayableBillAsync(AccountsPayableBillDraft bill, CancellationToken cancellationToken)
    {
        ValidatePositiveAmount(bill.Amount);

        if (!long.TryParse(bill.CompanyId, out var companyId))
        {
            throw new ArgumentException("CompanyId must be a numeric database ID.");
        }

        if (!long.TryParse(bill.VendorId, out var vendorId))
        {
            throw new ArgumentException("VendorId must be a numeric database ID.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var userId = await EnsureDevelopmentUserAsync(connection, transaction, cancellationToken);
        await EnsureBusinessPartnerTypeAsync(connection, transaction, companyId, vendorId, ["vendor", "both"], cancellationToken);

        var accounts = await GetAccountsAsync(connection, transaction, companyId, cancellationToken);
        var postingEngine = new PostingEngine(accounts);
        var postingResult = postingEngine.Post(new JournalDraft(
            bill.CompanyId,
            bill.JournalNumber,
            bill.DocumentDate,
            "accounts_payable",
            bill.Currency,
            [
                new JournalLineDraft(bill.DebitAccountId, bill.Amount, 0, $"AP bill {bill.DocumentNumber}"),
                new JournalLineDraft(bill.AccountsPayableAccountId, 0, bill.Amount, $"AP bill {bill.DocumentNumber}")
            ],
            bill.Memo,
            bill.DocumentNumber));

        if (!postingResult.IsPosted)
        {
            throw new ArgumentException(string.Join(" ", postingResult.Validation.Errors));
        }

        var journalId = await InsertPostedJournalAsync(connection, transaction, postingResult.Journal!, companyId, userId, cancellationToken);

        const string insertDocumentSql = """
            INSERT INTO core.ap_documents (
              company_id,
              vendor_id,
              journal_id,
              document_number,
              document_date,
              due_date,
              original_amount,
              open_amount,
              status,
              legacy_document_id
            )
            VALUES (
              @company_id,
              @vendor_id,
              @journal_id,
              @document_number,
              @document_date,
              @due_date,
              @original_amount,
              @open_amount,
              'open',
              @legacy_document_id
            )
            RETURNING id;
            """;

        await using var insertDocumentCommand = new NpgsqlCommand(insertDocumentSql, connection, transaction);
        insertDocumentCommand.Parameters.AddWithValue("company_id", companyId);
        insertDocumentCommand.Parameters.AddWithValue("vendor_id", vendorId);
        insertDocumentCommand.Parameters.AddWithValue("journal_id", journalId);
        insertDocumentCommand.Parameters.AddWithValue("document_number", bill.DocumentNumber);
        insertDocumentCommand.Parameters.AddWithValue("document_date", bill.DocumentDate);
        insertDocumentCommand.Parameters.AddWithValue("due_date", (object?)bill.DueDate ?? DBNull.Value);
        insertDocumentCommand.Parameters.AddWithValue("original_amount", bill.Amount);
        insertDocumentCommand.Parameters.AddWithValue("open_amount", bill.Amount);
        insertDocumentCommand.Parameters.AddWithValue("legacy_document_id", (object?)bill.LegacyDocumentId ?? DBNull.Value);

        var documentId = (long)(await insertDocumentCommand.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("AP document insert did not return an ID."));

        await InsertAuditEventAsync(
            connection,
            transaction,
            companyId,
            userId,
            "post",
            "ap_document",
            documentId.ToString(),
            "AP bill recorded.",
            new
            {
                bill.DocumentNumber,
                bill.DocumentDate,
                bill.DueDate,
                bill.Amount,
                JournalId = journalId,
                VendorId = vendorId
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetAccountsPayableDocumentAsync(documentId, cancellationToken)
            ?? throw new InvalidOperationException($"AP document {documentId} could not be loaded after save.");
    }

    public async Task<AccountsPayableDocumentRow> ApplyAccountsPayablePaymentAsync(long documentId, AccountsPayablePaymentDraft payment, CancellationToken cancellationToken)
    {
        ValidatePositiveAmount(payment.Amount);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var userId = await EnsureDevelopmentUserAsync(connection, transaction, cancellationToken);

        const string documentSql = """
            SELECT company_id, vendor_id, document_number, open_amount, status
            FROM core.ap_documents
            WHERE id = @document_id
            FOR UPDATE;
            """;

        await using var documentCommand = new NpgsqlCommand(documentSql, connection, transaction);
        documentCommand.Parameters.AddWithValue("document_id", documentId);

        await using var documentReader = await documentCommand.ExecuteReaderAsync(cancellationToken);
        if (!await documentReader.ReadAsync(cancellationToken))
        {
            throw new KeyNotFoundException($"AP document {documentId} was not found.");
        }

        var companyId = documentReader.GetInt64(0);
        var vendorId = documentReader.GetInt64(1);
        var documentNumber = documentReader.GetString(2);
        var openAmount = documentReader.GetDecimal(3);
        var status = documentReader.GetString(4);
        await documentReader.DisposeAsync();

        if (!status.Equals("open", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only open AP documents can receive payments.");
        }

        if (payment.Amount > openAmount)
        {
            throw new ArgumentException($"Payment amount {payment.Amount:N2} exceeds open amount {openAmount:N2}.");
        }

        var accounts = await GetAccountsAsync(connection, transaction, companyId, cancellationToken);
        var postingEngine = new PostingEngine(accounts);
        var postingResult = postingEngine.Post(new JournalDraft(
            companyId.ToString(),
            payment.PaymentJournalNumber,
            payment.PaymentDate,
            "accounts_payable",
            payment.Currency,
            [
                new JournalLineDraft(payment.AccountsPayableAccountId, payment.Amount, 0, $"Payment for AP bill {documentNumber}"),
                new JournalLineDraft(payment.CashAccountId, 0, payment.Amount, $"Payment for AP bill {documentNumber}")
            ],
            payment.Memo,
            documentNumber));

        if (!postingResult.IsPosted)
        {
            throw new ArgumentException(string.Join(" ", postingResult.Validation.Errors));
        }

        var journalId = await InsertPostedJournalAsync(connection, transaction, postingResult.Journal!, companyId, userId, cancellationToken);
        var remainingOpenAmount = openAmount - payment.Amount;
        var newStatus = remainingOpenAmount == 0 ? "paid" : "open";

        const string updateDocumentSql = """
            UPDATE core.ap_documents
            SET open_amount = @open_amount,
                status = @status
            WHERE id = @document_id;
            """;

        await using var updateDocumentCommand = new NpgsqlCommand(updateDocumentSql, connection, transaction);
        updateDocumentCommand.Parameters.AddWithValue("document_id", documentId);
        updateDocumentCommand.Parameters.AddWithValue("open_amount", remainingOpenAmount);
        updateDocumentCommand.Parameters.AddWithValue("status", newStatus);
        await updateDocumentCommand.ExecuteNonQueryAsync(cancellationToken);

        await InsertAuditEventAsync(
            connection,
            transaction,
            companyId,
            userId,
            "post",
            "ap_payment",
            journalId.ToString(),
            "AP payment recorded.",
            new
            {
                ApDocumentId = documentId,
                VendorId = vendorId,
                DocumentNumber = documentNumber,
                payment.Amount,
                RemainingOpenAmount = remainingOpenAmount,
                JournalId = journalId
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetAccountsPayableDocumentAsync(documentId, cancellationToken)
            ?? throw new InvalidOperationException($"AP document {documentId} could not be loaded after payment.");
    }

    public async Task<AccountsReceivableDocumentRow> SaveAccountsReceivableInvoiceAsync(AccountsReceivableInvoiceDraft invoice, CancellationToken cancellationToken)
    {
        ValidatePositiveAmount(invoice.Amount);

        if (!long.TryParse(invoice.CompanyId, out var companyId))
        {
            throw new ArgumentException("CompanyId must be a numeric database ID.");
        }

        if (!long.TryParse(invoice.CustomerId, out var customerId))
        {
            throw new ArgumentException("CustomerId must be a numeric database ID.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var userId = await EnsureDevelopmentUserAsync(connection, transaction, cancellationToken);
        await EnsureBusinessPartnerTypeAsync(connection, transaction, companyId, customerId, ["customer", "both"], cancellationToken);

        var accounts = await GetAccountsAsync(connection, transaction, companyId, cancellationToken);
        var postingEngine = new PostingEngine(accounts);
        var postingResult = postingEngine.Post(new JournalDraft(
            invoice.CompanyId,
            invoice.JournalNumber,
            invoice.DocumentDate,
            "accounts_receivable",
            invoice.Currency,
            [
                new JournalLineDraft(invoice.AccountsReceivableAccountId, invoice.Amount, 0, $"AR invoice {invoice.DocumentNumber}"),
                new JournalLineDraft(invoice.RevenueAccountId, 0, invoice.Amount, $"AR invoice {invoice.DocumentNumber}")
            ],
            invoice.Memo,
            invoice.DocumentNumber));

        if (!postingResult.IsPosted)
        {
            throw new ArgumentException(string.Join(" ", postingResult.Validation.Errors));
        }

        var journalId = await InsertPostedJournalAsync(connection, transaction, postingResult.Journal!, companyId, userId, cancellationToken);

        const string insertDocumentSql = """
            INSERT INTO core.ar_documents (
              company_id,
              customer_id,
              journal_id,
              document_number,
              document_date,
              due_date,
              original_amount,
              open_amount,
              status,
              legacy_document_id
            )
            VALUES (
              @company_id,
              @customer_id,
              @journal_id,
              @document_number,
              @document_date,
              @due_date,
              @original_amount,
              @open_amount,
              'open',
              @legacy_document_id
            )
            RETURNING id;
            """;

        await using var insertDocumentCommand = new NpgsqlCommand(insertDocumentSql, connection, transaction);
        insertDocumentCommand.Parameters.AddWithValue("company_id", companyId);
        insertDocumentCommand.Parameters.AddWithValue("customer_id", customerId);
        insertDocumentCommand.Parameters.AddWithValue("journal_id", journalId);
        insertDocumentCommand.Parameters.AddWithValue("document_number", invoice.DocumentNumber);
        insertDocumentCommand.Parameters.AddWithValue("document_date", invoice.DocumentDate);
        insertDocumentCommand.Parameters.AddWithValue("due_date", (object?)invoice.DueDate ?? DBNull.Value);
        insertDocumentCommand.Parameters.AddWithValue("original_amount", invoice.Amount);
        insertDocumentCommand.Parameters.AddWithValue("open_amount", invoice.Amount);
        insertDocumentCommand.Parameters.AddWithValue("legacy_document_id", (object?)invoice.LegacyDocumentId ?? DBNull.Value);

        var documentId = (long)(await insertDocumentCommand.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("AR document insert did not return an ID."));

        await InsertAuditEventAsync(
            connection,
            transaction,
            companyId,
            userId,
            "post",
            "ar_document",
            documentId.ToString(),
            "AR invoice recorded.",
            new
            {
                invoice.DocumentNumber,
                invoice.DocumentDate,
                invoice.DueDate,
                invoice.Amount,
                JournalId = journalId,
                CustomerId = customerId
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetAccountsReceivableDocumentAsync(documentId, cancellationToken)
            ?? throw new InvalidOperationException($"AR document {documentId} could not be loaded after save.");
    }

    public async Task<AccountsReceivableDocumentRow> ApplyAccountsReceivableReceiptAsync(long documentId, AccountsReceivableReceiptDraft receipt, CancellationToken cancellationToken)
    {
        ValidatePositiveAmount(receipt.Amount);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var userId = await EnsureDevelopmentUserAsync(connection, transaction, cancellationToken);

        const string documentSql = """
            SELECT company_id, customer_id, document_number, open_amount, status
            FROM core.ar_documents
            WHERE id = @document_id
            FOR UPDATE;
            """;

        await using var documentCommand = new NpgsqlCommand(documentSql, connection, transaction);
        documentCommand.Parameters.AddWithValue("document_id", documentId);

        await using var documentReader = await documentCommand.ExecuteReaderAsync(cancellationToken);
        if (!await documentReader.ReadAsync(cancellationToken))
        {
            throw new KeyNotFoundException($"AR document {documentId} was not found.");
        }

        var companyId = documentReader.GetInt64(0);
        var customerId = documentReader.GetInt64(1);
        var documentNumber = documentReader.GetString(2);
        var openAmount = documentReader.GetDecimal(3);
        var status = documentReader.GetString(4);
        await documentReader.DisposeAsync();

        if (!status.Equals("open", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only open AR documents can receive receipts.");
        }

        if (receipt.Amount > openAmount)
        {
            throw new ArgumentException($"Receipt amount {receipt.Amount:N2} exceeds open amount {openAmount:N2}.");
        }

        var accounts = await GetAccountsAsync(connection, transaction, companyId, cancellationToken);
        var postingEngine = new PostingEngine(accounts);
        var postingResult = postingEngine.Post(new JournalDraft(
            companyId.ToString(),
            receipt.ReceiptJournalNumber,
            receipt.ReceiptDate,
            "accounts_receivable",
            receipt.Currency,
            [
                new JournalLineDraft(receipt.CashAccountId, receipt.Amount, 0, $"Receipt for AR invoice {documentNumber}"),
                new JournalLineDraft(receipt.AccountsReceivableAccountId, 0, receipt.Amount, $"Receipt for AR invoice {documentNumber}")
            ],
            receipt.Memo,
            documentNumber));

        if (!postingResult.IsPosted)
        {
            throw new ArgumentException(string.Join(" ", postingResult.Validation.Errors));
        }

        var journalId = await InsertPostedJournalAsync(connection, transaction, postingResult.Journal!, companyId, userId, cancellationToken);
        var remainingOpenAmount = openAmount - receipt.Amount;
        var newStatus = remainingOpenAmount == 0 ? "paid" : "open";

        const string updateDocumentSql = """
            UPDATE core.ar_documents
            SET open_amount = @open_amount,
                status = @status
            WHERE id = @document_id;
            """;

        await using var updateDocumentCommand = new NpgsqlCommand(updateDocumentSql, connection, transaction);
        updateDocumentCommand.Parameters.AddWithValue("document_id", documentId);
        updateDocumentCommand.Parameters.AddWithValue("open_amount", remainingOpenAmount);
        updateDocumentCommand.Parameters.AddWithValue("status", newStatus);
        await updateDocumentCommand.ExecuteNonQueryAsync(cancellationToken);

        await InsertAuditEventAsync(
            connection,
            transaction,
            companyId,
            userId,
            "post",
            "ar_receipt",
            journalId.ToString(),
            "AR receipt recorded.",
            new
            {
                ArDocumentId = documentId,
                CustomerId = customerId,
                DocumentNumber = documentNumber,
                receipt.Amount,
                RemainingOpenAmount = remainingOpenAmount,
                JournalId = journalId
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetAccountsReceivableDocumentAsync(documentId, cancellationToken)
            ?? throw new InvalidOperationException($"AR document {documentId} could not be loaded after receipt.");
    }

    public async Task<IReadOnlyList<BankReconciliationSummaryRow>> GetBankReconciliationsAsync(long companyId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              br.id,
              br.bank_account_id,
              a.code,
              a.name,
              br.statement_ending_on,
              br.statement_balance,
              br.status,
              br.closed_at,
              COALESCE(SUM(CASE WHEN brl.status <> 'void' THEN brl.cleared_amount ELSE 0 END), 0) AS cleared_total,
              COUNT(brl.id) FILTER (WHERE brl.status <> 'void') AS line_count
            FROM core.bank_reconciliations br
            JOIN core.accounts a ON a.id = br.bank_account_id
            LEFT JOIN core.bank_reconciliation_lines brl ON brl.reconciliation_id = br.id
            WHERE br.company_id = @company_id
            GROUP BY br.id, br.bank_account_id, a.code, a.name
            ORDER BY br.statement_ending_on DESC, br.id DESC;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("company_id", companyId);

        var reconciliations = new List<BankReconciliationSummaryRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            reconciliations.Add(ReadBankReconciliationSummary(reader));
        }

        return reconciliations;
    }

    public async Task<BankReconciliationDetailRow?> GetBankReconciliationAsync(long reconciliationId, CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT
              br.id,
              br.bank_account_id,
              a.code,
              a.name,
              br.statement_ending_on,
              br.statement_balance,
              br.status,
              br.closed_at,
              COALESCE(SUM(CASE WHEN brl.status <> 'void' THEN brl.cleared_amount ELSE 0 END), 0) AS cleared_total,
              COUNT(brl.id) FILTER (WHERE brl.status <> 'void') AS line_count
            FROM core.bank_reconciliations br
            JOIN core.accounts a ON a.id = br.bank_account_id
            LEFT JOIN core.bank_reconciliation_lines brl ON brl.reconciliation_id = br.id
            WHERE br.id = @reconciliation_id
            GROUP BY br.id, br.bank_account_id, a.code, a.name;
            """;

        await using var headerCommand = _dataSource.CreateCommand(headerSql);
        headerCommand.Parameters.AddWithValue("reconciliation_id", reconciliationId);

        await using var headerReader = await headerCommand.ExecuteReaderAsync(cancellationToken);
        if (!await headerReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var summary = ReadBankReconciliationSummary(headerReader);
        await headerReader.DisposeAsync();

        var lines = await GetBankReconciliationLinesAsync(reconciliationId, cancellationToken);
        return new BankReconciliationDetailRow(summary, lines);
    }

    public async Task<long?> GetBankReconciliationCompanyIdAsync(long reconciliationId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT company_id
            FROM core.bank_reconciliations
            WHERE id = @reconciliation_id;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("reconciliation_id", reconciliationId);

        return await command.ExecuteScalarAsync(cancellationToken) is long companyId
            ? companyId
            : null;
    }

    public async Task<BankReconciliationDetailRow> CreateBankReconciliationAsync(BankReconciliationDraft draft, CancellationToken cancellationToken)
    {
        if (!long.TryParse(draft.CompanyId, out var companyId))
        {
            throw new ArgumentException("CompanyId must be a numeric database ID.");
        }

        if (!long.TryParse(draft.BankAccountId, out var bankAccountId))
        {
            throw new ArgumentException("BankAccountId must be a numeric database ID.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var userId = await EnsureDevelopmentUserAsync(connection, transaction, cancellationToken);
        await EnsureBankAccountAsync(connection, transaction, companyId, bankAccountId, cancellationToken);

        const string insertSql = """
            INSERT INTO core.bank_reconciliations (
              company_id,
              bank_account_id,
              statement_ending_on,
              statement_balance,
              status
            )
            VALUES (
              @company_id,
              @bank_account_id,
              @statement_ending_on,
              @statement_balance,
              'draft'
            )
            RETURNING id;
            """;

        await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("company_id", companyId);
        insertCommand.Parameters.AddWithValue("bank_account_id", bankAccountId);
        insertCommand.Parameters.AddWithValue("statement_ending_on", draft.StatementEndingOn);
        insertCommand.Parameters.AddWithValue("statement_balance", draft.StatementBalance);

        var reconciliationId = (long)(await insertCommand.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Bank reconciliation insert did not return an ID."));

        await InsertAuditEventAsync(
            connection,
            transaction,
            companyId,
            userId,
            "create",
            "bank_reconciliation",
            reconciliationId.ToString(),
            "Bank reconciliation draft created.",
            new
            {
                draft.BankAccountId,
                draft.StatementEndingOn,
                draft.StatementBalance
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetBankReconciliationAsync(reconciliationId, cancellationToken)
            ?? throw new InvalidOperationException($"Bank reconciliation {reconciliationId} could not be loaded after save.");
    }

    public async Task<BankReconciliationDetailRow> AddBankReconciliationLineAsync(long reconciliationId, BankReconciliationLineDraft line, CancellationToken cancellationToken)
    {
        ValidatePositiveAmount(line.ClearedAmount);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var userId = await EnsureDevelopmentUserAsync(connection, transaction, cancellationToken);

        const string reconciliationSql = """
            SELECT company_id, bank_account_id, status
            FROM core.bank_reconciliations
            WHERE id = @reconciliation_id
            FOR UPDATE;
            """;

        await using var reconciliationCommand = new NpgsqlCommand(reconciliationSql, connection, transaction);
        reconciliationCommand.Parameters.AddWithValue("reconciliation_id", reconciliationId);

        await using var reconciliationReader = await reconciliationCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reconciliationReader.ReadAsync(cancellationToken))
        {
            throw new KeyNotFoundException($"Bank reconciliation {reconciliationId} was not found.");
        }

        var companyId = reconciliationReader.GetInt64(0);
        var bankAccountId = reconciliationReader.GetInt64(1);
        var reconciliationStatus = reconciliationReader.GetString(2);
        await reconciliationReader.DisposeAsync();

        if (!reconciliationStatus.Equals("draft", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only draft bank reconciliations can be edited.");
        }

        long? journalLineId = null;
        if (!string.IsNullOrWhiteSpace(line.JournalLineId))
        {
            if (!long.TryParse(line.JournalLineId, out var parsedJournalLineId))
            {
                throw new ArgumentException("JournalLineId must be a numeric database ID.");
            }

            await EnsureBankJournalLineAsync(connection, transaction, companyId, bankAccountId, parsedJournalLineId, cancellationToken);
            journalLineId = parsedJournalLineId;
        }

        const string insertLineSql = """
            INSERT INTO core.bank_reconciliation_lines (
              reconciliation_id,
              journal_line_id,
              statement_reference,
              statement_date,
              cleared_amount,
              status
            )
            VALUES (
              @reconciliation_id,
              @journal_line_id,
              @statement_reference,
              @statement_date,
              @cleared_amount,
              'matched'
            )
            RETURNING id;
            """;

        await using var insertLineCommand = new NpgsqlCommand(insertLineSql, connection, transaction);
        insertLineCommand.Parameters.AddWithValue("reconciliation_id", reconciliationId);
        insertLineCommand.Parameters.AddWithValue("journal_line_id", (object?)journalLineId ?? DBNull.Value);
        insertLineCommand.Parameters.AddWithValue("statement_reference", (object?)line.StatementReference ?? DBNull.Value);
        insertLineCommand.Parameters.AddWithValue("statement_date", (object?)line.StatementDate ?? DBNull.Value);
        insertLineCommand.Parameters.AddWithValue("cleared_amount", line.ClearedAmount);

        var reconciliationLineId = (long)(await insertLineCommand.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Bank reconciliation line insert did not return an ID."));

        await InsertAuditEventAsync(
            connection,
            transaction,
            companyId,
            userId,
            "update",
            "bank_reconciliation",
            reconciliationId.ToString(),
            "Bank reconciliation line matched.",
            new
            {
                ReconciliationLineId = reconciliationLineId,
                JournalLineId = journalLineId,
                line.StatementReference,
                line.StatementDate,
                line.ClearedAmount
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetBankReconciliationAsync(reconciliationId, cancellationToken)
            ?? throw new InvalidOperationException($"Bank reconciliation {reconciliationId} could not be loaded after line save.");
    }

    public async Task<IReadOnlyList<BankReconciliationCandidateLineRow>> GetBankReconciliationCandidateLinesAsync(
        long companyId,
        long bankAccountId,
        DateOnly throughDate,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              jl.id,
              j.id,
              j.journal_number,
              j.journal_date,
              j.source_module,
              jl.description,
              jl.debit,
              jl.credit,
              CASE WHEN a.nature = 'asset' THEN jl.debit - jl.credit ELSE jl.credit - jl.debit END AS signed_amount
            FROM core.journal_lines jl
            JOIN core.journals j ON j.id = jl.journal_id
            JOIN core.accounts a ON a.id = jl.account_id
            WHERE j.company_id = @company_id
              AND j.status = 'posted'
              AND jl.account_id = @bank_account_id
              AND j.journal_date <= @through_date
              AND NOT EXISTS (
                SELECT 1
                FROM core.bank_reconciliation_lines brl
                JOIN core.bank_reconciliations br ON br.id = brl.reconciliation_id
                WHERE brl.journal_line_id = jl.id
                  AND br.status <> 'void'
                  AND brl.status <> 'void'
              )
            ORDER BY j.journal_date, j.id, jl.line_number;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("company_id", companyId);
        command.Parameters.AddWithValue("bank_account_id", bankAccountId);
        command.Parameters.AddWithValue("through_date", throughDate);

        var lines = new List<BankReconciliationCandidateLineRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(new BankReconciliationCandidateLineRow(
                reader.GetInt64(0).ToString(),
                reader.GetInt64(1).ToString(),
                reader.GetString(2),
                DateOnly.FromDateTime(reader.GetDateTime(3)),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetDecimal(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8)));
        }

        return lines;
    }

    public async Task<IReadOnlyList<TrialBalanceRow>> GetTrialBalanceAsync(long companyId, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              a.code,
              a.name,
              a.nature::text,
              COALESCE(SUM(CASE WHEN j.id IS NOT NULL THEN jl.debit ELSE 0 END), 0) AS debit,
              COALESCE(SUM(CASE WHEN j.id IS NOT NULL THEN jl.credit ELSE 0 END), 0) AS credit
            FROM core.accounts a
            LEFT JOIN core.journal_lines jl ON jl.account_id = a.id
            LEFT JOIN core.journals j ON j.id = jl.journal_id
              AND j.status = 'posted'
              AND j.journal_date <= @as_of_date
            WHERE a.company_id = @company_id
            GROUP BY a.code, a.name, a.nature
            ORDER BY a.code;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("company_id", companyId);
        command.Parameters.AddWithValue("as_of_date", asOfDate);

        var rows = new List<TrialBalanceRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var nature = ParseAccountNature(reader.GetString(2));
            var debit = reader.GetDecimal(3);
            var credit = reader.GetDecimal(4);
            var balance = NormalBalance(nature, debit, credit);

            rows.Add(new TrialBalanceRow(
                reader.GetString(0),
                reader.GetString(1),
                nature,
                debit,
                credit,
                balance));
        }

        return rows;
    }

    public async Task<IReadOnlyList<AgingReportRow>> GetAgedPayablesAsync(long companyId, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              ap.id,
              ap.vendor_id,
              bp.name,
              ap.document_number,
              ap.document_date,
              ap.due_date,
              ap.original_amount,
              ap.open_amount,
              ap.status,
              GREATEST(0, @as_of_date - COALESCE(ap.due_date, ap.document_date)) AS days_overdue,
              CASE WHEN COALESCE(ap.due_date, ap.document_date) >= @as_of_date THEN ap.open_amount ELSE 0 END AS current_amount,
              CASE WHEN @as_of_date - COALESCE(ap.due_date, ap.document_date) BETWEEN 1 AND 30 THEN ap.open_amount ELSE 0 END AS days_1_to_30,
              CASE WHEN @as_of_date - COALESCE(ap.due_date, ap.document_date) BETWEEN 31 AND 60 THEN ap.open_amount ELSE 0 END AS days_31_to_60,
              CASE WHEN @as_of_date - COALESCE(ap.due_date, ap.document_date) BETWEEN 61 AND 90 THEN ap.open_amount ELSE 0 END AS days_61_to_90,
              CASE WHEN @as_of_date - COALESCE(ap.due_date, ap.document_date) > 90 THEN ap.open_amount ELSE 0 END AS over_90
            FROM core.ap_documents ap
            JOIN core.business_partners bp ON bp.id = ap.vendor_id
            WHERE ap.company_id = @company_id
              AND ap.document_date <= @as_of_date
              AND ap.status = 'open'
              AND ap.open_amount > 0
            ORDER BY bp.name, COALESCE(ap.due_date, ap.document_date), ap.document_number;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("company_id", companyId);
        command.Parameters.AddWithValue("as_of_date", asOfDate);

        var rows = new List<AgingReportRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(ReadAgingReportRow(reader, "ap"));
        }

        return rows;
    }

    public async Task<IReadOnlyList<AgingReportRow>> GetAgedReceivablesAsync(long companyId, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              ar.id,
              ar.customer_id,
              bp.name,
              ar.document_number,
              ar.document_date,
              ar.due_date,
              ar.original_amount,
              ar.open_amount,
              ar.status,
              GREATEST(0, @as_of_date - COALESCE(ar.due_date, ar.document_date)) AS days_overdue,
              CASE WHEN COALESCE(ar.due_date, ar.document_date) >= @as_of_date THEN ar.open_amount ELSE 0 END AS current_amount,
              CASE WHEN @as_of_date - COALESCE(ar.due_date, ar.document_date) BETWEEN 1 AND 30 THEN ar.open_amount ELSE 0 END AS days_1_to_30,
              CASE WHEN @as_of_date - COALESCE(ar.due_date, ar.document_date) BETWEEN 31 AND 60 THEN ar.open_amount ELSE 0 END AS days_31_to_60,
              CASE WHEN @as_of_date - COALESCE(ar.due_date, ar.document_date) BETWEEN 61 AND 90 THEN ar.open_amount ELSE 0 END AS days_61_to_90,
              CASE WHEN @as_of_date - COALESCE(ar.due_date, ar.document_date) > 90 THEN ar.open_amount ELSE 0 END AS over_90
            FROM core.ar_documents ar
            JOIN core.business_partners bp ON bp.id = ar.customer_id
            WHERE ar.company_id = @company_id
              AND ar.document_date <= @as_of_date
              AND ar.status = 'open'
              AND ar.open_amount > 0
            ORDER BY bp.name, COALESCE(ar.due_date, ar.document_date), ar.document_number;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("company_id", companyId);
        command.Parameters.AddWithValue("as_of_date", asOfDate);

        var rows = new List<AgingReportRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(ReadAgingReportRow(reader, "ar"));
        }

        return rows;
    }

    public async Task<IReadOnlyList<GeneralLedgerAccountSection>> GetGeneralLedgerDetailAsync(
        long companyId,
        DateOnly fromDate,
        DateOnly toDate,
        long? accountId,
        CancellationToken cancellationToken)
    {
        if (toDate < fromDate)
        {
            throw new ArgumentException("ToDate must be on or after FromDate.");
        }

        const string openingSql = """
            SELECT
              a.id,
              a.code,
              a.name,
              a.nature::text,
              COALESCE(SUM(CASE WHEN j.id IS NOT NULL THEN jl.debit ELSE 0 END), 0) AS opening_debit,
              COALESCE(SUM(CASE WHEN j.id IS NOT NULL THEN jl.credit ELSE 0 END), 0) AS opening_credit
            FROM core.accounts a
            LEFT JOIN core.journal_lines jl ON jl.account_id = a.id
            LEFT JOIN core.journals j ON j.id = jl.journal_id
              AND j.company_id = a.company_id
              AND j.status = 'posted'
              AND j.journal_date < @from_date
            WHERE a.company_id = @company_id
              AND (CAST(@account_id AS bigint) IS NULL OR a.id = CAST(@account_id AS bigint))
            GROUP BY a.id, a.code, a.name, a.nature
            ORDER BY a.code;
            """;

        await using var openingCommand = _dataSource.CreateCommand(openingSql);
        openingCommand.Parameters.AddWithValue("company_id", companyId);
        openingCommand.Parameters.AddWithValue("from_date", fromDate);
        openingCommand.Parameters.AddWithValue("account_id", (object?)accountId ?? DBNull.Value);

        var sections = new Dictionary<long, GeneralLedgerSectionBuilder>();
        await using (var reader = await openingCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var parsedAccountId = reader.GetInt64(0);
                var nature = ParseAccountNature(reader.GetString(3));
                var openingDebit = reader.GetDecimal(4);
                var openingCredit = reader.GetDecimal(5);
                var openingBalance = NormalBalance(nature, openingDebit, openingCredit);

                sections.Add(parsedAccountId, new GeneralLedgerSectionBuilder(
                    parsedAccountId.ToString(),
                    reader.GetString(1),
                    reader.GetString(2),
                    nature,
                    openingBalance));
            }
        }

        const string linesSql = """
            SELECT
              a.id,
              j.id,
              j.journal_number,
              j.journal_date,
              j.source_module,
              j.memo,
              jl.line_number,
              jl.description,
              jl.debit,
              jl.credit
            FROM core.journal_lines jl
            JOIN core.journals j ON j.id = jl.journal_id
            JOIN core.accounts a ON a.id = jl.account_id
            WHERE j.company_id = @company_id
              AND j.status = 'posted'
              AND j.journal_date BETWEEN @from_date AND @to_date
              AND (CAST(@account_id AS bigint) IS NULL OR a.id = CAST(@account_id AS bigint))
            ORDER BY a.code, j.journal_date, j.id, jl.line_number;
            """;

        await using var linesCommand = _dataSource.CreateCommand(linesSql);
        linesCommand.Parameters.AddWithValue("company_id", companyId);
        linesCommand.Parameters.AddWithValue("from_date", fromDate);
        linesCommand.Parameters.AddWithValue("to_date", toDate);
        linesCommand.Parameters.AddWithValue("account_id", (object?)accountId ?? DBNull.Value);

        await using (var reader = await linesCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var parsedAccountId = reader.GetInt64(0);
                if (!sections.TryGetValue(parsedAccountId, out var section))
                {
                    continue;
                }

                var debit = reader.GetDecimal(8);
                var credit = reader.GetDecimal(9);
                var signedAmount = NormalBalance(section.Nature, debit, credit);
                section.RunningBalance += signedAmount;
                section.Lines.Add(new GeneralLedgerLineRow(
                    reader.GetInt64(1).ToString(),
                    reader.GetString(2),
                    DateOnly.FromDateTime(reader.GetDateTime(3)),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetInt32(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    debit,
                    credit,
                    signedAmount,
                    section.RunningBalance));
            }
        }

        return sections.Values
            .Where(section => section.OpeningBalance != 0 || section.Lines.Count > 0)
            .Select(section => section.ToReportSection())
            .ToArray();
    }

    public async Task AddReportViewAuditEventAsync(long companyId, string reportName, object parameters, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var userId = await EnsureDevelopmentUserAsync(connection, transaction, cancellationToken);

        await InsertAuditEventAsync(
            connection,
            transaction,
            companyId,
            userId,
            "view",
            "report",
            reportName,
            "Report viewed.",
            new
            {
                ReportName = reportName,
                Parameters = parameters
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddAuthorizationFailureAuditEventAsync(
        long companyId,
        string externalIdentityId,
        string requiredPermission,
        string entityType,
        string? entityId,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var userId = await TryGetUserIdAsync(connection, transaction, externalIdentityId, cancellationToken);

        await InsertAuditEventAsync(
            connection,
            transaction,
            companyId,
            userId,
            "authorization_failure",
            entityType,
            entityId,
            reason,
            new
            {
                ExternalIdentityId = externalIdentityId,
                RequiredPermission = requiredPermission
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEventRow>> GetAuditEventsAsync(long companyId, int limit, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              id,
              event_type::text,
              entity_type,
              entity_id,
              event_timestamp,
              reason,
              metadata::text
            FROM core.audit_events
            WHERE company_id = @company_id
            ORDER BY event_timestamp DESC, id DESC
            LIMIT @limit;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("company_id", companyId);
        command.Parameters.AddWithValue("limit", Math.Clamp(limit, 1, 500));

        var events = new List<AuditEventRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new AuditEventRow(
                reader.GetInt64(0).ToString(),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetDateTime(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6)));
        }

        return events;
    }

    private async Task<IReadOnlyList<PostedJournalLine>> GetJournalLinesAsync(long journalId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT line_number, account_id, debit, credit, description
            FROM core.journal_lines
            WHERE journal_id = @journal_id
            ORDER BY line_number;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("journal_id", journalId);

        var lines = new List<PostedJournalLine>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(new PostedJournalLine(
                reader.GetInt32(0),
                reader.GetInt64(1).ToString(),
                reader.GetDecimal(2),
                reader.GetDecimal(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return lines;
    }

    private async Task<AccountsPayableDocumentRow?> GetAccountsPayableDocumentAsync(long documentId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              ap.id,
              ap.vendor_id,
              bp.name,
              ap.journal_id,
              ap.document_number,
              ap.document_date,
              ap.due_date,
              ap.original_amount,
              ap.open_amount,
              ap.status,
              ap.legacy_document_id
            FROM core.ap_documents ap
            JOIN core.business_partners bp ON bp.id = ap.vendor_id
            WHERE ap.id = @document_id;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("document_id", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadAccountsPayableDocument(reader)
            : null;
    }

    private static AccountsPayableDocumentRow ReadAccountsPayableDocument(NpgsqlDataReader reader)
    {
        return new AccountsPayableDocumentRow(
            reader.GetInt64(0).ToString(),
            reader.GetInt64(1).ToString(),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetInt64(3).ToString(),
            reader.GetString(4),
            DateOnly.FromDateTime(reader.GetDateTime(5)),
            reader.IsDBNull(6) ? null : DateOnly.FromDateTime(reader.GetDateTime(6)),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10));
    }

    private async Task<AccountsReceivableDocumentRow?> GetAccountsReceivableDocumentAsync(long documentId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              ar.id,
              ar.customer_id,
              bp.name,
              ar.journal_id,
              ar.document_number,
              ar.document_date,
              ar.due_date,
              ar.original_amount,
              ar.open_amount,
              ar.status,
              ar.legacy_document_id
            FROM core.ar_documents ar
            JOIN core.business_partners bp ON bp.id = ar.customer_id
            WHERE ar.id = @document_id;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("document_id", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadAccountsReceivableDocument(reader)
            : null;
    }

    private async Task<IReadOnlyList<BankReconciliationLineRow>> GetBankReconciliationLinesAsync(long reconciliationId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              brl.id,
              brl.journal_line_id,
              j.id,
              j.journal_number,
              j.journal_date,
              j.source_module,
              brl.statement_reference,
              brl.statement_date,
              brl.cleared_amount,
              brl.status
            FROM core.bank_reconciliation_lines brl
            LEFT JOIN core.journal_lines jl ON jl.id = brl.journal_line_id
            LEFT JOIN core.journals j ON j.id = jl.journal_id
            WHERE brl.reconciliation_id = @reconciliation_id
            ORDER BY COALESCE(brl.statement_date, j.journal_date), brl.id;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("reconciliation_id", reconciliationId);

        var lines = new List<BankReconciliationLineRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(new BankReconciliationLineRow(
                reader.GetInt64(0).ToString(),
                reader.IsDBNull(1) ? null : reader.GetInt64(1).ToString(),
                reader.IsDBNull(2) ? null : reader.GetInt64(2).ToString(),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : DateOnly.FromDateTime(reader.GetDateTime(4)),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : DateOnly.FromDateTime(reader.GetDateTime(7)),
                reader.GetDecimal(8),
                reader.GetString(9)));
        }

        return lines;
    }

    private static AccountsReceivableDocumentRow ReadAccountsReceivableDocument(NpgsqlDataReader reader)
    {
        return new AccountsReceivableDocumentRow(
            reader.GetInt64(0).ToString(),
            reader.GetInt64(1).ToString(),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetInt64(3).ToString(),
            reader.GetString(4),
            DateOnly.FromDateTime(reader.GetDateTime(5)),
            reader.IsDBNull(6) ? null : DateOnly.FromDateTime(reader.GetDateTime(6)),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10));
    }

    private static BankReconciliationSummaryRow ReadBankReconciliationSummary(NpgsqlDataReader reader)
    {
        var statementBalance = reader.GetDecimal(5);
        var clearedTotal = reader.GetDecimal(8);

        return new BankReconciliationSummaryRow(
            reader.GetInt64(0).ToString(),
            reader.GetInt64(1).ToString(),
            reader.GetString(2),
            reader.GetString(3),
            DateOnly.FromDateTime(reader.GetDateTime(4)),
            statementBalance,
            reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetDateTime(7),
            clearedTotal,
            statementBalance - clearedTotal,
            reader.GetInt64(9));
    }

    private static AgingReportRow ReadAgingReportRow(NpgsqlDataReader reader, string subledgerType)
    {
        return new AgingReportRow(
            subledgerType,
            reader.GetInt64(0).ToString(),
            reader.GetInt64(1).ToString(),
            reader.GetString(2),
            reader.GetString(3),
            DateOnly.FromDateTime(reader.GetDateTime(4)),
            reader.IsDBNull(5) ? null : DateOnly.FromDateTime(reader.GetDateTime(5)),
            reader.GetDecimal(6),
            reader.GetDecimal(7),
            reader.GetString(8),
            reader.GetInt32(9),
            reader.GetDecimal(10),
            reader.GetDecimal(11),
            reader.GetDecimal(12),
            reader.GetDecimal(13),
            reader.GetDecimal(14));
    }

    private sealed class GeneralLedgerSectionBuilder
    {
        public GeneralLedgerSectionBuilder(string accountId, string accountCode, string accountName, AccountNature nature, decimal openingBalance)
        {
            AccountId = accountId;
            AccountCode = accountCode;
            AccountName = accountName;
            Nature = nature;
            OpeningBalance = openingBalance;
            RunningBalance = openingBalance;
        }

        public string AccountId { get; }
        public string AccountCode { get; }
        public string AccountName { get; }
        public AccountNature Nature { get; }
        public decimal OpeningBalance { get; }
        public decimal RunningBalance { get; set; }
        public List<GeneralLedgerLineRow> Lines { get; } = [];

        public GeneralLedgerAccountSection ToReportSection() => new(
            AccountId,
            AccountCode,
            AccountName,
            Nature,
            OpeningBalance,
            RunningBalance,
            Lines);
    }

    private static async Task<IReadOnlyList<Account>> GetAccountsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, code, name, nature::text, status::text
            FROM core.accounts
            WHERE company_id = @company_id
            ORDER BY code;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("company_id", companyId);

        var accounts = new List<Account>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(new Account(
                Id: reader.GetInt64(0).ToString(),
                Code: reader.GetString(1),
                Name: reader.GetString(2),
                Nature: ParseAccountNature(reader.GetString(3)),
                IsActive: reader.GetString(4).Equals("active", StringComparison.OrdinalIgnoreCase)));
        }

        return accounts;
    }

    private static async Task EnsureBusinessPartnerTypeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long businessPartnerId,
        IReadOnlyCollection<string> allowedTypes,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT partner_type, is_active
            FROM core.business_partners
            WHERE id = @business_partner_id
              AND company_id = @company_id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("business_partner_id", businessPartnerId);
        command.Parameters.AddWithValue("company_id", companyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new ArgumentException($"Business partner {businessPartnerId} was not found for company {companyId}.");
        }

        var partnerType = reader.GetString(0);
        var isActive = reader.GetBoolean(1);

        if (!isActive)
        {
            throw new ArgumentException($"Business partner {businessPartnerId} is inactive.");
        }

        if (!allowedTypes.Contains(partnerType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Business partner {businessPartnerId} is not valid for this workflow.");
        }
    }

    private static async Task EnsureBankAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long bankAccountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT is_bank_account, status::text
            FROM core.accounts
            WHERE id = @bank_account_id
              AND company_id = @company_id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("bank_account_id", bankAccountId);
        command.Parameters.AddWithValue("company_id", companyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new ArgumentException($"Bank account {bankAccountId} was not found for company {companyId}.");
        }

        var isBankAccount = reader.GetBoolean(0);
        var status = reader.GetString(1);

        if (!isBankAccount)
        {
            throw new ArgumentException($"Account {bankAccountId} is not marked as a bank account.");
        }

        if (!status.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Bank account {bankAccountId} is inactive.");
        }
    }

    private static async Task EnsureBankJournalLineAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long bankAccountId,
        long journalLineId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM core.journal_lines jl
            JOIN core.journals j ON j.id = jl.journal_id
            WHERE jl.id = @journal_line_id
              AND jl.account_id = @bank_account_id
              AND j.company_id = @company_id
              AND j.status = 'posted';
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("journal_line_id", journalLineId);
        command.Parameters.AddWithValue("bank_account_id", bankAccountId);
        command.Parameters.AddWithValue("company_id", companyId);

        var count = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        if (count == 0)
        {
            throw new ArgumentException($"Journal line {journalLineId} is not a posted line for bank account {bankAccountId}.");
        }
    }

    private static async Task<long> InsertPostedJournalAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostedJournal journal,
        long companyId,
        long userId,
        CancellationToken cancellationToken)
    {
        long? reversedJournalId = null;
        if (!string.IsNullOrWhiteSpace(journal.ReversedJournalId) && long.TryParse(journal.ReversedJournalId, out var parsedReversedJournalId))
        {
            reversedJournalId = parsedReversedJournalId;
        }

        const string insertJournalSql = """
            INSERT INTO core.journals (
              company_id,
              journal_number,
              journal_date,
              status,
              source_module,
              source_reference,
              memo,
              currency,
              exchange_rate,
              reversed_journal_id,
              created_by_user_id
            )
            VALUES (
              @company_id,
              @journal_number,
              @journal_date,
              'draft',
              @source_module,
              @source_reference,
              @memo,
              @currency,
              1,
              @reversed_journal_id,
              @created_by_user_id
            )
            RETURNING id;
            """;

        await using var insertJournalCommand = new NpgsqlCommand(insertJournalSql, connection, transaction);
        insertJournalCommand.Parameters.AddWithValue("company_id", companyId);
        insertJournalCommand.Parameters.AddWithValue("journal_number", journal.JournalNumber);
        insertJournalCommand.Parameters.AddWithValue("journal_date", journal.JournalDate);
        insertJournalCommand.Parameters.AddWithValue("source_module", journal.SourceModule);
        insertJournalCommand.Parameters.AddWithValue("source_reference", (object?)journal.SourceReference ?? DBNull.Value);
        insertJournalCommand.Parameters.AddWithValue("memo", (object?)journal.Memo ?? DBNull.Value);
        insertJournalCommand.Parameters.AddWithValue("currency", journal.Currency);
        insertJournalCommand.Parameters.AddWithValue("reversed_journal_id", (object?)reversedJournalId ?? DBNull.Value);
        insertJournalCommand.Parameters.AddWithValue("created_by_user_id", userId);

        var persistedJournalId = (long)(await insertJournalCommand.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Journal insert did not return an ID."));

        const string insertLineSql = """
            INSERT INTO core.journal_lines (
              journal_id,
              line_number,
              account_id,
              debit,
              credit,
              description
            )
            VALUES (
              @journal_id,
              @line_number,
              @account_id,
              @debit,
              @credit,
              @description
            );
            """;

        foreach (var line in journal.Lines)
        {
            if (!long.TryParse(line.AccountId, out var accountId))
            {
                throw new InvalidOperationException($"AccountId '{line.AccountId}' must be a numeric database ID.");
            }

            await using var insertLineCommand = new NpgsqlCommand(insertLineSql, connection, transaction);
            insertLineCommand.Parameters.AddWithValue("journal_id", persistedJournalId);
            insertLineCommand.Parameters.AddWithValue("line_number", line.LineNumber);
            insertLineCommand.Parameters.AddWithValue("account_id", accountId);
            insertLineCommand.Parameters.AddWithValue("debit", line.Debit);
            insertLineCommand.Parameters.AddWithValue("credit", line.Credit);
            insertLineCommand.Parameters.AddWithValue("description", (object?)line.Description ?? DBNull.Value);
            await insertLineCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string postJournalSql = """
            UPDATE core.journals
            SET
              status = 'posted',
              posted_at = now(),
              posted_by_user_id = @posted_by_user_id
            WHERE id = @journal_id;
            """;

        await using var postJournalCommand = new NpgsqlCommand(postJournalSql, connection, transaction);
        postJournalCommand.Parameters.AddWithValue("journal_id", persistedJournalId);
        postJournalCommand.Parameters.AddWithValue("posted_by_user_id", userId);
        await postJournalCommand.ExecuteNonQueryAsync(cancellationToken);

        await InsertAuditEventAsync(
            connection,
            transaction,
            companyId,
            userId,
            reversedJournalId is null ? "post" : "reverse",
            "journal",
            persistedJournalId.ToString(),
            reversedJournalId is null ? "Journal posted." : "Journal reversal posted.",
            new
            {
                journal.JournalNumber,
                journal.JournalDate,
                journal.SourceModule,
                journal.Currency,
                journal.TotalDebit,
                journal.TotalCredit,
                ReversedJournalId = reversedJournalId
            },
            cancellationToken);

        return persistedJournalId;
    }

    private static void ValidatePositiveAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }
    }

    private static async Task<long> EnsureDevelopmentUserAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO core.users (external_identity_id, display_name, email)
            VALUES (@external_identity_id, 'API Development User', 'api-dev@example.local')
            ON CONFLICT (external_identity_id) DO UPDATE
            SET display_name = EXCLUDED.display_name
            RETURNING id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("external_identity_id", DevelopmentUserExternalId);

        return (long)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Development user insert did not return an ID."));
    }

    private static async Task<long?> TryGetUserIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string externalIdentityId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id
            FROM core.users
            WHERE external_identity_id = @external_identity_id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("external_identity_id", externalIdentityId);

        return await command.ExecuteScalarAsync(cancellationToken) is long userId
            ? userId
            : null;
    }

    private static async Task InsertAuditEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long? actorUserId,
        string eventType,
        string entityType,
        string? entityId,
        string reason,
        object metadata,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO core.audit_events (
              company_id,
              actor_user_id,
              event_type,
              entity_type,
              entity_id,
              reason,
              metadata
            )
            VALUES (
              @company_id,
              @actor_user_id,
              CAST(@event_type AS core.audit_event_type),
              @entity_type,
              @entity_id,
              @reason,
              CAST(@metadata AS jsonb)
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("company_id", companyId);
        command.Parameters.AddWithValue("actor_user_id", (object?)actorUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("event_type", eventType);
        command.Parameters.AddWithValue("entity_type", entityType);
        command.Parameters.AddWithValue("entity_id", (object?)entityId ?? DBNull.Value);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("metadata", JsonSerializer.Serialize(metadata));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static AccountNature ParseAccountNature(string value) => value switch
    {
        "asset" => AccountNature.Asset,
        "liability" => AccountNature.Liability,
        "equity" => AccountNature.Equity,
        "revenue" => AccountNature.Revenue,
        "expense" => AccountNature.Expense,
        _ => throw new InvalidOperationException($"Unsupported account nature '{value}'.")
    };

    private static JournalStatus ParseJournalStatus(string value) => value switch
    {
        "draft" => JournalStatus.Draft,
        "posted" => JournalStatus.Posted,
        "reversed" => JournalStatus.Reversed,
        _ => throw new InvalidOperationException($"Unsupported journal status '{value}'.")
    };

    private static decimal NormalBalance(AccountNature nature, decimal debit, decimal credit)
    {
        return nature is AccountNature.Asset or AccountNature.Expense
            ? debit - credit
            : credit - debit;
    }
}

public sealed record TrialBalanceRow(
    string AccountCode,
    string AccountName,
    AccountNature Nature,
    decimal Debit,
    decimal Credit,
    decimal NormalBalance);

public sealed record AgingReportRow(
    string SubledgerType,
    string DocumentId,
    string PartnerId,
    string PartnerName,
    string DocumentNumber,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    decimal OriginalAmount,
    decimal OpenAmount,
    string Status,
    int DaysOverdue,
    decimal CurrentAmount,
    decimal Days1To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90);

public sealed record GeneralLedgerAccountSection(
    string AccountId,
    string AccountCode,
    string AccountName,
    AccountNature Nature,
    decimal OpeningBalance,
    decimal ClosingBalance,
    IReadOnlyList<GeneralLedgerLineRow> Lines);

public sealed record GeneralLedgerLineRow(
    string JournalId,
    string JournalNumber,
    DateOnly JournalDate,
    string SourceModule,
    string? Memo,
    int LineNumber,
    string? Description,
    decimal Debit,
    decimal Credit,
    decimal SignedAmount,
    decimal RunningBalance);

public sealed record AuditEventRow(
    string Id,
    string EventType,
    string EntityType,
    string? EntityId,
    DateTime EventTimestamp,
    string? Reason,
    string Metadata);

public sealed record BusinessPartnerRow(
    string Id,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? TaxIdentifier,
    bool IsActive);

public sealed record BankAccountRow(
    string Id,
    string Code,
    string Name,
    string Status);

public sealed record UserAuthorizationContext(
    string UserId,
    string ExternalIdentityId,
    string DisplayName,
    string? Email,
    bool IsActive,
    IReadOnlyList<string> Permissions)
{
    public bool HasPermission(string permission) => Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}

public sealed record AccountsPayableBillDraft(
    string CompanyId,
    string VendorId,
    string JournalNumber,
    string DocumentNumber,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    decimal Amount,
    string DebitAccountId,
    string AccountsPayableAccountId,
    string Currency,
    string? Memo,
    string? LegacyDocumentId);

public sealed record AccountsPayablePaymentDraft(
    string PaymentJournalNumber,
    DateOnly PaymentDate,
    decimal Amount,
    string CashAccountId,
    string AccountsPayableAccountId,
    string Currency,
    string? Memo);

public sealed record AccountsPayableDocumentRow(
    string Id,
    string VendorId,
    string VendorName,
    string? JournalId,
    string DocumentNumber,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    decimal OriginalAmount,
    decimal OpenAmount,
    string Status,
    string? LegacyDocumentId);

public sealed record AccountsReceivableInvoiceDraft(
    string CompanyId,
    string CustomerId,
    string JournalNumber,
    string DocumentNumber,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    decimal Amount,
    string AccountsReceivableAccountId,
    string RevenueAccountId,
    string Currency,
    string? Memo,
    string? LegacyDocumentId);

public sealed record AccountsReceivableReceiptDraft(
    string ReceiptJournalNumber,
    DateOnly ReceiptDate,
    decimal Amount,
    string CashAccountId,
    string AccountsReceivableAccountId,
    string Currency,
    string? Memo);

public sealed record AccountsReceivableDocumentRow(
    string Id,
    string CustomerId,
    string CustomerName,
    string? JournalId,
    string DocumentNumber,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    decimal OriginalAmount,
    decimal OpenAmount,
    string Status,
    string? LegacyDocumentId);

public sealed record BankReconciliationDraft(
    string CompanyId,
    string BankAccountId,
    DateOnly StatementEndingOn,
    decimal StatementBalance);

public sealed record BankReconciliationLineDraft(
    string? JournalLineId,
    string? StatementReference,
    DateOnly? StatementDate,
    decimal ClearedAmount);

public sealed record BankReconciliationSummaryRow(
    string Id,
    string BankAccountId,
    string BankAccountCode,
    string BankAccountName,
    DateOnly StatementEndingOn,
    decimal StatementBalance,
    string Status,
    DateTime? ClosedAt,
    decimal ClearedTotal,
    decimal Difference,
    long LineCount);

public sealed record BankReconciliationDetailRow(
    BankReconciliationSummaryRow Summary,
    IReadOnlyList<BankReconciliationLineRow> Lines);

public sealed record BankReconciliationLineRow(
    string Id,
    string? JournalLineId,
    string? JournalId,
    string? JournalNumber,
    DateOnly? JournalDate,
    string? SourceModule,
    string? StatementReference,
    DateOnly? StatementDate,
    decimal ClearedAmount,
    string Status);

public sealed record BankReconciliationCandidateLineRow(
    string JournalLineId,
    string JournalId,
    string JournalNumber,
    DateOnly JournalDate,
    string SourceModule,
    string? Description,
    decimal Debit,
    decimal Credit,
    decimal SignedAmount);
