using ArcaneVault.Next.Collection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Explicit, catalog-backed native ownership edit. The entire previous native
/// workspace is retained as an independently verified recovery point.
/// </summary>
public partial class NativeAddPrintingWindow : Window
{
    private readonly string catalogPath;
    private readonly string nativePath;
    private readonly string expectedBackupSha;
    private bool busy;

    public NativeAddPrintingWindow(string catalogPath, string nativePath,
        string expectedBackupSha)
    {
        this.catalogPath = catalogPath;
        this.nativePath = nativePath;
        this.expectedBackupSha = expectedBackupSha;
        InitializeComponent();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; _ = SearchAsync(); }
    }

    private void OnFindClicked(object sender, RoutedEventArgs e) => _ = SearchAsync();

    private async Task SearchAsync()
    {
        if (busy) return;
        busy = true;
        FindButton.IsEnabled = false;
        AddButton.IsEnabled = false;
        PrintingsList.ItemsSource = null;
        try
        {
            string query = QueryBox.Text.Trim();
            var found = await Task.Run(() =>
                NativePhysicalEditing.FindPaperPrintings(catalogPath, query));
            PrintingsList.ItemsSource = found.Rows;
            StatusText.Text = found.HasMore
                ? $"Showing the first {found.Rows.Count} matching paper printings. Narrow the name to find more."
                : $"{found.Rows.Count} matching paper printings. Select the specific set/collector number.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Printing search failed: " + ex.Message;
        }
        finally
        {
            busy = false;
            FindButton.IsEnabled = true;
            AddButton.IsEnabled = true;
        }
    }

    private async void OnAddClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (PrintingsList.SelectedItem is not NativePhysicalEditing.PrintingCandidate selected)
        {
            StatusText.Text = "Choose one exact paper printing from the search results.";
            return;
        }
        if (!int.TryParse(QuantityBox.Text, out int quantity) || quantity is < 1 or > 99_999)
        {
            StatusText.Text = "Quantity must be between 1 and 99,999.";
            return;
        }
        string finish = (FinishBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        if (MessageBox.Show(this,
            $"Add {quantity:N0} {finish} copy/copies of:\n\n" +
            $"{selected.Name}\n{selected.SetCode.ToUpperInvariant()} #{selected.CollectorNumber}\n" +
            $"Printing ID: {selected.PrintId}\n\n" +
            "This changes ONLY the native Windows collection. A full recovery " +
            "copy is retained. The old Arcane Vault Windows app is not updated.",
            "Confirm exact physical printing", MessageBoxButton.YesNo,
            MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        busy = true;
        FindButton.IsEnabled = false;
        AddButton.IsEnabled = false;
        StatusText.Text = "Verifying catalog, staging the edit, and preserving recovery…";
        try
        {
            var result = await Task.Run(() =>
                NativeWorkspaceSafety.AddVerifiedPhysical(
                    nativePath, catalogPath, selected.PrintId,
                    quantity, finish, expectedBackupSha));
            MessageBox.Show(this,
                $"Verified native ownership is now {result.Current.Collection.PhysicalCopies:N0} total paper copies.\n\n" +
                "Previous native collection saved at:\n" + result.RecoveryPath +
                "\n\nUse 'Restore native recovery' in the main window to undo.",
                "Native printing added", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Edit could not be confirmed. Inspect the native collection and recovery files before retrying. " +
                ex.Message;
            MessageBox.Show(this, StatusText.Text, "Native edit not confirmed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            busy = false;
            FindButton.IsEnabled = true;
            AddButton.IsEnabled = true;
        }
    }
}
