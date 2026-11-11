using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Edit the quantity of one selected native physical line by its backup
/// ordinal, complete row snapshot, and expected workspace checksum.
/// </summary>
public partial class NativeAdjustPhysicalWindow : Window
{
    private readonly string nativePath;
    private readonly string expectedSha;
    private readonly NativePhysicalRow original;
    private readonly int ordinal;
    private bool busy;

    public NativeAdjustPhysicalWindow(string nativePath,
        string expectedSha, int physicalOrdinal, NativePhysicalRow original)
    {
        this.nativePath = nativePath;
        this.expectedSha = expectedSha;
        ordinal = physicalOrdinal;
        this.original = original;
        InitializeComponent();
        NewQuantityBox.Text = original.Quantity.ToString();
        SelectedCardText.Text =
            $"{original.Name}\n{original.SetAndNumber} · {original.Finish} · " +
            $"Current: {original.Quantity:N0} copies\n" +
            $"Printing status: {original.PrintingStatus}";
    }

    private void OnSetZeroClicked(object sender, RoutedEventArgs e) =>
        NewQuantityBox.Text = "0";

    private async void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (!int.TryParse(NewQuantityBox.Text.Trim(), out int quantity) ||
            quantity is < 0 or > 99_999 || quantity == original.Quantity)
        {
            StatusText.Text =
                "Enter a different quantity from 0 through 99,999. " +
                "Zero removes only the selected row.";
            return;
        }
        if (quantity == 0 && ConfirmRemoveBox.Text != "REMOVE")
        {
            StatusText.Text = "To remove the selected line completely, type REMOVE exactly.";
            return;
        }
        string change = quantity == 0
            ? "REMOVE this exact ownership row"
            : $"change this row from {original.Quantity:N0} to {quantity:N0} copies";
        if (MessageBox.Show(this,
            $"Confirm: {change}?\n\n" +
            $"{original.Name}\n{original.SetAndNumber} · {original.Finish}\n" +
            $"Printing ID: {(original.ScryfallId.Length == 0 ? "unresolved" : original.ScryfallId)}\n\n" +
            "Only the selected physical row is changed. The previous complete " +
            "native collection will be saved as a separate recovery point. " +
            "The installed Windows app is NOT updated.",
            quantity == 0 ? "Confirm physical row removal" : "Confirm quantity change",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        busy = true;
        ApplyButton.IsEnabled = false;
        StatusText.Text = "Checking the selected row and writing a recoverable staged replacement…";
        try
        {
            var result = await Task.Run(() =>
                NativeWorkspaceSafety.SetPhysicalQuantity(nativePath, ordinal,
                    original, quantity, expectedSha));
            MessageBox.Show(this,
                $"Native paper collection now has {result.Current.Collection.PhysicalCopies:N0} copies.\n\n" +
                "Previous full native collection preserved at:\n" + result.RecoveryPath +
                "\n\nUse Restore native recovery on the main window to reverse this edit.",
                "Verified native quantity change", MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Edit not confirmed; inspect the workspace and recovery files before retrying. " + ex.Message;
            MessageBox.Show(this, StatusText.Text,
                "Native quantity edit not confirmed", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            busy = false;
            ApplyButton.IsEnabled = true;
        }
    }
}
