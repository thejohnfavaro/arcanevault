namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Lightweight advisory deck-composition checklist. Targets are common starting
/// points for 100-card singleton formats, not format rules or power rankings.
/// Each role is heuristic and a card may contribute to more than one role.
/// Does not modify the collection, catalog or deck.
/// </summary>
public static class EdhLabDeckBlueprint
{
    private static readonly (string Role, string Label, int Minimum)[] Targets =
    [
        ("Land", "Lands", 36),
        ("Ramp", "Ramp", 10),
        ("Card Draw", "Draw", 10),
        ("Removal", "Removal", 8),
        ("Board Wipe", "Wipes", 2),
        ("Protection", "Protection", 2)
    ];

    public sealed record RoleProgress(string Role, string Label,
        int Copies, int SuggestedMinimum)
    {
        public int Shortfall => Math.Max(0, SuggestedMinimum - Copies);
    }

    public sealed record Snapshot(int MainboardCopies, int UnverifiedCopies,
        IReadOnlyList<RoleProgress> Roles)
    {
        // Prefer the largest proportional gap. On ties, retain the familiar
        // land -> ramp -> draw -> interaction ordering from Targets.
        public RoleProgress? NextGap => Roles.Where(x => x.Shortfall > 0)
            .OrderByDescending(x => (double)x.Shortfall / x.SuggestedMinimum)
            .FirstOrDefault();

        public string Display =>
            $"Mainboard {MainboardCopies}/99 (commander separate)\n" +
            string.Join(" · ", Roles.Take(3)
                .Select(x => $"{x.Label} {x.Copies}/{x.SuggestedMinimum}")) +
            "\n" +
            string.Join(" · ", Roles.Skip(3)
                .Select(x => $"{x.Label} {x.Copies}/{x.SuggestedMinimum}")) +
            (UnverifiedCopies > 0
                ? $"\n{UnverifiedCopies} copies have no eligible local catalog match."
                : "") +
            "\nSuggested targets only; overlapping roles are counted in each.";
    }

    public static Snapshot Analyze(EdhLabDraftCatalog catalog,
        IEnumerable<(string Name, int Quantity)> mainboard)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(mainboard);
        var counts = new int[Targets.Length];
        int total = 0, unverified = 0;

        foreach (var (name, quantity) in mainboard)
        {
            if (quantity < 1)
                throw new ArgumentOutOfRangeException(nameof(mainboard),
                    "Draft card quantities must be positive.");
            total = checked(total + quantity);
            var card = catalog.Find(name);
            if (card is null)
            {
                unverified = checked(unverified + quantity);
                continue;
            }

            var tags = card.RoleHints.Split(" · ",
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < Targets.Length; i++)
                if (tags.Contains(Targets[i].Role, StringComparer.OrdinalIgnoreCase))
                    counts[i] = checked(counts[i] + quantity);
        }

        return new Snapshot(total, unverified, Targets.Select((x, i) =>
            new RoleProgress(x.Role, x.Label, counts[i], x.Minimum)).ToArray());
    }
}
