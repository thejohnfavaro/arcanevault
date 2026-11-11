using Microsoft.Data.Sqlite;
using System.Globalization;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Read-only USD estimates from the official Scryfall DEFAULT_CARDS bulk
/// snapshot, matched to verified physical print IDs and exact finishes.
/// A missing/ambiguous value is unknown, never $0 or another printing's price.
/// Cached bulk prices are indicative and stale after 24 hours.
/// </summary>
public static class NativePhysicalValuation
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public sealed record Line(
        int Ordinal, CollectionCatalogReview.PrintingRow Source,
        decimal? UnitUsd, decimal? LineUsd, string PriceStatus)
    {
        public string Name => Source.Name;
        public int Quantity => Source.Quantity;
        public string SetAndNumber => string.IsNullOrWhiteSpace(Source.Original.SetCode)
            ? Source.SetAndNumber
            : $"{Source.Original.SetCode.ToUpperInvariant()} #{Source.Original.CollectorNumber}".Trim();
        public string Finish => Source.Finish;
        public string Status => Source.Status;
        public string UnitLabel => UnitUsd.HasValue ? UnitUsd.Value.ToString("C2", Us) : "—";
        public string LineLabel => LineUsd.HasValue ? LineUsd.Value.ToString("C2", Us) : "—";
    }

    public sealed record Result(
        IReadOnlyList<Line> Lines, decimal PricedSubtotalUsd,
        long PricedCopies, long MissingCopies, int PricedRows, int MissingRows,
        bool HasPriceColumns, DateTimeOffset? SnapshotUtc, bool IsStale)
    {
        public string PricedSubtotalLabel => PricedSubtotalUsd.ToString("C2", Us);
        public string Summary =>
            $"{PricedSubtotalLabel} priced subtotal • {PricedCopies:N0} priced copies, " +
            $"{MissingCopies:N0} unpriced copies across {MissingRows:N0} rows." +
            (MissingCopies > 0
                ? " Subtotal is NOT your complete collection value." : "");
        public string SnapshotStatus => !HasPriceColumns
            ? "Price fields unavailable: run Full Scryfall Sync in the main window, then reopen Collection."
            : SnapshotUtc is null
                ? "Undated Scryfall price snapshot; values cannot be certified current."
                : $"Scryfall bulk USD estimates as of {SnapshotUtc.Value.UtcDateTime:yyyy-MM-dd HH:mm} UTC" +
                  (IsStale ? " • STALE (>24 hours old)" : " • recent (<24 hours old)");
    }

    private sealed record Price(
        string Name, string Set, string Collector,
        bool Paper, string Usd, string Foil, string Etched);

    private static decimal? ReadPrice(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 24) return null;
        if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var value) ||
            value < 0m || value > 1_000_000m ||
            decimal.Round(value, 2, MidpointRounding.AwayFromZero) != value)
            return null;
        return value;
    }

    public static Result Calculate(CollectionCatalogReview.Result review,
        string? catalogPath, DateTimeOffset? now = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(review);
        DateTimeOffset asOf = now ?? DateTimeOffset.UtcNow;
        bool hasPrices = false;
        DateTimeOffset? snapshot = null;
        var quotes = new Dictionary<Guid, Price>();

        if (review.CatalogVerified && !string.IsNullOrWhiteSpace(catalogPath))
        {
            using var db = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = catalogPath,
                Mode = SqliteOpenMode.ReadOnly, Pooling = false
            }.ToString());
            db.Open();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(variants)";
                var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) cols.Add(reader.GetString(1));
                hasPrices = new[] { "usd", "usd_foil", "usd_etched", "print_id",
                    "name", "setcode", "collector", "games", "digital" }.All(cols.Contains);
            }
            try
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT value FROM state WHERE key='bulk_updated_utc'";
                if (DateTimeOffset.TryParse(cmd.ExecuteScalar()?.ToString(),
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                    out var date))
                    snapshot = date.ToUniversalTime();
            }
            catch (SqliteException) { /* Legacy catalog has no dated bulk state. */ }

            if (hasPrices)
            {
                var wanted = review.Rows.Where(x => x.Confirmed)
                    .Select(x => x.PrintId).Where(x => Guid.TryParse(x, out _))
                    .Select(Guid.Parse).ToHashSet();
                if (wanted.Count > 0)
                {
                    // Scan the catalog once, rather than doing thousands of
                    // per-printing SQL roundtrips for a large physical inventory.
                    using var cmd = db.CreateCommand();
                    cmd.CommandText = """
                        SELECT print_id,name,setcode,collector,games,digital,
                               usd,usd_foil,usd_etched FROM variants
                        """;
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (reader.IsDBNull(0) ||
                            !Guid.TryParse(reader.GetString(0), out var id) ||
                            !wanted.Contains(id)) continue;
                        bool paper = !reader.IsDBNull(4) && !reader.IsDBNull(5) &&
                            reader.GetInt64(5) == 0 &&
                            reader.GetString(4).Split(',',
                                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                                .Any(x => x.Equals("paper", StringComparison.OrdinalIgnoreCase));
                        var quote = new Price(reader.GetString(1), reader.GetString(2),
                            reader.GetString(3), paper,
                            reader.IsDBNull(6) ? "" : reader.GetString(6),
                            reader.IsDBNull(7) ? "" : reader.GetString(7),
                            reader.IsDBNull(8) ? "" : reader.GetString(8));
                        if (!quotes.TryAdd(id, quote))
                            throw new InvalidDataException(
                                "Duplicate catalog Scryfall printing ID; valuation cannot be trusted.");
                    }
                }
            }
        }

        var lines = new List<Line>(review.Rows.Count);
        decimal subtotal = 0m;
        long pricedCopies = 0, missingCopies = 0;
        int pricedRows = 0;
        for (int i = 0; i < review.Rows.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var source = review.Rows[i];
            decimal? unit = null;
            string status;
            if (!source.Confirmed)
                status = "Printing unverified — no price guessed";
            else if (!hasPrices)
                status = "Prices not in catalog — run Full Scryfall Sync";
            else if (!Guid.TryParse(source.PrintId, out var id) ||
                     !quotes.TryGetValue(id, out var data) ||
                     !data.Paper ||
                     !data.Name.Equals(source.Name, StringComparison.OrdinalIgnoreCase) ||
                     !data.Set.Equals(source.ConfirmedSet, StringComparison.OrdinalIgnoreCase) ||
                     !data.Collector.Equals(source.CatalogCollector, StringComparison.OrdinalIgnoreCase))
                status = "No matching verified paper printing quote";
            else
            {
                string finish = source.Finish.Trim().ToLowerInvariant();
                string? raw = finish switch
                {
                    "nonfoil" => data.Usd,
                    "foil" => data.Foil,
                    "etched" => data.Etched,
                    _ => null
                };
                if (raw is null)
                    status = "Finish unspecified — not valued";
                else
                {
                    unit = ReadPrice(raw);
                    status = unit.HasValue
                        ? "Scryfall cached USD estimate"
                        : $"No valid {finish} price in Scryfall snapshot";
                }
            }

            decimal? lineTotal = unit.HasValue
                ? checked(unit.Value * source.Quantity) : null;
            if (lineTotal.HasValue)
            {
                subtotal = checked(subtotal + lineTotal.Value);
                pricedCopies = checked(pricedCopies + source.Quantity);
                pricedRows++;
            }
            else
                missingCopies = checked(missingCopies + source.Quantity);
            lines.Add(new Line(i, source, unit, lineTotal, status));
        }
        if (pricedCopies + missingCopies != review.TotalCopies ||
            lines.Count != review.Rows.Count)
            throw new InvalidDataException("Valuation did not conserve physical inventory.");
        bool stale = snapshot is null || snapshot > asOf.AddMinutes(5) ||
                     asOf - snapshot.Value > TimeSpan.FromHours(24);
        return new Result(lines.AsReadOnly(), subtotal,
            pricedCopies, missingCopies, pricedRows, lines.Count - pricedRows,
            hasPrices, snapshot, stale);
    }
}
