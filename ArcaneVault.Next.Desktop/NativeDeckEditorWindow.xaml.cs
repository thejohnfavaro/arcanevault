using ArcaneVault.Next.Collection;
using System.IO;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

public partial class NativeDeckEditorWindow : Window
{
    private readonly string nativePath;
    private readonly string expectedSha;
    private readonly NativeDeckEditing.DeckRow? original;
    private bool busy;

    public NativeDeckEditorWindow(string nativePath, string expectedSha,
        NativeDeckEditing.DeckRow? selected = null)
    {
        this.nativePath = nativePath;
        this.expectedSha = expectedSha;
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
            // Validate locally before asking the user to confirm the edit.
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

        int copies = entries.Sum(entry => entry.Quantity);
        string action = original is null ? "CREATE" : "UPDATE";
        if (MessageBox.Show(this,
            $"{action} \"{name}\" ({format}) with {copies:N0} mainboard copies across " +
            $"{entries.Count:N0} distinct card names" +
            (commander.Length > 0 ? $", Commander: {commander}" : ", no commander") +
            "?\n\nThis only changes the saved Deck Library. Physical/Arena ownership " +
            "does not change. The full previous native database is preserved " +
            "as a recovery file. Format legality has NOT been validated.",
            "Confirm native deck save", MessageBoxButton.YesNo,
            MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        busy = true;
        SaveDeckButton.IsEnabled = false;
        StatusText.Text = "Verifying and saving the native deck with automatic recovery…";
        try
        {
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
            StatusText.Text = "Save was not confirmed. No automatic reset was performed. " + ex.Message;
            MessageBox.Show(this, StatusText.Text,
                "Deck save stopped", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            busy = false;
            SaveDeckButton.IsEnabled = true;
        }
    }
}
