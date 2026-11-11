using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.IO;
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
        FormatBox.Text = selected?.Format ?? "60-card";
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
    }

    private async void OnSaveDeckClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        string name = DeckNameBox.Text.Trim();
        string commander = CommanderBox.Text.Trim();
        string format = FormatBox.Text.Trim();
        string mainboard = MainboardBox.Text;
        IReadOnlyList<NativeDeckEditing.Entry> entries;
        try
        {
            entries = NativeDeckEditing.ParseMainboard(mainboard);
            if (name.Length is < 1 or > 120 || name.Any(char.IsControl) ||
                commander.Length > 255 || commander.Any(char.IsControl) ||
                format.Length is < 1 or > 60 || format.Any(char.IsControl))
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
                $"{entries.Count:N0} distinct card names" +
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
                    mainboard, original, expectedSha, format: format));
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
