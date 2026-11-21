using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Read-only comparison of an exported physical inventory with a catalog snapshot.
/// Identities and copies are never inferred from card names or set hints.
/// </summary>
public static class CollectionCatalogReview
{
    public sealed record PrintingRow(
        NativePhysicalRow Original,
        bool Confirmed,
        string Status,
        string ConfirmedSet,
        string CatalogName,
        string CatalogCollector)
    {
        public string Name => Original.Name;
        public int Quantity => Original.Quantity;
        public string SetAndNumber => Original.SetAndNumber;
        public string Finish => Original.Finish;
        public string PrintId => Original.ScryfallId;
        public string SetCode => Confirmed ? ConfirmedSet : "";
    }

    public sealed record SetSummary(string SetCode, int DistinctPrintings, long Copies);

    public sealed record Result(
        bool CatalogVerified,
        IReadOnlyList<PrintingRow> Rows,
        long TotalCopies,
        long ConfirmedCopies,
        long UnresolvedCopies,
        int ConfirmedDistinctPrintings,
        IReadOnlyList<SetSummary> ConfirmedSets)
    {
        public bool QuantitiesConserved => ConfirmedCopies + UnresolvedCopies == TotalCopies;
    }

    private sealed record Printing(Guid Id, string Name, string Set, string Collector, bool IsPaper);

    private static SqliteConnection Open(string path)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        db.Open();
        return db;
    }

    /// <summary>
    /// Null catalog = no verification. Otherwise scan trusted printing IDs using
    /// read-only SQLite and compare both ID and available source metadata.
    /// </summary>
    public static Result Examine(NativeBackupView backup, string? catalogPath,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(backup);
        bool verified = !string.IsNullOrWhiteSpace(catalogPath);
        var presentIds = new HashSet<Guid>();
        foreach (var row in backup.PhysicalCards)
            if (Guid.TryParse(row.ScryfallId, out var id) && id != Guid.Empty)
                presentIds.Add(id);

        var matches = new Dictionary<Guid, Printing>();
        if (verified)
        {
            using var db = Open(catalogPath!);
            using var command = db.CreateCommand();
            command.CommandText = "SELECT print_id,name,setcode,collector,games,digital FROM variants";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cancellation.ThrowIfCancellationRequested();
                if (reader.IsDBNull(0) || !Guid.TryParse(reader.GetString(0), out var id) ||
                    id == Guid.Empty || !presentIds.Contains(id))
                    continue;
                if (reader.IsDBNull(1) || reader.IsDBNull(2) || reader.IsDBNull(3) ||
                    reader.IsDBNull(4) || reader.IsDBNull(5))
                    continue;
                bool hasPaper = reader.GetString(4).Split(',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Any(g => g.Equals("paper", StringComparison.OrdinalIgnoreCase));
                var record = new Printing(id, reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), hasPaper && reader.GetInt64(5) == 0);
                if (!matches.TryAdd(id, record))
                    throw new InvalidDataException("Catalog contains a duplicated Scryfall printing ID.");
            }
        }

        var rows = new List<PrintingRow>(backup.PhysicalCards.Count);
        var sets = new Dictionary<string, (HashSet<Guid> Ids, long Copies)>(
            StringComparer.OrdinalIgnoreCase);
        long confirmed = 0, unresolved = 0;
        var allConfirmed = new HashSet<Guid>();
        foreach (var row in backup.PhysicalCards)
        {
            cancellation.ThrowIfCancellationRequested();
            string status;
            Printing? match = null;
            bool ok = false;
            if (string.IsNullOrWhiteSpace(row.ScryfallId))
                status = "Missing printing ID";
            else if (!Guid.TryParse(row.ScryfallId, out var id) || id == Guid.Empty)
                status = "Invalid printing ID";
            else if (!verified)
                status = "ID supplied — load catalog to verify";
            else if (!matches.TryGetValue(id, out match))
                status = "Printing ID absent from catalog";
            else if (!match.IsPaper)
                status = "Not a physical paper printing";
            else if (string.IsNullOrWhiteSpace(match.Set) ||
                     string.IsNullOrWhiteSpace(match.Collector))
                status = "Catalog printing is missing a set or collector number";
            else if (!string.Equals(row.Name.Trim(), match.Name.Trim(),
                         StringComparison.OrdinalIgnoreCase))
                status = "Card name conflicts with printing ID";
            else if (!string.IsNullOrWhiteSpace(row.SetCode) &&
                     !string.Equals(row.SetCode.Trim(), match.Set.Trim(),
                         StringComparison.OrdinalIgnoreCase))
                status = "Set conflicts with printing ID";
            else if (!string.IsNullOrWhiteSpace(row.CollectorNumber) &&
                     !string.Equals(row.CollectorNumber.Trim(), match.Collector.Trim(),
                         StringComparison.OrdinalIgnoreCase))
                status = "Collector number conflicts with printing ID";
            else
            {
                status = "Verified printing";
                ok = true;
                confirmed = checked(confirmed + row.Quantity);
                allConfirmed.Add(id);
                if (!sets.TryGetValue(match.Set, out var stats))
                    stats = (new HashSet<Guid>(), 0);
                stats.Ids.Add(id);
                stats.Copies = checked(stats.Copies + row.Quantity);
                sets[match.Set] = stats;
            }

            if (!ok)
                unresolved = checked(unresolved + row.Quantity);
            rows.Add(new PrintingRow(row, ok, status,
                ok ? match!.Set : "", ok ? match!.Name : "",
                ok ? match!.Collector : ""));
        }
        if (backup.PhysicalCopies != confirmed + unresolved)
            throw new InvalidDataException("Collection verification failed copy conservation.");

        var perSet = sets.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new SetSummary(pair.Key, pair.Value.Ids.Count, pair.Value.Copies))
            .ToArray();
        return new Result(verified, rows.AsReadOnly(), backup.PhysicalCopies,
            confirmed, unresolved, allConfirmed.Count, perSet);
    }

    public sealed record CardDetails(
        string Name,
        string TypeLine,
        string OracleText,
        string Rarity,
        double ManaValue,
        string ColorIdentity,
        string Legalities,
        string SetCode,
        string ArtworkUrl = "",
        string ManaCost = "");

    /// <summary>
    /// Validate multiple AI-mentioned card identities using one read-only
    /// catalog connection. No guesses, deck edits, or external calls.
    /// </summary>
    public static IReadOnlyList<string> FindExactNames(string catalogPath,
        IEnumerable<string> candidates, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        ArgumentNullException.ThrowIfNull(candidates);
        var verified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var db = Open(catalogPath);
        using var command = db.CreateCommand();
        command.CommandText = "SELECT name FROM cards WHERE name=$name COLLATE NOCASE LIMIT 1";
        var parameter = command.Parameters.Add("$name", SqliteType.Text);
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(80))
        {
            cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 255) continue;

            // ChatGPT sometimes uses typographic apostrophes (Sphinx’s
            // Tutelage), while the Scryfall catalog stores a plain ASCII
            // apostrophe (Sphinx's Tutelage), or vice versa. Try a small,
            // explicit set of punctuation-equivalent spellings; each query
            // still requires a full exact card-name match.
            string normalized = candidate.Trim().Normalize(System.Text.NormalizationForm.FormKC)
                .Replace('’', '\'').Replace('‘', '\'')
                .Replace('ʼ', '\'').Replace('＇', '\'');
            var alternatives = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                candidate.Trim(), normalized,
                normalized.Replace('\'', '’'), normalized.Replace('\'', '‘')
            };
            foreach (string variant in alternatives)
            {
                parameter.Value = variant;
                var match = command.ExecuteScalar()?.ToString();
                if (!string.IsNullOrWhiteSpace(match))
                {
                    verified.Add(match);
                    break;
                }
            }
        }
        return verified.ToArray();
    }

    /// <summary>
    /// Link all real card names explicitly mentioned in a completed AI review,
    /// not just bold phrases and swap-table cells. Scan the locally installed
    /// read-only catalog once; no network requests or AI-provided identifiers.
    /// </summary>
    public static IReadOnlyList<string> FindMentionedNames(
        string catalogPath, string reviewText, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        if (string.IsNullOrWhiteSpace(reviewText)) return Array.Empty<string>();

        static string Normalize(string value) =>
            value.Normalize(System.Text.NormalizationForm.FormKC)
                .Replace('’', '\'').Replace('‘', '\'')
                .Replace('ʼ', '\'').Replace('＇', '\'');

        // We compare against the text without changing what is displayed.
        // Apostrophes are normalized here and in the final WPF hyperlink matcher.
        string review = Normalize(reviewText);
        var matches = new List<string>();
        using var db = Open(catalogPath);
        using var command = db.CreateCommand();
        command.CommandText = "SELECT name FROM cards";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellation.ThrowIfCancellationRequested();
            if (reader.IsDBNull(0)) continue;
            string name = reader.GetString(0);
            if (name.Length < 2 || name.Length > 150) continue;
            string normalized = Normalize(name);
            if (ContainsFullName(review, normalized))
                matches.Add(name);
        }
        // Prioritize longer names so they win over names nested inside them,
        // and cap expensive WPF interval matching for unusually long reviews.
        return matches.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Length)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(150).ToArray();
    }

    /// <summary>
    /// Resolve abbreviated references such as "Tezzeret" to a full local
    /// catalog card. Prefer the reviewed deck when it has exactly one
    /// matching card; otherwise only allow globally unambiguous names.
    /// Never guess if several eligible printings/card identities share
    /// the nickname.
    /// </summary>
    public static IReadOnlyDictionary<string, string> FindShortNameLinks(
        string catalogPath, string reviewText, IEnumerable<string> deckCardNames,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        ArgumentNullException.ThrowIfNull(deckCardNames);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(reviewText)) return result;

        // Only distinctive leading names. Avoid ordinary English and generic
        // card-type phrases; alias matches remain case sensitive in the review.
        static IEnumerable<string> Aliases(string name)
        {
            string lead = name.Split(',')[0].Trim();
            // Short legendary names such as Sai (or Ral) are useful, but
            // require deck context: three-/four-letter strings are too easy
            // to mistake for ordinary text in the wider catalog.
            if (lead.Length >= 3 && !lead.Contains(' ') &&
                char.IsUpper(lead[0])) yield return lead;

            string first = name.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)[0];
            if (first.Length >= 6 && !first.Equals("The", StringComparison.OrdinalIgnoreCase) &&
                char.IsUpper(first[0]) && !first.Equals(lead, StringComparison.OrdinalIgnoreCase))
                yield return first;
        }

        var deckNames = new HashSet<string>(
            deckCardNames.Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim()), StringComparer.OrdinalIgnoreCase);
        var global = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var deck = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        using (var db = Open(catalogPath))
        using (var command = db.CreateCommand())
        {
            command.CommandText = "SELECT DISTINCT name FROM cards";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cancellation.ThrowIfCancellationRequested();
                if (reader.IsDBNull(0)) continue;
                string canonical = reader.GetString(0);
                foreach (string alias in Aliases(canonical))
                {
                    if (!ContainsAlias(reviewText, alias)) continue;
                    if (!global.TryGetValue(alias, out var all))
                        global[alias] = all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    all.Add(canonical);
                    if (deckNames.Contains(canonical))
                    {
                        if (!deck.TryGetValue(alias, out var nearby))
                            deck[alias] = nearby = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        nearby.Add(canonical);
                    }
                }
            }
        }
        foreach (var pair in global)
        {
            cancellation.ThrowIfCancellationRequested();
            // Unique local deck identity outranks multiple catalog cards.
            if (deck.TryGetValue(pair.Key, out var local) && local.Count == 1)
                result[pair.Key] = local.Single();
            else if (pair.Key.Length >= 5 &&
                     (!deck.ContainsKey(pair.Key) || deck[pair.Key].Count == 0) &&
                     pair.Value.Count == 1)
                result[pair.Key] = pair.Value.Single();
        }
        return result;
    }

    private static bool ContainsAlias(string text, string alias)
    {
        int from = 0;
        while (from < text.Length)
        {
            int index = text.IndexOf(alias, from, StringComparison.Ordinal);
            if (index < 0) return false;
            int end = index + alias.Length;
            if ((index == 0 || !char.IsLetterOrDigit(text[index - 1])) &&
                (end == text.Length || !char.IsLetterOrDigit(text[end])))
                return true;
            from = index + 1;
        }
        return false;
    }

    private static bool ContainsFullName(string text, string name)
    {
        // A card is only a mention when its entire name has word boundaries.
        // For single-word card names, require the name's original casing to
        // avoid linking generic prose such as 'draw' or 'counter'.
        bool oneWord = !name.Any(char.IsWhiteSpace);
        var comparison = oneWord ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int offset = 0;
        while (offset < text.Length)
        {
            int index = text.IndexOf(name, offset, comparison);
            if (index < 0) return false;
            int end = index + name.Length;
            bool before = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            bool after = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (before && after) return true;
            offset = index + 1;
        }
        return false;
    }

    /// <summary>Offline card identity details; not a claim about the owned printing.</summary>
    public static CardDetails? ReadDetails(string catalogPath, string name)
    {
        if (string.IsNullOrWhiteSpace(catalogPath) || string.IsNullOrWhiteSpace(name))
            return null;
        using var db = Open(catalogPath);
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT name,type,oracle,rarity,mv,colors,legalities,setcode
            FROM cards WHERE name=$name COLLATE NOCASE LIMIT 1
            """;
        command.Parameters.AddWithValue("$name", name);
        CardDetails? details;
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read()) return null;
            details = new CardDetails(reader.GetString(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(3),
                reader.GetDouble(4), reader.GetString(5),
                reader.GetString(6), reader.GetString(7));
        }
        // The official native Scryfall catalog stores preview artwork URLs.
        // Older synthetic/imported catalogs might not have an image column;
        // card text and ownership remain fully available in that case.
        try
        {
            using var art = db.CreateCommand();
            art.CommandText = "SELECT image FROM cards WHERE name=$name COLLATE NOCASE LIMIT 1";
            art.Parameters.AddWithValue("$name", name);
            details = details with { ArtworkUrl = art.ExecuteScalar()?.ToString() ?? "" };
        }
        catch (SqliteException) { /* Old catalog schema has no image URL. */ }

        // Preserve the exact official token sequence, e.g. {2}{U},
        // {W/U}, {2/R}, {U/P}; it is not a color-identity string.
        // Older catalogs without mana_cost remain readable.
        try
        {
            using var mana = db.CreateCommand();
            mana.CommandText =
                "SELECT mana_cost FROM cards WHERE name=$name COLLATE NOCASE LIMIT 1";
            mana.Parameters.AddWithValue("$name", name);
            details = details with { ManaCost = mana.ExecuteScalar()?.ToString() ?? "" };
        }
        catch (SqliteException) { /* Older catalogs lack mana_cost. */ }
        return details;
    }
}
