using ArcaneVault.Next.Collection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Shared on-demand card preview for saved decks and commander discovery.
/// Reads only the existing isolated Scryfall catalog; never modifies card
/// ownership or starts requests for cards outside the current selection.
/// </summary>
public partial class NativeCardPeekPane : UserControl
{
    private int selectionVersion;
    private string selectedName = "";
    private string selectedOwnership = "";
    private string? selectedCatalogPath;
    private CollectionCatalogReview.CardDetails? selectedInfo;

    public NativeCardPeekPane()
    {
        InitializeComponent();
        PeekOracle.Document = OracleDocument("Select a card to see its official rules.");
        Unloaded += (_, _) => ++selectionVersion;
    }

    private static FlowDocument OracleDocument(string? oracle)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontSize = 13,
            LineHeight = 20
        };
        foreach (var line in (oracle ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 6) };
            foreach (var token in NativeOracleSymbolTokenizer.Split(line))
            {
                if (token.IsSymbol && token.Text.Length >= 3)
                    paragraph.Inlines.Add(new InlineUIContainer(new NativeManaSymbol
                    {
                        Symbol = token.Text[1..^1],
                        Width = 17,
                        Height = 17
                    }) { BaselineAlignment = BaselineAlignment.Center });
                else
                    paragraph.Inlines.Add(new Run(token.Text));
            }
            doc.Blocks.Add(paragraph);
        }
        return doc;
    }

    public void Clear(string prompt = "Select a card")
    {
        selectionVersion++;
        selectedName = "";
        selectedOwnership = "";
        selectedInfo = null;
        selectedCatalogPath = null;
        PeekName.Text = prompt;
        PeekArt.ArtUrl = "";
        PeekOwnership.Text = "";
        PeekType.Text = "";
        PeekIdentity.ItemsSource = null;
        PeekOracle.Document = OracleDocument("Select a card to see its official rules.");
        PeekStatus.Text = "Read-only preview";
        PeekFullDetails.IsEnabled = false;
    }

    public async Task InspectAsync(string name, string ownership,
        string? catalogPath, string? fallbackType = null,
        string? fallbackOracle = null, string? fallbackIdentity = null,
        string? fallbackArt = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Clear();
            return;
        }
        int version = ++selectionVersion;
        selectedName = name;
        selectedOwnership = ownership;
        selectedInfo = null;
        selectedCatalogPath = catalogPath;
        PeekName.Text = name;
        PeekName.ToolTip = name;
        PeekArt.ArtUrl = NativeCardArtwork.IsTrustedArtworkUrl(fallbackArt)
            ? fallbackArt! : "";
        PeekOwnership.Text = ownership;
        PeekType.Text = fallbackType ?? "Loading card metadata…";
        PeekIdentity.ItemsSource = fallbackIdentity is null ? null :
            NativeManaIdentity.ParseColorIdentity(fallbackIdentity);
        PeekOracle.Document = OracleDocument(fallbackOracle ?? "Loading rules text…");
        PeekStatus.Text = "Read-only preview · no owned printing inferred";
        PeekFullDetails.IsEnabled = false;

        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            PeekType.Text = fallbackType ?? "Catalog not loaded";
            PeekOracle.Document = OracleDocument(fallbackOracle ??
                "A local Scryfall catalog is needed for this card's rules.");
            PeekStatus.Text = "Local commander information only · no collection edits";
            return;
        }

        CollectionCatalogReview.CardDetails? info;
        try
        {
            info = await Task.Run(() =>
                CollectionCatalogReview.ReadDetails(catalogPath, name));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or
            Microsoft.Data.Sqlite.SqliteException or UnauthorizedAccessException or
            ArgumentException)
        {
            info = null;
        }
        if (!IsLoaded || version != selectionVersion) return;

        selectedInfo = info;
        if (info is null)
        {
            PeekType.Text = fallbackType ?? "No verified local metadata";
            PeekOracle.Document = OracleDocument(fallbackOracle ??
                "No matching card details in the local catalog.");
            PeekStatus.Text = "Catalog lookup unavailable; saved deck and collection untouched";
            return;
        }

        PeekType.Text = info.TypeLine;
        PeekIdentity.ItemsSource = NativeManaIdentity.ParseColorIdentity(info.ColorIdentity);
        PeekOracle.Document = OracleDocument(info.OracleText);
        if (NativeCardArtwork.IsTrustedArtworkUrl(info.ArtworkUrl))
            PeekArt.ArtUrl = info.ArtworkUrl;
        PeekFullDetails.IsEnabled = true;
        PeekStatus.Text = "Cached Scryfall metadata · preview is not ownership verification";
    }

    private void OnFullDetailsClicked(object sender, RoutedEventArgs e)
    {
        if (selectedInfo is null || selectedName.Length == 0) return;
        // Read-only full details: saved-deck browsing may hold an older native
        // SHA, so this path deliberately cannot write shortlist/art preferences.
        var dialog = new NativeCardDetailsWindow(selectedName, selectedOwnership,
            selectedInfo, catalogPath: selectedCatalogPath)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.Show();
    }
}
