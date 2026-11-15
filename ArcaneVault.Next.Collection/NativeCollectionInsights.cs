using ArcaneVault.Next.Domain;
using System.Globalization;

namespace ArcaneVault.Next.Collection;

/// <summary>Collection intelligence from authoritative, read-only native data.
 /// Never treats missing quotes as zero-priced market observations.</summary>
public static class NativeCollectionInsights
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    public sealed record SetRank(string Code, decimal PricedUsd, int PricedRows,
        int MissingRows);
    public sealed record CardRank(string Name, string Set, string Finish,
        decimal LineUsd, int Quantity);
    public sealed record DeckFit(string Name, string Format, int Covered, int Required,
        long Missing, IReadOnlyList<NativeDeckOwnedPicker.Shortage> Needs)
    {
        public string Summary => $"{Name} · {Covered:N0}/{Required:N0} copies owned" +
            (Missing > 0 ? $" · {Missing:N0} missing" : " · buildable");
    }
    public sealed record Report(decimal PricedUsd, long PricedCopies,
        int PricedRows, int UnpricedRows, int UniqueOwnedNames,
        IReadOnlyList<SetRank> Sets,
        IReadOnlyList<CardRank> Cards,
        IReadOnlyList<string> Unpriced,
        IReadOnlyList<DeckFit> Decks,
        bool Stale)
    {
        public string Summary => $"{PricedUsd.ToString("C2",Us)} priced subtotal" +
            $" · {PricedRows:N0}/{PricedRows + UnpricedRows:N0} printing rows priced" +
            $" · {UniqueOwnedNames:N0} distinct physical card names" +
            (Stale ? " · quote snapshot stale" : "");
        public decimal PriceCoverage => PricedRows + UnpricedRows == 0 ? 0m :
            100m * PricedRows / (PricedRows + UnpricedRows);
    }

    public static Report Build(NativePhysicalValuation.Result prices,
        NativeBackupView collection,
        IEnumerable<NativeDeckEditing.DeckRow> decks,
        int limit = 12)
    {
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(decks);
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var all = prices.Lines;
        var topCards = all.Where(x => x.LineUsd.HasValue && x.Source.Confirmed)
            .OrderByDescending(x => x.LineUsd!.Value)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => new CardRank(x.Name, x.Source.ConfirmedSet, x.Finish,
                x.LineUsd!.Value, x.Quantity)).ToArray();
        var bySet = all.Where(x => x.Source.Confirmed)
            .GroupBy(x => x.Source.ConfirmedSet, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SetRank(g.Key,
                g.Where(x => x.LineUsd.HasValue).Sum(x => x.LineUsd!.Value),
                g.Count(x => x.LineUsd.HasValue),
                g.Count(x => !x.LineUsd.HasValue)))
            .Where(s => s.PricedRows > 0)
            .OrderByDescending(s => s.PricedUsd).ThenBy(s => s.Code)
            .Take(limit).ToArray();
        var gaps = all.Where(x => !x.LineUsd.HasValue)
            .OrderByDescending(x => x.Quantity)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => $"{x.Name} · {x.SetAndNumber} · {x.PriceStatus}")
            .ToArray();
        var fits = new List<DeckFit>();
        foreach (var deck in decks.Take(2000))
        {
            if (!deck.Editable || string.IsNullOrWhiteSpace(deck.MainboardText))
                continue;
            bool arena = deck.Format.Equals("Arena Brawl",
                StringComparison.OrdinalIgnoreCase) ||
                deck.Format.Equals("Historic Brawl", StringComparison.OrdinalIgnoreCase);
            try
            {
                var result = NativeDeckOwnedPicker.Review(collection,
                    arena ? NativeDeckOwnedPicker.Pool.Arena :
                        NativeDeckOwnedPicker.Pool.Physical,
                    deck.MainboardText, deck.Commander, deck.Format);
                if (result.TotalWithCommander <= 0) continue;
                fits.Add(new DeckFit(deck.Name, deck.Format,
                    result.CoveredCopies, result.TotalWithCommander,
                    result.MissingCopies, result.Shortages.Take(5).ToArray()));
            }
            catch (ArgumentException) { /* Invalid imported list: no invented deck fit. */ }
            catch (InvalidDataException) { /* Unsupported deck: exclude safely. */ }
        }
        return new Report(prices.PricedSubtotalUsd, prices.PricedCopies,
            prices.PricedRows, prices.MissingRows,
            collection.PhysicalCards.Where(x => x.Quantity > 0)
                .Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            bySet, topCards, gaps,
            fits.OrderBy(x => x.Missing)
                .ThenByDescending(x => x.Covered)
                .ThenBy(x => x.Name,StringComparer.OrdinalIgnoreCase)
                .Take(limit).ToArray(), prices.IsStale);
    }
}
