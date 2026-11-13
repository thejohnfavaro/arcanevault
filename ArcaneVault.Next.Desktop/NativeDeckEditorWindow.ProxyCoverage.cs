using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.IO;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Optional paper proxy coverage for decks. Proxies remain distinct from the
/// native real-ownership snapshot and never contribute to paper valuation.
/// </summary>
public partial class NativeDeckEditorWindow
{
    private IReadOnlyList<NativeProxyEditing.ProxyRow> proxyLedger =
        Array.Empty<NativeProxyEditing.ProxyRow>();
    private readonly Dictionary<string, long> proxyCounts =
        new(StringComparer.OrdinalIgnoreCase);
    private bool proxyCoverageClosed;

    private long CountProxy(string name) => OwnedPoolBox?.SelectedIndex == 1
        ? 0 : proxyCounts.GetValueOrDefault(name);

    private async Task LoadProxyCoverageAsync()
    {
        if (string.IsNullOrWhiteSpace(nativePath)) return;
        try
        {
            string path = nativePath;
            var rows = await Task.Run(() => NativeWorkspaceSafety.ListProxies(path));
            if (proxyCoverageClosed) return;
            proxyLedger = rows;
            proxyCounts.Clear();
            foreach (var row in rows)
                proxyCounts[row.Name] = checked(
                    proxyCounts.GetValueOrDefault(row.Name) + row.Quantity);
            // The existing draft text is still authoritative. Re-render only
            // read-only coverage, labels, and candidate search—not the deck.
            RefreshDraftSummary();
            if (previewedCard is not null)
            {
                previewedCard = previewedCard with {
                    Proxied = CountProxy(previewedCard.Name)
                };
                RefreshPreviewOwnership();
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or
            SqliteException or IOException or UnauthorizedAccessException or
            ArgumentException or OverflowException)
        {
            if (!proxyCoverageClosed)
                StatusText.Text = "Proxy coverage unavailable: " + ex.Message +
                    ". Deck editing still works; missing-copy checks use only " +
                    "verified real ownership until the ledger is available.";
        }
    }
}
