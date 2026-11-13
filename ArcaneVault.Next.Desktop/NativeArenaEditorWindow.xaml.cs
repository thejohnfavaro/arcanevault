using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

public partial class NativeArenaEditorWindow : Window
{
    private readonly string nativePath;
    private readonly string expectedSha;
    private readonly int? selectedOrdinal;
    private readonly NativeArenaRow? selectedRow;
    private bool busy;

    public NativeArenaEditorWindow(string nativePath, string expectedSha,
        int? selectedOrdinal = null, NativeArenaRow? selectedRow = null)
    {
        this.nativePath = nativePath;
        this.expectedSha = expectedSha;
        this.selectedOrdinal = selectedOrdinal;
        this.selectedRow = selectedRow;
        if (selectedOrdinal.HasValue != (selectedRow is not null))
            throw new ArgumentException("Editing requires both a selected row and its original index.");
        InitializeComponent();
        if (selectedRow is not null)
        {
            HeadingText.Text = "Edit selected Arena card";
            Title = "Arcane Vault Next — Edit Arena card";
            CardNameBox.Text = selectedRow.Name;
            QuantityBox.Text = selectedRow.Quantity.ToString();
            ModeNote.Text = "Change this specific row's name or quantity. " +
                "Enter 0 to remove this row. Existing deck-list references are preserved, not renamed.";
        }
        else
        {
            QuantityBox.Text = "1";
            ModeNote.Text = "Adds to an unambiguous matching Arena name, or creates a new name-only row. " +
                "The card does not need a printing ID or the Scryfall catalog.";
        }
    }

    private async void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        string name = CardNameBox.Text.Trim();
        bool editing = selectedOrdinal.HasValue;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 ||
            name.Any(char.IsControl))
        {
            StatusText.Text = "Enter a card name of at most 255 characters, without control characters.";
            return;
        }
        if (!int.TryParse(QuantityBox.Text.Trim(), out int count) ||
            count < (editing ? 0 : 1) || count > 99_999)
        {
            StatusText.Text = editing
                ? "Choose 0–99,999. Zero removes this selected Arena row."
                : "Choose 1–99,999 copies to add.";
            return;
        }
        string intent = !editing ? $"Add {count:N0} copies of {name}"
            : count == 0 ? $"REMOVE this selected Arena row: {selectedRow!.Name}"
            : $"Change the selected Arena row to {name}, {count:N0} total copies";
        if (NativeInAppDialog.Show(this, intent + "?\n\n" +
            "Only native Arena ownership changes. Each successful save keeps the " +
            "entire previous native collection as a recovery point. " +
            "Your old Windows app is not updated.",
            editing && count == 0 ? "Confirm Arena row removal" : "Confirm Arena ownership edit",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        busy = true;
        SaveButton.IsEnabled = false;
        CardNameBox.IsEnabled = false;
        QuantityBox.IsEnabled = false;
        StatusText.Text = "Validating source row and applying a recoverable native edit…";
        try
        {
            var result = await Task.Run(() => selectedOrdinal.HasValue
                ? NativeWorkspaceSafety.EditArenaRow(nativePath,
                    selectedOrdinal.Value, selectedRow!, name, count, expectedSha)
                : NativeWorkspaceSafety.AddArenaCopies(nativePath, name, count, expectedSha));
            NativeInAppDialog.Show(this,
                $"Saved native Arena inventory: {result.Current.Collection.ArenaCopies:N0} owned copies.\n\n" +
                "The complete previous collection was preserved at:\n" +
                result.RecoveryPath +
                "\n\nUse Restore native recovery from the main window to undo.",
                "Native Arena edit saved", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Arena edit not confirmed. Inspect the current native collection " +
                "and recovery files before retrying. " + ex.Message;
            NativeInAppDialog.Show(this, StatusText.Text, "Native edit stopped",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            busy = false;
            SaveButton.IsEnabled = true;
            CardNameBox.IsEnabled = true;
            QuantityBox.IsEnabled = true;
        }
    }
}
