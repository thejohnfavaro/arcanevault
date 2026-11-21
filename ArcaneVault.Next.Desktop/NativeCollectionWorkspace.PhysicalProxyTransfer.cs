using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.IO;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

public partial class NativeCollectionWorkspace
{
    private bool physicalProxyTransferBusy;

    private async void OnMovePhysicalToProxyClicked(object sender, RoutedEventArgs e)
    {
        if (closing || physicalProxyTransferBusy ||
            string.IsNullOrWhiteSpace(nativePath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha) ||
            string.IsNullOrWhiteSpace(catalogPath))
        {
            StatusText.Text = "Open a verified native collection with a local paper catalog.";
            return;
        }
        if (!int.TryParse(PhysicalToProxyCopiesBox.Text.Trim(), out int quantity) ||
            quantity is < 1 or > 99_999)
        {
            StatusText.Text = "Enter a positive number of physical copies per selected printing.";
            return;
        }

        var selected = PhysicalList.SelectedItems
            .Cast<object>().OfType<NativePhysicalValuation.Line>().ToArray();
        if (selected.Length == 0)
        {
            StatusText.Text = "Select one or more confirmed physical printing rows first (Ctrl/Shift-click).";
            return;
        }
        if (selected.Length > 500)
        {
            StatusText.Text = "Move up to 500 selected printing rows in one transaction.";
            return;
        }
        if (selected.Any(x => !x.Source.Confirmed ||
            x.Ordinal < 0 || x.Ordinal >= backup.PhysicalCards.Count ||
            backup.PhysicalCards[x.Ordinal] != x.Source.Original ||
            x.Source.Original.Quantity < quantity))
        {
            StatusText.Text = "Every selected row must be catalog-confirmed, unchanged and " +
                "contain at least the requested number of copies. Nothing was moved.";
            return;
        }

        var moves = selected.Select(x => new NativePhysicalProxyTransfer.Move(
            x.Ordinal, x.Source.Original, quantity)).ToArray();
        var grouped = selected.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new { Name = x.Key, Copies = x.Count() * quantity })
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        int totalCopies = moves.Length * quantity;
        string details = string.Join("\n", grouped.Take(20)
            .Select(x => $"• {x.Copies:N0} {x.Name}"));
        if (grouped.Length > 20)
            details += $"\n…and {grouped.Length - 20:N0} more card names.";
        if (NativeInAppDialog.Show(this,
            $"Transfer {totalCopies:N0} confirmed physical copies from " +
            $"{moves.Length:N0} printing rows into the unpriced Proxy ledger?\n\n" +
            details + "\n\nThis DECREASES actual owned copies and physical set progress/value. " +
            "The same number of proxies is ADDED by name; existing proxy counts are " +
            "incremented. Arena inventory, deck lists and match records stay intact. " +
            "The full prior workspace is kept as a recovery point.\n\nApply this transfer?",
            "Confirm physical → Proxy transfer", MessageBoxButton.YesNo,
            MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        physicalProxyTransferBusy = true;
        MovePhysicalToProxyButton.IsEnabled = false;
        StatusText.Text = "Verifying physical printings and transferring to Proxy with full rollback…";
        try
        {
            string path = nativePath, sha = expectedBackupSha, catalog = catalogPath;
            var result = await Task.Run(() =>
                NativeWorkspaceSafety.TransferPhysicalToProxy(
                    path, catalog, moves, sha));
            if (closing) return;
            NativeInAppDialog.Show(this,
                $"Transferred {totalCopies:N0} physical copies to Proxy. " +
                "The original physical inventory and all other data are retained " +
                "in the recovery workspace:\n" + result.RecoveryPath,
                "Physical → Proxy transfer complete", MessageBoxButton.OK,
                MessageBoxImage.Information);
            if (!await TryRefreshVerifiedPhysicalChangeAsync(result, transferOnly: true))
            {
                Close();
                if (reopenNative is not null) await reopenNative();
            }
        }
        catch (Exception ex)
        {
            if (!closing)
            {
                StatusText.Text = "Physical-to-proxy transfer not completed: " + ex.Message;
                NativeInAppDialog.Show(this,
                    "Transfer could not be verified. No intentional partial transfer " +
                    "was applied.\n\n" + ex.Message, "Transfer stopped",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            physicalProxyTransferBusy = false;
            if (!closing)
                MovePhysicalToProxyButton.IsEnabled =
                    !string.IsNullOrWhiteSpace(nativePath) &&
                    !string.IsNullOrWhiteSpace(expectedBackupSha) &&
                    !string.IsNullOrWhiteSpace(catalogPath);
        }
    }
}
