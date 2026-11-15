using ArcaneVault.Next.Collection;
using System.Globalization;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

/// <summary>Read-only dashboard derived from the same valuation and deck inventory
 /// as Physical Collection. Nothing in this view edits the collection.</summary>
public partial class NativeCollectionWorkspace
{
    private void RefreshCollectionAnalytics()
    {
        if (AnalyticsSummary is null || AnalyticsSets is null ||
            AnalyticsCards is null || AnalyticsDecks is null ||
            AnalyticsMissing is null || AnalyticsCoverage is null)
            return;
        if (valuation is null)
        {
            AnalyticsSummary.Text="Price data not yet calculated.";
            AnalyticsCoverage.Text="The dashboard appears after the first cached Scryfall valuation.";
            return;
        }
        var data=NativeCollectionInsights.Build(valuation,backup,deckRows);
        AnalyticsSummary.Text=data.Summary;
        AnalyticsCoverage.Text=
            $"{data.PriceCoverage:0.0}% of physical printing rows have valid quotes" +
            $" · {data.PricedCopies:N0} priced physical copies" +
            (data.Stale ? " · prices may be stale" : "");
        AnalyticsHealth.Text=
            $"Physical printing verification: {review.ConfirmedCopies:N0} confirmed copies" +
            $" · {review.UnresolvedCopies:N0} unresolved copies" +
            (review.CatalogVerified ? " · catalog verified" : " · catalog not verified") +
            ". Missing prices and unresolved editions do not count as $0 values.";
        AnalyticsCards.Text=data.Cards.Count==0
            ? "No verified priced physical printings."
            : string.Join("\n",data.Cards.Select((x,i)=>
                $"{i+1}. {x.Name} · {x.Set.ToUpperInvariant()} · {x.Finish}" +
                $" · {x.Quantity} copies · {x.LineUsd.ToString("C2",CultureInfo.GetCultureInfo("en-US"))}"));
        AnalyticsSets.Text=data.Sets.Count==0
            ? "No priced physical sets."
            : string.Join("\n",data.Sets.Select((x,i)=>
                $"{i+1}. {x.Code.ToUpperInvariant()}" +
                $" · {x.PricedUsd.ToString("C2",CultureInfo.GetCultureInfo("en-US"))}" +
                $" · {x.PricedRows} priced, {x.MissingRows} unpriced"));
        AnalyticsDecks.Text=data.Decks.Count==0
            ? "No readable saved decks with an owned-card coverage result."
            : string.Join("\n\n",data.Decks.Select(x=>x.Summary +
                (x.Needs.Count==0 ? "" : "\nNeeds: "+string.Join(", ",
                    x.Needs.Select(n=>$"{n.Missing}× {n.Name}")))));
        AnalyticsMissing.Text=data.Unpriced.Count==0
            ? "All tracked printing rows have usable prices."
            : string.Join("\n",data.Unpriced);
        DrawCollectionAnalyticsHistory();
    }
    private void DrawCollectionAnalyticsHistory()
    {
        if(AnalyticsHistoryCanvas is null)return;
        NativeValueHistoryChart.Draw(AnalyticsHistoryCanvas,
            NativeValueHistory.Read(),"Physical collection · Scryfall USD");
    }
}
