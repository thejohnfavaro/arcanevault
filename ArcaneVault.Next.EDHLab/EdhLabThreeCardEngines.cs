namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Read-only three-card pathways from explicitly linked cached Oracle-text
/// effects. This is a short list of possible sequences, NOT a rules engine,
/// deterministic loop detector, or proof that a win condition is assembled.
/// Every proposed pathway has at least two distinct cards in the current
/// draft, and at most one additional eligible name.
/// </summary>
public static class EdhLabThreeCardEngines
{
    private sealed record Pathway(string Theme, string Explanation,
        Func<EdhLabDraftCatalog.Card, bool> Producer,
        Func<EdhLabDraftCatalog.Card, bool> Bridge,
        Func<EdhLabDraftCatalog.Card, bool> Reward);

    private static bool Has(string? text, string token) =>
        (text ?? "").Contains(token, StringComparison.OrdinalIgnoreCase);
    private static bool Any(string? text, params string[] tokens) =>
        tokens.Any(t => Has(text, t));

    private static bool ArtifactToken(EdhLabDraftCatalog.Card c) =>
        System.Text.RegularExpressions.Regex.IsMatch(c.Oracle ?? "",
            @"\bcreate\b[^.!?;]{0,115}\bartifact(?: creature)? tokens?\b|\bcreate\b[^.!?;]{0,115}\bThopter artifact creature tokens?\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(30));

    private static bool CreatureToken(EdhLabDraftCatalog.Card c) =>
        System.Text.RegularExpressions.Regex.IsMatch(c.Oracle ?? "",
            @"\bcreate\b[^.!?;]{0,115}\bcreature tokens?\b|\bcreate\b[^.!?;]{0,115}\bThopter artifact creature tokens?\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(30));

    private static bool TriggerOnOwnArtifact(EdhLabDraftCatalog.Card c) =>
        Any(c.Oracle, "whenever an artifact enters",
            "whenever another artifact enters",
            "whenever one or more artifacts enter") &&
        Any(c.Oracle, "under your control", "you control");

    private static bool TriggerOnOwnCreature(EdhLabDraftCatalog.Card c) =>
        Any(c.Oracle, "whenever a creature enters",
            "whenever another creature enters",
            "whenever one or more creatures enter") &&
        Any(c.Oracle, "under your control", "you control");

    // The bridge must visibly turn the first effect into the next signal.
    // Our snippets are deliberately conservative and can still miss timing,
    // optionality, wording, ownership, or replacement effects.
    private static readonly Pathway[] Pathways =
    [
        new("Artifacts",
            "Artifact token entry → an artifact-entry life-gain trigger → a life-gain payoff",
            ArtifactToken,
            c => TriggerOnOwnArtifact(c) && Any(c.Oracle,
                "gain 1 life", "gain 2 life", "gain 3 life", "gain life"),
            c => Any(c.Oracle, "whenever you gain life",
                "whenever you gain one or more life")),
        new("Artifacts",
            "Artifact token entry → an artifact-entry card-draw trigger → a draw payoff",
            ArtifactToken,
            c => TriggerOnOwnArtifact(c) && Any(c.Oracle,
                "draw a card", "draw two cards"),
            c => Any(c.Oracle, "whenever you draw a card",
                "whenever you draw one or more cards")),
        new("Tokens",
            "Creature token entry → an entry-triggered counter placement → a counter payoff",
            CreatureToken,
            c => TriggerOnOwnCreature(c) &&
                Has(c.Oracle, "put a +1/+1 counter"),
            c => Any(c.Oracle, "whenever a +1/+1 counter",
                "whenever one or more +1/+1 counters"))
    ];

    public sealed record Engine(string Theme, string Producer,
        string Bridge, string Payoff, string Status, string Explanation,
        string MissingName, long UnusedRecordedCopies, string MissingRoles)
    {
        public string Display =>
            $"[{Theme}] {Producer} → {Bridge} → {Payoff} · {Status}";
    }

    public sealed record Report(IReadOnlyList<Engine> Engines, int UnknownCopies)
    {
        public string Headline => Engines.Count == 0
            ? "No supported three-card pathway with at least two pieces in this draft."
            : $"{Engines.Count} possible three-card pathways to inspect";
        public string Disclaimer =>
            "Conservative Oracle-text cues, not verified combos, loops, triggers or wins. " +
            "Only locally format/identity-eligible candidates are considered. " +
            "No deck or collection edits.";
    }

    public static Report Analyze(EdhLabDraftCatalog catalog,
        IEnumerable<(string Name, int Quantity)> mainboard, int limit = 10)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(mainboard);
        if (limit is < 1 or > 30)
            throw new ArgumentOutOfRangeException(nameof(limit));

        var deck = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int unknown = 0;
        foreach (var (name, quantity) in mainboard)
        {
            if (quantity is < 1 or > 250)
                throw new ArgumentOutOfRangeException(nameof(mainboard));
            if (string.IsNullOrWhiteSpace(name) || !catalog.Contains(name))
            {
                unknown = checked(unknown + quantity);
                continue;
            }
            deck[name] = checked(deck.GetValueOrDefault(name) + quantity);
        }
        var themes = catalog.Synergy.Themes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (themes.Count == 0 || deck.Count < 2)
            return new Report(Array.Empty<Engine>(), unknown);

        var eligible = catalog.AllEligibleCards;
        var found = new List<Engine>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Pathways.Where(p => themes.Contains(p.Theme)))
        {
            // Bound search cost for larger collections. All drafted candidates
            // come first so catalog order does not hide an existing engine.
            EdhLabDraftCatalog.Card[] Choose(
                Func<EdhLabDraftCatalog.Card, bool> predicate) =>
                eligible.Where(predicate)
                    .OrderByDescending(c => deck.ContainsKey(c.Name))
                    .ThenByDescending(c => c.RecordedCopies > 0)
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(28).ToArray();
            var first = Choose(path.Producer);
            var middle = Choose(path.Bridge);
            var last = Choose(path.Reward);
            foreach (var a in first)
            foreach (var b in middle)
            foreach (var c in last)
            {
                if (a.Name.Equals(b.Name, StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase) ||
                    b.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                var cards = new[] { a, b, c };
                var missing = cards.Where(x => !deck.ContainsKey(x.Name)).ToArray();
                if (missing.Length > 1) continue;
                var candidate = missing.FirstOrDefault();
                long available = candidate is null ? 0 : Math.Max(0, Math.Min(
                    candidate.RecordedCopies,
                    (long)candidate.SuggestedCopyLimit));
                // An exhausted but recorded singleton is not a valid addition.
                if (candidate is not null &&
                    candidate.RecordedCopies > 0 && available == 0) continue;
                var status = candidate is null ? "All three in deck"
                    : available > 0 ? "Owned missing link" : "Not recorded";
                string key = path.Theme + "\u001f" + a.Name + "\u001f" +
                    b.Name + "\u001f" + c.Name;
                if (!keys.Add(key)) continue;
                found.Add(new Engine(path.Theme, a.Name, b.Name, c.Name,
                    status, path.Explanation +
                    ". Inspect complete Oracle text for scope, timing, optional effects and choices.",
                    candidate?.Name ?? "", available, candidate?.RoleHints ?? ""));
            }
        }

        return new Report(found
            .OrderBy(x => x.Status == "All three in deck" ? 0 :
                x.Status == "Owned missing link" ? 1 : 2)
            .ThenBy(x => x.Theme, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Producer, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Bridge, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Payoff, StringComparer.OrdinalIgnoreCase)
            .Take(limit).ToArray(), unknown);
    }
}
