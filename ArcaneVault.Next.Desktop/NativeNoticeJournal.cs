namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Session-local, capped activity history. No OS notifications, telemetry,
/// persistent personal data, or collection writes.
/// </summary>
public sealed class NativeNoticeJournal
{
    public sealed record Entry(Guid Id, DateTimeOffset Timestamp, string Title,
        string Message, bool IsWarning)
    {
        public string TimestampLabel => Timestamp.ToLocalTime().ToString("h:mm tt");
        public string SeverityLabel => IsWarning ? "Attention" : "Update";
    }

    private readonly List<Entry> entries = new();
    public const int MaximumHistory = 60;
    public IReadOnlyList<Entry> Entries => entries;

    public Entry Post(string title, string message, bool warning = false)
    {
        var next = new Entry(Guid.NewGuid(), DateTimeOffset.UtcNow,
            title.Trim(), message.Trim(), warning);
        entries.Insert(0, next);
        if (entries.Count > MaximumHistory)
            entries.RemoveRange(MaximumHistory, entries.Count - MaximumHistory);
        return next;
    }

    public void Clear() => entries.Clear();
}
