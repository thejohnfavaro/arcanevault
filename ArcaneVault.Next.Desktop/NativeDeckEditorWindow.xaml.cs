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

    private void OnDraftChanged(object sender,
        System.Windows.Controls.TextChangedEventArgs e) => RefreshDraftSummary();

    private void OnFormatChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(RefreshDraftSummary));

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

    private void OnOwnedCardDoubleClicked(object sender,
        System.Windows.Input.MouseButtonEventArgs e) => AddSelectedOwnedCard();

    private void OnAddOwnedClicked(object sender, RoutedEventArgs e) => AddSelectedOwnedCard();

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
