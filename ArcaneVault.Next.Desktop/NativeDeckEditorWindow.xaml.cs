using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

public partial class NativeDeckEditorWindow : Window
{
    private readonly string nativePath;
    private readonly string expectedSha;
    private readonly NativeDeckEditing.DeckRow? original;
    private readonly NativeBackupView ownership;
    private readonly string? catalogPath;
    private bool busy;
    private int draftVersion;
    private int previewVersion;
    private CancellationTokenSource? previewRequest;
    private sealed record OwnedDisplay(string Name, long Owned)
    {
        public string Display => $"{Name}  ·  {Owned:N0} owned";
    }

    private void RefreshOwnedCards()
    {
        if (OwnedCardsList is null || OwnedPoolBox is null || OwnedSearchBox is null)
            return;
        var pool = OwnedPoolBox.SelectedIndex == 1
            ? NativeDeckOwnedPicker.Pool.Arena : NativeDeckOwnedPicker.Pool.Physical;
        OwnedCardsList.ItemsSource = NativeDeckOwnedPicker.Search(
            ownership, pool, OwnedSearchBox.Text)
            .Select(x => new OwnedDisplay(x.Name, x.Owned)).ToArray();
    }

    private async void OnOwnedCardSelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Selection is user-driven. Never fetch art during list population or
        // scrolling; cancel prior requests and wait for selection to settle.
        previewRequest?.Cancel();
        int version = ++previewVersion;
        if (PreviewCardName is null || PreviewArtwork is null ||
            PreviewStatus is null || PreviewMetadata is null ||
            PreviewOracleText is null || PreviewArtFallback is null ||
            PreviewOwnership is null || OwnedCardsList is null)
            return;

        PreviewArtwork.Source = null;
        PreviewArtwork.Visibility = Visibility.Collapsed;
        PreviewArtFallback.Visibility = Visibility.Visible;
        PreviewOracleText.Text = "";
        PreviewMetadata.Text = "";
        if (OwnedCardsList.SelectedItem is not OwnedDisplay card)
        {
            PreviewCardName.Text = "Select an owned card";
            PreviewOwnership.Text = "";
            PreviewStatus.Text = "Preview is read-only; selecting a card does not modify your deck.";
            return;
        }

        PreviewCardName.Text = card.Name;
        RefreshPreviewOwnership();
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            PreviewMetadata.Text = "No offline Scryfall catalog available.";
            PreviewStatus.Text = "Use Full Sync to load card metadata and artwork.";
            return;
        }

        bool arena = OwnedPoolBox.SelectedIndex == 1;
        var request = new CancellationTokenSource();
        previewRequest = request;
        var token = request.Token;
        PreviewStatus.Text = "Loading local card details…";
        try
        {
            // Rapid keyboard navigation should not generate one network
            // request per transient selection.
            await Task.Delay(220, token);
            var preview = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var info = CollectionCatalogReview.ReadDetails(catalogPath!, card.Name);
                string imageUrl = arena ? "" : info?.ArtworkUrl ?? "";
                if (arena && info is not null)
                {
                    // Never use a potentially paper-only card image for Arena.
                    // Printing gallery returns one representative Arena image,
                    // not a claimed owned digital cosmetic style.
                    try
                    {
                        imageUrl = NativePrintingArtwork.Read(catalogPath!,
                            card.Name, limit: 1, cancellation: token,
                            arenaOnly: true).Printings.FirstOrDefault()?.ImageUrl ?? "";
                    }
                    catch (Exception ex) when (ex is InvalidDataException or
                        Microsoft.Data.Sqlite.SqliteException)
                    {
                        imageUrl = "";
                    }
                }
                return (Info: info, ImageUrl: imageUrl);
            }, token);
            if (token.IsCancellationRequested || version != previewVersion) return;
            if (preview.Info is null)
            {
                PreviewMetadata.Text = "This card is not present in the offline catalog.";
                PreviewStatus.Text = "Ownership remains available; refresh card metadata to preview.";
                return;
            }

            PreviewMetadata.Text = preview.Info.TypeLine +
                $"  ·  Mana value {preview.Info.ManaValue:G}" +
                (string.IsNullOrWhiteSpace(preview.Info.ColorIdentity)
                    ? "  ·  Colorless" : "  ·  Identity: " + preview.Info.ColorIdentity);
            PreviewOracleText.Text = preview.Info.OracleText;
            if (!NativeCardArtwork.IsTrustedArtworkUrl(preview.ImageUrl))
            {
                PreviewStatus.Text = "No suitable official artwork in the current catalog. " +
                    "This preview does not identify an owned printing.";
                return;
            }

            PreviewStatus.Text = "Loading illustrative official artwork…";
            var bytes = await NativeCardArtwork.LoadAsync(preview.ImageUrl, token);
            if (token.IsCancellationRequested || version != previewVersion) return;
            if (bytes is null)
            {
                PreviewStatus.Text = "Artwork unavailable; offline card text is still shown.";
                return;
            }
            using var stream = new MemoryStream(bytes);
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 320;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            PreviewArtwork.Source = bitmap;
            PreviewArtwork.Visibility = Visibility.Visible;
            PreviewArtFallback.Visibility = Visibility.Collapsed;
            PreviewStatus.Text = "Illustrative catalog artwork · not verification of an owned printing.";
        }
        catch (OperationCanceledException) { /* Selection changed or editor closed. */ }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidDataException or Microsoft.Data.Sqlite.SqliteException or
            ArgumentException or HttpRequestException or System.NotSupportedException)
        {
            if (!token.IsCancellationRequested && version == previewVersion)
                PreviewStatus.Text = "Card preview unavailable: " + ex.Message;
        }
        finally
        {
            if (ReferenceEquals(previewRequest, request))
                previewRequest = null;
            request.Dispose();
        }
    }

    private void RefreshPreviewOwnership()
    {
        if (PreviewOwnership is null || MainboardBox is null ||
            OwnedCardsList?.SelectedItem is not OwnedDisplay card)
            return;
        try
        {
            var inDeck = NativeDeckEditing.ParseMainboard(MainboardBox.Text)
                .FirstOrDefault(x => x.Name.Equals(card.Name,
                    StringComparison.OrdinalIgnoreCase))?.Quantity ?? 0;
            PreviewOwnership.Text = $"{card.Owned:N0} owned · {inDeck:N0} in mainboard";
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException)
        {
            PreviewOwnership.Text = $"{card.Owned:N0} owned · draft needs review";
        }
    }

    private void InvalidateAnalytics()
    {
        draftVersion++;
        if (AnalyticsCharts is null || AnalyticsText is null) return;
        AnalyticsCharts.Visibility = Visibility.Collapsed;
        AnalyticsText.Text = "Deck changed. Select Analyze deck to refresh the charts.";
    }

    private void OnDraftChanged(object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        InvalidateAnalytics();
        RefreshDraftSummary();
        RefreshPreviewOwnership();
    }

    private void OnFormatChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(() =>
        {
            InvalidateAnalytics();
            RefreshDraftSummary();
        }));

    private void RefreshDraftSummary()
    {
        if (DeckSummaryText is null || DeckShortageText is null ||
            MainboardBox is null || CommanderBox is null || FormatBox is null ||
            OwnedPoolBox is null)
            return;
        var pool = OwnedPoolBox.SelectedIndex == 1
            ? NativeDeckOwnedPicker.Pool.Arena : NativeDeckOwnedPicker.Pool.Physical;
        try
        {
            var summary = NativeDeckOwnedPicker.Review(ownership, pool,
                MainboardBox.Text, CommanderBox.Text, FormatBox.Text);
            DeckSummaryText.Text = summary.Header;
            string poolName = pool == NativeDeckOwnedPicker.Pool.Arena ? "Arena" : "Physical";
            var lines = summary.Shortages.Take(6).Select(x =>
                $"{x.Name}: need {x.Required}, owned {x.Owned} (missing {x.Missing})");
            DeckShortageText.Text = summary.Shortages.Count == 0
                ? $"All draft copies covered by {poolName} inventory."
                : $"{poolName} shortages ({summary.Shortages.Count} cards):\n" +
                  string.Join("\n", lines) +
                  (summary.Shortages.Count > 6
                      ? $"\n… plus {summary.Shortages.Count - 6} more card names."
                      : "");
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException)
        {
            DeckSummaryText.Text = "Deck draft needs review";
            DeckShortageText.Text = ex.Message;
        }
    }

    private void OnOwnedSearchChanged(object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        if (OwnedSearchBox?.Text.Length > 255)
        {
            StatusText.Text = "Owned-card search must be 255 characters or fewer.";
            return;
        }
        RefreshOwnedCards();
    }

    private void OnOwnedPoolChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshOwnedCards();
        RefreshDraftSummary();
    }

    private async void OnAnalyzeDeckClicked(object sender, RoutedEventArgs e)
    {
        if (busy || !AnalyzeDeckButton.IsEnabled) return;
        AnalyticsPanel.IsExpanded = true;
        IReadOnlyList<NativeDeckEditing.Entry> entries;
        try
        {
            entries = NativeDeckEditing.ParseMainboard(MainboardBox.Text);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            AnalyticsCharts.Visibility = Visibility.Collapsed;
            AnalyticsText.Text = "Deck analytics unavailable: " + ex.Message;
            return;
        }

        // Snapshot the draft before crossing the async boundary. Late results
        // must never be shown for a different deck or format.
        int version = draftVersion;
        string mainboard = MainboardBox.Text;
        string commander = CommanderBox.Text;
        string format = FormatBox.Text;
        AnalyzeDeckButton.IsEnabled = false;
        AnalyticsCharts.Visibility = Visibility.Collapsed;
        AnalyticsText.Text = "Analyzing offline Scryfall metadata…";
        try
        {
            var report = await Task.Run(() => NativeDeckAnalytics.Analyze(
                entries, commander, format, catalogPath));
            if (version != draftVersion ||
                MainboardBox.Text != mainboard ||
                CommanderBox.Text != commander ||
                FormatBox.Text != format)
            {
                AnalyticsText.Text = "Deck changed during analysis. Select Analyze deck again.";
                return;
            }
            var charts = NativeDeckAnalyticsCharts.FromReport(report);
            ManaCurveBars.ItemsSource = charts.Curve;
            ColorIdentityBars.ItemsSource = charts.Identity;
            CardTypeBars.ItemsSource = charts.Types;
            AverageManaText.Text = report.AverageNonlandManaValue.HasValue
                ? $"Average nonland mana value: {report.AverageNonlandManaValue.Value:F2}"
                : "Average nonland mana value: unavailable";
            AnalyticsText.Text = report.CoverageSummary +
                (report.UnknownCards > 0
                    ? " · Unknown copies excluded from the charts."
                    : " · All deck copies matched offline metadata.");
            AnalyticsCharts.Visibility = Visibility.Visible;
        }
        catch (Exception ex) when (ex is InvalidDataException or
            Microsoft.Data.Sqlite.SqliteException or IOException or
            UnauthorizedAccessException or ArgumentException or OverflowException)
        {
            AnalyticsCharts.Visibility = Visibility.Collapsed;
            AnalyticsText.Text = "Analytics unavailable: " + ex.Message +
                " (the deck and collection were not modified).";
        }
        finally
        {
            AnalyzeDeckButton.IsEnabled = true;
        }
    }

    private void OnOwnedCardDoubleClicked(object sender,
        System.Windows.Input.MouseButtonEventArgs e) => AddSelectedOwnedCard();

    private void OnAddOwnedClicked(object sender, RoutedEventArgs e) => AddSelectedOwnedCard();

    private void OnRemoveOwnedClicked(object sender, RoutedEventArgs e)
    {
        if (busy || OwnedCardsList.SelectedItem is not OwnedDisplay card) return;
        try
        {
            MainboardBox.Text = NativeDeckOwnedPicker.RemoveOne(MainboardBox.Text, card.Name);
            StatusText.Text = $"Removed 1 {card.Name} from the unsaved draft. " +
                "Collection quantities are unchanged.";
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or OverflowException)
        {
            StatusText.Text = "Could not remove card: " + ex.Message;
        }
    }

    private void OnSortMainboardClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        try
        {
            string sorted = NativeDeckOwnedPicker.SortAlphabetically(MainboardBox.Text);
            if (sorted != MainboardBox.Text)
                MainboardBox.Text = sorted;
            StatusText.Text = "Sorted the unsaved mainboard alphabetically. " +
                "Commander, ownership and saved decks are unchanged.";
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or OverflowException)
        {
            StatusText.Text = "Could not sort draft: " + ex.Message;
        }
    }


    private void AddSelectedOwnedCard()
    {
        if (busy || OwnedCardsList.SelectedItem is not OwnedDisplay card) return;
        try
        {
            MainboardBox.Text = NativeDeckOwnedPicker.AddOne(MainboardBox.Text, card.Name);
            StatusText.Text = $"Added 1 {card.Name} to the unsaved draft. " +
                "Collection quantities are unchanged; save still requires preflight review.";
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or OverflowException)
        {
            StatusText.Text = "Could not add card: " + ex.Message;
        }
    }

    public NativeDeckEditorWindow(string nativePath, string expectedSha,
        NativeBackupView ownership, string? catalogPath,
        NativeDeckEditing.DeckRow? selected = null)
    {
        this.nativePath = nativePath;
        this.expectedSha = expectedSha;
        this.ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
        this.catalogPath = catalogPath;
        original = selected;
        InitializeComponent();
        Closed += (_, _) => { previewVersion++; previewRequest?.Cancel(); };
        OwnedPoolBox.SelectedIndex = selected?.Format.Equals("Arena Brawl",
            StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
        RefreshOwnedCards();
        FormatBox.Text = selected?.Format ?? "60-card";
        FolderBox.Text = selected?.Folder ?? "Unfiled";
        FavoriteDeckBox.IsChecked = selected?.Favorite ?? false;
        if (selected is not null)
        {
            HeaderText.Text = "Edit saved deck";
            DeckNameBox.Text = selected.Name;
            CommanderBox.Text = selected.Commander;
            MainboardBox.Text = selected.MainboardText;
            StatusText.Text = $"Editing saved deck: {selected.EntryCount:N0} entries, " +
                $"{selected.TotalCopies:N0} mainboard copies and {selected.Revisions:N0} revisions. " +
                "The prior deck is appended to its revision history before changes are committed.";
        }
        RefreshDraftSummary();
    }

    private void ApplyDeckImport(string text)
    {
        if (busy) return;
        try
        {
            var imported = NativeDeckTextExchange.Parse(text);
            string printWarning = imported.RemovedPrintingAnnotations
                ? "This text lists Arena set/collector annotations. The native deck editor " +
                  "stores card names and quantities, NOT those exact printing choices.\n\n"
                : "";
            string commanderWarning = imported.Commander.Length == 0
                ? "No Commander section detected; the current Commander field will be kept.\n\n"
                : $"Commander: {imported.Commander}\n\n";
            string prompt =
                $"Import {imported.MainboardCopies:N0} mainboard copies across " +
                $"{imported.CardNames:N0} card names?\n\n" +
                commanderWarning + printWarning +
                "This replaces the TEXT currently visible in the deck editor; " +
                "it does not save or modify your collection until you press Review and save deck.\n\n" +
                "Continue?";
            if (MessageBox.Show(this, prompt, "Review imported decklist",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            MainboardBox.Text = imported.MainboardText;
            if (imported.Commander.Length > 0)
                CommanderBox.Text = imported.Commander;
            StatusText.Text = $"Loaded {imported.SourceKind}: " +
                $"{imported.CardNames:N0} card names, {imported.MainboardCopies:N0} copies. " +
                "Not saved yet; review the selected format and press Review and save deck.";
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            StatusText.Text = "Deck import rejected: " + ex.Message;
            MessageBox.Show(this, ex.Message,
                "Decklist needs review", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnPasteDeckClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        try
        {
            if (!Clipboard.ContainsText())
                throw new InvalidDataException("Clipboard has no plain-text decklist.");
            ApplyDeckImport(Clipboard.GetText());
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Runtime.InteropServices.COMException)
        {
            StatusText.Text = "Unable to paste decklist: " + ex.Message;
        }
    }

    private void OnImportTextClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var picker = new OpenFileDialog
        {
            Title = "Import a plain-text or MTG Arena decklist",
            Filter = "Decklist text (*.txt)|*.txt|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var info = new FileInfo(picker.FileName);
            if (!info.Exists || info.Length is < 1 or > 256_000)
                throw new InvalidDataException("Decklist files must be smaller than 256 KB.");
            ApplyDeckImport(File.ReadAllText(info.FullName, Encoding.UTF8));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            StatusText.Text = "Unable to read decklist: " + ex.Message;
        }
    }

    private void OnCopyDeckClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        try
        {
            string exported = NativeDeckTextExchange.Export(MainboardBox.Text, CommanderBox.Text);
            Clipboard.SetText(exported);
            StatusText.Text = "Copied a name/quantity Arena-style decklist. " +
                "This export does not include printings, sideboards, deck history or ownership.";
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or
            System.Runtime.InteropServices.COMException)
        {
            StatusText.Text = "Could not copy decklist: " + ex.Message;
        }
    }

    private void OnExportTextClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        string exported;
        try { exported = NativeDeckTextExchange.Export(MainboardBox.Text, CommanderBox.Text); }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            StatusText.Text = "Cannot export decklist: " + ex.Message;
            return;
        }
        string filename = new string(
            (DeckNameBox.Text.Trim().Length == 0 ? "ArcaneVaultDeck" : DeckNameBox.Text.Trim())
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());
        var picker = new SaveFileDialog
        {
            Title = "Export current deck as Arena-style text",
            FileName = filename + ".txt",
            Filter = "Decklist text (*.txt)|*.txt",
            AddExtension = true,
            DefaultExt = ".txt",
            OverwritePrompt = true
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            // Export only; never touch the authoritative native SQLite deck library.
            File.WriteAllText(picker.FileName, exported, new UTF8Encoding(false));
            StatusText.Text = "Exported decklist text. Exact card printings, deck history " +
                "and organization remain only in your full native backup.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = "Could not write decklist: " + ex.Message;
        }
    }

    private async void OnSaveDeckClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        string name = DeckNameBox.Text.Trim();
        string commander = CommanderBox.Text.Trim();
        string format = FormatBox.Text.Trim();
        string folder = FolderBox.Text.Trim();
        bool favorite = FavoriteDeckBox.IsChecked == true;
        string mainboard = MainboardBox.Text;
        IReadOnlyList<NativeDeckEditing.Entry> entries;
        try
        {
            entries = NativeDeckEditing.ParseMainboard(mainboard);
            if (name.Length is < 1 or > 120 || name.Any(char.IsControl) ||
                commander.Length > 255 || commander.Any(char.IsControl) ||
                format.Length is < 1 or > 60 || format.Any(char.IsControl) ||
                folder.Length is < 1 or > 64 || folder.Any(char.IsControl))
                throw new InvalidDataException("Invalid deck or commander name.");
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            StatusText.Text = ex.Message;
            return;
        }

        busy = true;
        SaveDeckButton.IsEnabled = false;
        StatusText.Text = "Reviewing the saved cards against native ownership and local Scryfall metadata…";
        try
        {
            // Runs without changing the native database. Reads local catalog in
            // read-only mode and treats unavailable metadata as unverified.
            var report = await Task.Run(() => NativeDeckPreflight.Check(
                ownership, entries, commander, format, catalogPath));

            int copies = entries.Sum(entry => entry.Quantity);
            string action = original is null ? "CREATE" : "UPDATE";
            var details = report.Findings.Take(18)
                .Select(f => "• " + f.Severity + ": " + f.Message);
            int omitted = Math.Max(0, report.Findings.Count - 18);
            string reportText = string.Join("\n", details);
            if (omitted > 0) reportText += $"\n…plus {omitted} additional review notes.";
            string prompt =
                $"{action} \"{name}\" ({format}) with {copies:N0} mainboard copies across " +
                $"{entries.Count:N0} distinct card names · Folder: {folder}" +
                (favorite ? " · ★ Favorite" : "") +
                (commander.Length > 0 ? $", Commander: {commander}" : ", no commander") +
                $"?\n\nRead-only deck preflight: {report.Summary}\n" +
                (report.CatalogChecked
                    ? "Metadata from the offline Scryfall catalog.\n"
                    : "Catalog legality checks unavailable.\n") +
                (reportText.Length > 0 ? "\n" + reportText + "\n" : "") +
                "\nReview is advisory, not a guarantee of Arena or tournament legality. " +
                "Save anyway? Your physical/Arena inventory will NOT change. " +
                "The complete previous native database will be retained for recovery.";

            // Explicit consent is required even when warnings are present.
            if (MessageBox.Show(this, prompt, "Review native deck and save",
                MessageBoxButton.YesNo,
                report.Warnings > 0 ? MessageBoxImage.Warning :
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                StatusText.Text = "Save canceled after review. " + report.Summary +
                    ". No collection data changed.";
                return;
            }

            StatusText.Text = "Verifying and saving the native deck with automatic recovery…";
            var saved = await Task.Run(() =>
                NativeWorkspaceSafety.SaveDeck(nativePath, name, commander,
                    mainboard, original, expectedSha, format: format,
                    folder: folder, favorite: favorite));
            MessageBox.Show(this,
                $"Deck saved. Library now contains {saved.Current.Collection.Decks:N0} decks " +
                $"and {saved.Current.Collection.DeckRevisions:N0} revisions.\n\n" +
                "The complete prior native collection is preserved at:\n" +
                saved.RecoveryPath,
                "Saved deck verified", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Review or save did not finish. No automatic reset was performed. " +
                ex.Message;
            MessageBox.Show(this, StatusText.Text,
                "Deck action stopped", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            busy = false;
            SaveDeckButton.IsEnabled = true;
        }
    }
}
