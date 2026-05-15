using Accounting.Core;

var tests = new PostingEngineTests();

tests.ValidBalancedJournalPosts();
tests.UnbalancedJournalIsRejected();
tests.DualSidedLineIsRejected();
tests.InactiveAccountIsRejected();
tests.ReversalCreatesEqualAndOppositeJournal();

Console.WriteLine("Accounting.Core.Tests passed.");

internal sealed class PostingEngineTests
{
    private readonly Account[] _accounts =
    [
        new("cash", "1000", "Cash", AccountNature.Asset),
        new("revenue", "4000", "Revenue", AccountNature.Revenue),
        new("inactive", "9999", "Inactive", AccountNature.Expense, IsActive: false)
    ];

    public void ValidBalancedJournalPosts()
    {
        var engine = new PostingEngine(_accounts);
        var result = engine.Post(CreateDraft([
            new JournalLineDraft("cash", 100, 0),
            new JournalLineDraft("revenue", 0, 100)
        ]), () => "posted-1");

        Assert(result.IsPosted, "Expected balanced journal to post.");
        Assert(result.Journal!.TotalDebit == 100, "Expected debit total to be 100.");
        Assert(result.Journal.TotalCredit == 100, "Expected credit total to be 100.");
        Assert(result.Journal.Status == JournalStatus.Posted, "Expected posted status.");
    }

    public void UnbalancedJournalIsRejected()
    {
        var engine = new PostingEngine(_accounts);
        var result = engine.Post(CreateDraft([
            new JournalLineDraft("cash", 100, 0),
            new JournalLineDraft("revenue", 0, 99)
        ]));

        Assert(!result.IsPosted, "Expected unbalanced journal to be rejected.");
        Assert(result.Validation.Errors.Any(error => error.Contains("not balanced", StringComparison.OrdinalIgnoreCase)), "Expected balance error.");
    }

    public void DualSidedLineIsRejected()
    {
        var engine = new PostingEngine(_accounts);
        var result = engine.Post(CreateDraft([
            new JournalLineDraft("cash", 100, 1),
            new JournalLineDraft("revenue", 0, 99)
        ]));

        Assert(!result.IsPosted, "Expected dual-sided line to be rejected.");
        Assert(result.Validation.Errors.Any(error => error.Contains("exactly one", StringComparison.OrdinalIgnoreCase)), "Expected one-sided line error.");
    }

    public void InactiveAccountIsRejected()
    {
        var engine = new PostingEngine(_accounts);
        var result = engine.Post(CreateDraft([
            new JournalLineDraft("cash", 100, 0),
            new JournalLineDraft("inactive", 0, 100)
        ]));

        Assert(!result.IsPosted, "Expected inactive account to be rejected.");
        Assert(result.Validation.Errors.Any(error => error.Contains("inactive", StringComparison.OrdinalIgnoreCase)), "Expected inactive account error.");
    }

    public void ReversalCreatesEqualAndOppositeJournal()
    {
        var engine = new PostingEngine(_accounts);
        var result = engine.Post(CreateDraft([
            new JournalLineDraft("cash", 100, 0, "Cash receipt"),
            new JournalLineDraft("revenue", 0, 100, "Revenue")
        ]), () => "posted-2");

        var reversal = engine.Reverse(result.Journal!, "REV-001", new DateOnly(2026, 5, 15), () => "reversal-1");

        Assert(reversal.ReversedJournalId == "posted-2", "Expected reversal link.");
        Assert(reversal.TotalDebit == result.Journal!.TotalCredit, "Expected debit total to reverse original credit.");
        Assert(reversal.TotalCredit == result.Journal.TotalDebit, "Expected credit total to reverse original debit.");
        Assert(reversal.Lines[0].Credit == 100, "Expected first line to be credited in reversal.");
        Assert(reversal.Lines[1].Debit == 100, "Expected second line to be debited in reversal.");
    }

    private static JournalDraft CreateDraft(IReadOnlyList<JournalLineDraft> lines) => new(
        CompanyId: "company-1",
        JournalNumber: $"JV-{Guid.NewGuid():N}",
        JournalDate: new DateOnly(2026, 5, 15),
        SourceModule: "general_ledger",
        Currency: "PHP",
        Lines: lines);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
