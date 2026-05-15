namespace Accounting.Core;

public enum AccountNature
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Expense
}

public enum JournalStatus
{
    Draft,
    Posted,
    Reversed
}

public sealed record Account(
    string Id,
    string Code,
    string Name,
    AccountNature Nature,
    bool IsActive = true);

public sealed record JournalLineDraft(
    string AccountId,
    decimal Debit,
    decimal Credit,
    string? Description = null,
    string? LegacyLineId = null,
    decimal? LegacySignedAmount = null);

public sealed record JournalDraft(
    string CompanyId,
    string JournalNumber,
    DateOnly JournalDate,
    string SourceModule,
    string Currency,
    IReadOnlyList<JournalLineDraft> Lines,
    string? Memo = null,
    string? SourceReference = null,
    string? LegacyJournalId = null);

public sealed record PostedJournalLine(
    int LineNumber,
    string AccountId,
    decimal Debit,
    decimal Credit,
    string? Description);

public sealed record PostedJournal(
    string Id,
    string CompanyId,
    string JournalNumber,
    DateOnly JournalDate,
    JournalStatus Status,
    string SourceModule,
    string Currency,
    IReadOnlyList<PostedJournalLine> Lines,
    decimal TotalDebit,
    decimal TotalCredit,
    string? Memo = null,
    string? SourceReference = null,
    string? ReversedJournalId = null);

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Success { get; } = new(true, Array.Empty<string>());

    public static ValidationResult Failure(IReadOnlyList<string> errors) => new(false, errors);
}

public sealed record PostingResult(ValidationResult Validation, PostedJournal? Journal)
{
    public bool IsPosted => Validation.IsValid && Journal is not null;
}
