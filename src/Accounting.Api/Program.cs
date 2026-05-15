using Accounting.Api;
using Accounting.Core;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Accounting")
    ?? Environment.GetEnvironmentVariable("ACCOUNTING_DB")
    ?? throw new InvalidOperationException("Set ConnectionStrings:Accounting or ACCOUNTING_DB.");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddScoped<AccountingRepository>();

var app = builder.Build();
const string DevelopmentIdentityHeader = "X-Accounting-User";
const string DevelopmentIdentityFallback = "api-dev";

app.MapGet("/", () => Results.Ok(new
{
    name = "Accounting System API",
    status = "ok",
    scope = "core-finance-v1"
}));

app.MapGet("/api/companies/{companyId:long}/accounts", async (long companyId, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "company.view", "accounts", null, cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var accounts = await repository.GetAccountsAsync(companyId, cancellationToken);
    return Results.Ok(accounts);
});

app.MapGet("/api/companies/{companyId:long}/vendors", async (long companyId, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "company.view", "vendors", null, cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var vendors = await repository.GetVendorsAsync(companyId, cancellationToken);
    return Results.Ok(vendors);
});

app.MapGet("/api/companies/{companyId:long}/customers", async (long companyId, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "company.view", "customers", null, cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var customers = await repository.GetCustomersAsync(companyId, cancellationToken);
    return Results.Ok(customers);
});

app.MapGet("/api/companies/{companyId:long}/bank-accounts", async (long companyId, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "bank_reconciliation.manage", "bank_account", null, cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var accounts = await repository.GetBankAccountsAsync(companyId, cancellationToken);
    return Results.Ok(accounts);
});

app.MapGet("/api/companies/{companyId:long}/ap-documents", async (long companyId, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "ap.manage", "ap_document", null, cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var documents = await repository.GetAccountsPayableDocumentsAsync(companyId, cancellationToken);
    return Results.Ok(documents);
});

app.MapGet("/api/companies/{companyId:long}/ar-documents", async (long companyId, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "ar.manage", "ar_document", null, cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var documents = await repository.GetAccountsReceivableDocumentsAsync(companyId, cancellationToken);
    return Results.Ok(documents);
});

app.MapPost("/api/ap-documents/bills", async (AccountsPayableBillRequest request, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        if (!long.TryParse(request.CompanyId, out var companyId))
        {
            return Results.BadRequest(ValidationResult.Failure(["CompanyId must be a numeric database ID."]));
        }

        var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "ap.manage", "ap_document", request.DocumentNumber, cancellationToken);
        if (!authorization.IsAllowed)
        {
            return authorization.Result!;
        }

        var document = await repository.SaveAccountsPayableBillAsync(request.ToDraft(), cancellationToken);
        return Results.Created($"/api/ap-documents/{document.Id}", document);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(ValidationResult.Failure([exception.Message]));
    }
    catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
    {
        return Results.BadRequest(ValidationResult.Failure(["AP document or journal number already exists."]));
    }
});

app.MapPost("/api/ap-documents/{documentId:long}/payments", async (long documentId, AccountsPayablePaymentRequest request, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        var companyId = await repository.GetAccountsPayableDocumentCompanyIdAsync(documentId, cancellationToken);
        if (companyId is null)
        {
            return Results.NotFound();
        }

        var authorization = await RequirePermissionAsync(httpContext, repository, companyId.Value, "ap.manage", "ap_document", documentId.ToString(), cancellationToken);
        if (!authorization.IsAllowed)
        {
            return authorization.Result!;
        }

        var document = await repository.ApplyAccountsPayablePaymentAsync(documentId, request.ToDraft(), cancellationToken);
        return Results.Ok(document);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(ValidationResult.Failure([exception.Message]));
    }
    catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
    {
        return Results.BadRequest(ValidationResult.Failure(["Payment journal number already exists."]));
    }
});

app.MapPost("/api/ar-documents/invoices", async (AccountsReceivableInvoiceRequest request, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        if (!long.TryParse(request.CompanyId, out var companyId))
        {
            return Results.BadRequest(ValidationResult.Failure(["CompanyId must be a numeric database ID."]));
        }

        var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "ar.manage", "ar_document", request.DocumentNumber, cancellationToken);
        if (!authorization.IsAllowed)
        {
            return authorization.Result!;
        }

        var document = await repository.SaveAccountsReceivableInvoiceAsync(request.ToDraft(), cancellationToken);
        return Results.Created($"/api/ar-documents/{document.Id}", document);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(ValidationResult.Failure([exception.Message]));
    }
    catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
    {
        return Results.BadRequest(ValidationResult.Failure(["AR document or journal number already exists."]));
    }
});

app.MapPost("/api/ar-documents/{documentId:long}/receipts", async (long documentId, AccountsReceivableReceiptRequest request, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        var companyId = await repository.GetAccountsReceivableDocumentCompanyIdAsync(documentId, cancellationToken);
        if (companyId is null)
        {
            return Results.NotFound();
        }

        var authorization = await RequirePermissionAsync(httpContext, repository, companyId.Value, "ar.manage", "ar_document", documentId.ToString(), cancellationToken);
        if (!authorization.IsAllowed)
        {
            return authorization.Result!;
        }

        var document = await repository.ApplyAccountsReceivableReceiptAsync(documentId, request.ToDraft(), cancellationToken);
        return Results.Ok(document);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(ValidationResult.Failure([exception.Message]));
    }
    catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
    {
        return Results.BadRequest(ValidationResult.Failure(["Receipt journal number already exists."]));
    }
});

app.MapGet("/api/companies/{companyId:long}/bank-reconciliations", async (long companyId, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "bank_reconciliation.manage", "bank_reconciliation", null, cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var reconciliations = await repository.GetBankReconciliationsAsync(companyId, cancellationToken);
    return Results.Ok(reconciliations);
});

app.MapGet("/api/bank-reconciliations/{reconciliationId:long}", async (long reconciliationId, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var companyId = await repository.GetBankReconciliationCompanyIdAsync(reconciliationId, cancellationToken);
    if (companyId is null)
    {
        return Results.NotFound();
    }

    var authorization = await RequirePermissionAsync(httpContext, repository, companyId.Value, "bank_reconciliation.manage", "bank_reconciliation", reconciliationId.ToString(), cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var reconciliation = await repository.GetBankReconciliationAsync(reconciliationId, cancellationToken);
    return reconciliation is null ? Results.NotFound() : Results.Ok(reconciliation);
});

app.MapPost("/api/bank-reconciliations", async (BankReconciliationRequest request, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        if (!long.TryParse(request.CompanyId, out var companyId))
        {
            return Results.BadRequest(ValidationResult.Failure(["CompanyId must be a numeric database ID."]));
        }

        var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "bank_reconciliation.manage", "bank_reconciliation", request.BankAccountId, cancellationToken);
        if (!authorization.IsAllowed)
        {
            return authorization.Result!;
        }

        var reconciliation = await repository.CreateBankReconciliationAsync(request.ToDraft(), cancellationToken);
        return Results.Created($"/api/bank-reconciliations/{reconciliation.Summary.Id}", reconciliation);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(ValidationResult.Failure([exception.Message]));
    }
    catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
    {
        return Results.BadRequest(ValidationResult.Failure(["Bank reconciliation already exists for this bank account and statement date."]));
    }
});

app.MapGet("/api/companies/{companyId:long}/bank-accounts/{bankAccountId:long}/candidate-lines", async (long companyId, long bankAccountId, DateOnly throughDate, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "bank_reconciliation.manage", "bank_account", bankAccountId.ToString(), cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var lines = await repository.GetBankReconciliationCandidateLinesAsync(companyId, bankAccountId, throughDate, cancellationToken);
    return Results.Ok(lines);
});

app.MapPost("/api/bank-reconciliations/{reconciliationId:long}/lines", async (long reconciliationId, BankReconciliationLineRequest request, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        var companyId = await repository.GetBankReconciliationCompanyIdAsync(reconciliationId, cancellationToken);
        if (companyId is null)
        {
            return Results.NotFound();
        }

        var authorization = await RequirePermissionAsync(httpContext, repository, companyId.Value, "bank_reconciliation.manage", "bank_reconciliation", reconciliationId.ToString(), cancellationToken);
        if (!authorization.IsAllowed)
        {
            return authorization.Result!;
        }

        var reconciliation = await repository.AddBankReconciliationLineAsync(reconciliationId, request.ToDraft(), cancellationToken);
        return Results.Ok(reconciliation);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(ValidationResult.Failure([exception.Message]));
    }
});

app.MapPost("/api/journals/validate", async (JournalDraftRequest request, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    if (!long.TryParse(request.CompanyId, out var companyId))
    {
        return Results.BadRequest(ValidationResult.Failure(["CompanyId must be a numeric database ID."]));
    }

    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "journal.create", "journal", request.JournalNumber, cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var accounts = await repository.GetAccountsAsync(companyId, cancellationToken);
    var postingEngine = new PostingEngine(accounts);
    var validation = postingEngine.Validate(request.ToDomain());

    return validation.IsValid
        ? Results.Ok(validation)
        : Results.BadRequest(validation);
});

app.MapPost("/api/journals/post", async (JournalDraftRequest request, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    if (!long.TryParse(request.CompanyId, out var companyId))
    {
        return Results.BadRequest(ValidationResult.Failure(["CompanyId must be a numeric database ID."]));
    }

    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "journal.post", "journal", request.JournalNumber, cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var accounts = await repository.GetAccountsAsync(companyId, cancellationToken);
    var postingEngine = new PostingEngine(accounts);
    var result = postingEngine.Post(request.ToDomain());
    if (!result.IsPosted)
    {
        return Results.BadRequest(result.Validation);
    }

    var savedJournal = await repository.SavePostedJournalAsync(result.Journal!, cancellationToken);
    return Results.Created($"/api/journals/{savedJournal.Id}", savedJournal);
});

app.MapPost("/api/journals/{id}/reverse", async (string id, ReversalRequest request, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var postedJournal = await repository.GetPostedJournalAsync(id, cancellationToken);
    if (postedJournal is null)
    {
        return Results.NotFound();
    }

    var companyId = long.Parse(postedJournal.CompanyId);
    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "journal.reverse", "journal", id, cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var accounts = await repository.GetAccountsAsync(companyId, cancellationToken);
    var postingEngine = new PostingEngine(accounts);
    var reversal = postingEngine.Reverse(postedJournal, request.ReversalJournalNumber, request.ReversalDate);
    var savedReversal = await repository.SavePostedJournalAsync(reversal, cancellationToken);

    return Results.Created($"/api/journals/{savedReversal.Id}", savedReversal);
});

app.MapGet("/api/companies/{companyId:long}/journals", async (long companyId, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "journal.view", "journal", null, cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var journals = await repository.GetPostedJournalsAsync(companyId, cancellationToken);
    return Results.Ok(journals);
});

app.MapGet("/api/journals/{id}", async (string id, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var journal = await repository.GetPostedJournalAsync(id, cancellationToken);
    if (journal is null)
    {
        return Results.NotFound();
    }

    var authorization = await RequirePermissionAsync(httpContext, repository, long.Parse(journal.CompanyId), "journal.view", "journal", id, cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    return Results.Ok(journal);
});

app.MapGet("/api/companies/{companyId:long}/reports/trial-balance", async (long companyId, DateOnly asOfDate, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "reports.view", "report", "trial-balance", cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var rows = await repository.GetTrialBalanceAsync(companyId, asOfDate, cancellationToken);
    await repository.AddReportViewAuditEventAsync(companyId, "trial-balance", new { AsOfDate = asOfDate }, cancellationToken);
    return Results.Ok(rows);
});

app.MapGet("/api/companies/{companyId:long}/reports/aged-payables", async (long companyId, DateOnly asOfDate, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "reports.view", "report", "aged-payables", cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var rows = await repository.GetAgedPayablesAsync(companyId, asOfDate, cancellationToken);
    await repository.AddReportViewAuditEventAsync(companyId, "aged-payables", new { AsOfDate = asOfDate }, cancellationToken);
    return Results.Ok(rows);
});

app.MapGet("/api/companies/{companyId:long}/reports/aged-receivables", async (long companyId, DateOnly asOfDate, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "reports.view", "report", "aged-receivables", cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var rows = await repository.GetAgedReceivablesAsync(companyId, asOfDate, cancellationToken);
    await repository.AddReportViewAuditEventAsync(companyId, "aged-receivables", new { AsOfDate = asOfDate }, cancellationToken);
    return Results.Ok(rows);
});

app.MapGet("/api/companies/{companyId:long}/reports/general-ledger", async (long companyId, DateOnly fromDate, DateOnly toDate, long? accountId, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "reports.view", "report", "general-ledger", cancellationToken);
        if (!authorization.IsAllowed)
        {
            return authorization.Result!;
        }

        var sections = await repository.GetGeneralLedgerDetailAsync(companyId, fromDate, toDate, accountId, cancellationToken);
        await repository.AddReportViewAuditEventAsync(companyId, "general-ledger", new { FromDate = fromDate, ToDate = toDate, AccountId = accountId }, cancellationToken);
        return Results.Ok(sections);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(ValidationResult.Failure([exception.Message]));
    }
});

app.MapGet("/api/companies/{companyId:long}/audit-events", async (long companyId, int? limit, HttpContext httpContext, AccountingRepository repository, CancellationToken cancellationToken) =>
{
    var authorization = await RequirePermissionAsync(httpContext, repository, companyId, "audit.view", "audit_event", null, cancellationToken);
    if (!authorization.IsAllowed)
    {
        return authorization.Result!;
    }

    var events = await repository.GetAuditEventsAsync(companyId, limit ?? 100, cancellationToken);
    return Results.Ok(events);
});

app.Run();

static string GetExternalIdentity(HttpContext httpContext)
{
    var headerValue = httpContext.Request.Headers[DevelopmentIdentityHeader].FirstOrDefault();
    return string.IsNullOrWhiteSpace(headerValue)
        ? DevelopmentIdentityFallback
        : headerValue.Trim();
}

static async Task<AuthorizationCheck> RequirePermissionAsync(
    HttpContext httpContext,
    AccountingRepository repository,
    long companyId,
    string permission,
    string entityType,
    string? entityId,
    CancellationToken cancellationToken)
{
    var externalIdentityId = GetExternalIdentity(httpContext);
    var user = await repository.GetUserAuthorizationAsync(externalIdentityId, companyId, cancellationToken);

    if (user is null)
    {
        await repository.AddAuthorizationFailureAuditEventAsync(
            companyId,
            externalIdentityId,
            permission,
            entityType,
            entityId,
            "Unknown user identity.",
            cancellationToken);

        return AuthorizationCheck.Denied(Results.Unauthorized());
    }

    if (!user.IsActive)
    {
        await repository.AddAuthorizationFailureAuditEventAsync(
            companyId,
            externalIdentityId,
            permission,
            entityType,
            entityId,
            "Inactive user attempted access.",
            cancellationToken);

        return AuthorizationCheck.Denied(Results.Forbid());
    }

    if (!user.HasPermission(permission))
    {
        await repository.AddAuthorizationFailureAuditEventAsync(
            companyId,
            externalIdentityId,
            permission,
            entityType,
            entityId,
            "Required permission is missing.",
            cancellationToken);

        return AuthorizationCheck.Denied(Results.Forbid());
    }

    return AuthorizationCheck.Allowed(user);
}

public sealed record JournalDraftRequest(
    string CompanyId,
    string JournalNumber,
    DateOnly JournalDate,
    string SourceModule,
    string Currency,
    IReadOnlyList<JournalLineRequest> Lines,
    string? Memo,
    string? SourceReference)
{
    public JournalDraft ToDomain() => new(
        CompanyId,
        JournalNumber,
        JournalDate,
        SourceModule,
        Currency,
        Lines.Select(line => new JournalLineDraft(
            line.AccountId,
            line.Debit,
            line.Credit,
            line.Description)).ToArray(),
        Memo,
        SourceReference);
}

public sealed record JournalLineRequest(
    string AccountId,
    decimal Debit,
    decimal Credit,
    string? Description);

public sealed record ReversalRequest(
    string ReversalJournalNumber,
    DateOnly ReversalDate);

public sealed record AccountsPayableBillRequest(
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
    string? LegacyDocumentId)
{
    public AccountsPayableBillDraft ToDraft() => new(
        CompanyId,
        VendorId,
        JournalNumber,
        DocumentNumber,
        DocumentDate,
        DueDate,
        Amount,
        DebitAccountId,
        AccountsPayableAccountId,
        Currency,
        Memo,
        LegacyDocumentId);
}

public sealed record AccountsPayablePaymentRequest(
    string PaymentJournalNumber,
    DateOnly PaymentDate,
    decimal Amount,
    string CashAccountId,
    string AccountsPayableAccountId,
    string Currency,
    string? Memo)
{
    public AccountsPayablePaymentDraft ToDraft() => new(
        PaymentJournalNumber,
        PaymentDate,
        Amount,
        CashAccountId,
        AccountsPayableAccountId,
        Currency,
        Memo);
}

public sealed record AccountsReceivableInvoiceRequest(
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
    string? LegacyDocumentId)
{
    public AccountsReceivableInvoiceDraft ToDraft() => new(
        CompanyId,
        CustomerId,
        JournalNumber,
        DocumentNumber,
        DocumentDate,
        DueDate,
        Amount,
        AccountsReceivableAccountId,
        RevenueAccountId,
        Currency,
        Memo,
        LegacyDocumentId);
}

public sealed record AccountsReceivableReceiptRequest(
    string ReceiptJournalNumber,
    DateOnly ReceiptDate,
    decimal Amount,
    string CashAccountId,
    string AccountsReceivableAccountId,
    string Currency,
    string? Memo)
{
    public AccountsReceivableReceiptDraft ToDraft() => new(
        ReceiptJournalNumber,
        ReceiptDate,
        Amount,
        CashAccountId,
        AccountsReceivableAccountId,
        Currency,
        Memo);
}

public sealed record BankReconciliationRequest(
    string CompanyId,
    string BankAccountId,
    DateOnly StatementEndingOn,
    decimal StatementBalance)
{
    public BankReconciliationDraft ToDraft() => new(
        CompanyId,
        BankAccountId,
        StatementEndingOn,
        StatementBalance);
}

public sealed record BankReconciliationLineRequest(
    string? JournalLineId,
    string? StatementReference,
    DateOnly? StatementDate,
    decimal ClearedAmount)
{
    public BankReconciliationLineDraft ToDraft() => new(
        JournalLineId,
        StatementReference,
        StatementDate,
        ClearedAmount);
}

public sealed record AuthorizationCheck(bool IsAllowed, UserAuthorizationContext? User, IResult? Result)
{
    public static AuthorizationCheck Allowed(UserAuthorizationContext user) => new(true, user, null);

    public static AuthorizationCheck Denied(IResult result) => new(false, null, result);
}
