namespace Accounting.Core;

public sealed class PostingEngine
{
    private readonly IReadOnlyDictionary<string, Account> _accounts;

    public PostingEngine(IEnumerable<Account> accounts)
    {
        _accounts = accounts.ToDictionary(account => account.Id, StringComparer.OrdinalIgnoreCase);
    }

    public ValidationResult Validate(JournalDraft draft)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(draft.CompanyId))
        {
            errors.Add("Company is required.");
        }

        if (string.IsNullOrWhiteSpace(draft.JournalNumber))
        {
            errors.Add("Journal number is required.");
        }

        if (draft.JournalDate == default)
        {
            errors.Add("Journal date is required.");
        }

        if (string.IsNullOrWhiteSpace(draft.SourceModule))
        {
            errors.Add("Source module is required.");
        }

        if (string.IsNullOrWhiteSpace(draft.Currency) || draft.Currency.Length != 3)
        {
            errors.Add("Currency must be a three-letter ISO code.");
        }

        if (draft.Lines.Count < 2)
        {
            errors.Add("A journal must have at least two lines.");
        }

        for (var i = 0; i < draft.Lines.Count; i++)
        {
            var line = draft.Lines[i];
            var lineNumber = i + 1;

            if (!_accounts.TryGetValue(line.AccountId, out var account))
            {
                errors.Add($"Line {lineNumber}: account '{line.AccountId}' does not exist.");
                continue;
            }

            if (!account.IsActive)
            {
                errors.Add($"Line {lineNumber}: account '{account.Code}' is inactive.");
            }

            if (line.Debit < 0 || line.Credit < 0)
            {
                errors.Add($"Line {lineNumber}: debit and credit must be non-negative.");
            }

            var hasDebit = line.Debit > 0;
            var hasCredit = line.Credit > 0;

            if (hasDebit == hasCredit)
            {
                errors.Add($"Line {lineNumber}: exactly one of debit or credit must be greater than zero.");
            }
        }

        var totalDebit = draft.Lines.Sum(line => line.Debit);
        var totalCredit = draft.Lines.Sum(line => line.Credit);

        if (totalDebit != totalCredit)
        {
            errors.Add($"Journal is not balanced. Debit total {totalDebit:N2}; credit total {totalCredit:N2}.");
        }

        return errors.Count == 0
            ? ValidationResult.Success
            : ValidationResult.Failure(errors);
    }

    public PostingResult Post(JournalDraft draft, Func<string>? idFactory = null)
    {
        var validation = Validate(draft);
        if (!validation.IsValid)
        {
            return new PostingResult(validation, null);
        }

        var journal = new PostedJournal(
            Id: idFactory?.Invoke() ?? Guid.NewGuid().ToString("N"),
            CompanyId: draft.CompanyId,
            JournalNumber: draft.JournalNumber,
            JournalDate: draft.JournalDate,
            Status: JournalStatus.Posted,
            SourceModule: draft.SourceModule,
            Currency: draft.Currency.ToUpperInvariant(),
            Lines: draft.Lines.Select((line, index) => new PostedJournalLine(
                LineNumber: index + 1,
                AccountId: line.AccountId,
                Debit: line.Debit,
                Credit: line.Credit,
                Description: line.Description)).ToArray(),
            TotalDebit: draft.Lines.Sum(line => line.Debit),
            TotalCredit: draft.Lines.Sum(line => line.Credit),
            Memo: draft.Memo,
            SourceReference: draft.SourceReference);

        return new PostingResult(validation, journal);
    }

    public PostedJournal Reverse(PostedJournal postedJournal, string reversalJournalNumber, DateOnly reversalDate, Func<string>? idFactory = null)
    {
        if (postedJournal.Status != JournalStatus.Posted)
        {
            throw new InvalidOperationException("Only posted journals can be reversed.");
        }

        var reversedLines = postedJournal.Lines
            .Select(line => new PostedJournalLine(
                LineNumber: line.LineNumber,
                AccountId: line.AccountId,
                Debit: line.Credit,
                Credit: line.Debit,
                Description: $"Reversal of {postedJournal.JournalNumber}: {line.Description}".Trim()))
            .ToArray();

        return new PostedJournal(
            Id: idFactory?.Invoke() ?? Guid.NewGuid().ToString("N"),
            CompanyId: postedJournal.CompanyId,
            JournalNumber: reversalJournalNumber,
            JournalDate: reversalDate,
            Status: JournalStatus.Posted,
            SourceModule: postedJournal.SourceModule,
            Currency: postedJournal.Currency,
            Lines: reversedLines,
            TotalDebit: reversedLines.Sum(line => line.Debit),
            TotalCredit: reversedLines.Sum(line => line.Credit),
            Memo: $"Reversal of {postedJournal.JournalNumber}",
            SourceReference: postedJournal.JournalNumber,
            ReversedJournalId: postedJournal.Id);
    }
}
