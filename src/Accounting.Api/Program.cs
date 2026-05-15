using Accounting.Core;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var accounts = new[]
{
    new Account("cash", "1000", "Cash", AccountNature.Asset),
    new Account("accounts-receivable", "1200", "Accounts Receivable", AccountNature.Asset),
    new Account("accounts-payable", "2100", "Accounts Payable", AccountNature.Liability),
    new Account("revenue", "4000", "Revenue", AccountNature.Revenue),
    new Account("expense", "5000", "Operating Expense", AccountNature.Expense)
};

var postingEngine = new PostingEngine(accounts);
var postedJournals = new List<PostedJournal>();

app.MapGet("/", () => Results.Ok(new
{
    name = "Accounting System API",
    status = "ok",
    scope = "core-finance-v1"
}));

app.MapGet("/api/accounts", () => Results.Ok(accounts));

app.MapPost("/api/journals/validate", (JournalDraftRequest request) =>
{
    var validation = postingEngine.Validate(request.ToDomain());

    return validation.IsValid
        ? Results.Ok(validation)
        : Results.BadRequest(validation);
});

app.MapPost("/api/journals/post", (JournalDraftRequest request) =>
{
    var result = postingEngine.Post(request.ToDomain());
    if (!result.IsPosted)
    {
        return Results.BadRequest(result.Validation);
    }

    postedJournals.Add(result.Journal!);
    return Results.Created($"/api/journals/{result.Journal!.Id}", result.Journal);
});

app.MapPost("/api/journals/{id}/reverse", (string id, ReversalRequest request) =>
{
    var postedJournal = postedJournals.SingleOrDefault(journal => journal.Id == id);
    if (postedJournal is null)
    {
        return Results.NotFound();
    }

    var reversal = postingEngine.Reverse(postedJournal, request.ReversalJournalNumber, request.ReversalDate);
    postedJournals.Add(reversal);

    return Results.Created($"/api/journals/{reversal.Id}", reversal);
});

app.MapGet("/api/journals", () => Results.Ok(postedJournals));

app.Run();

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
