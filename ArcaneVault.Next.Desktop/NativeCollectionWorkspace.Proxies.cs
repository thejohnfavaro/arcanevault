using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Independently labeled third-party/proxy copies, deliberately not a physical
/// printing or a priced collection row. Uses verified full-backup atomic swaps.
/// </summary>
public partial class NativeCollectionWorkspace
{
    private IReadOnlyList<NativeProxyEditing.ProxyRow> proxyRows =
        Array.Empty<NativeProxyEditing.ProxyRow>();
    private bool proxyBusy;

    private async Task LoadProxyRowsAsync()
    {
        if (ProxyList is null || ProxyStatusText is null) return;
        bool editable = !string.IsNullOrWhiteSpace(nativePath) &&
            !string.IsNullOrWhiteSpace(expectedBackupSha);
        ProxySaveButton.IsEnabled = editable;
        if (!editable)
        {
            ProxyStatusText.Text = "Open a verified native workspace to manage " +
                "non-valued proxy cards. Read-only backups are not modified.";
            return;
        }

        try
        {
            string path = nativePath!;
            var loaded = await Task.Run(() => NativeWorkspaceSafety.ListProxies(path));
            if (closing) return;
            proxyRows = loaded;
            ProxyList.ItemsSource = proxyRows;
            long copies = proxyRows.Sum(x => (long)x.Quantity);
            ProxyStatusText.Text =
                $"{copies:N0} proxy copies across {proxyRows.Count:N0} names. " +
                "Inventory kind: Proxy. Valuation excluded; never price-scraped, " +
                "credited as real owned printings, or included in set progress.";
            ProxyRemoveButton.IsEnabled = false;
        }
        catch (Exception ex) when (ex is InvalidDataException or
            SqliteException or IOException or UnauthorizedAccessException or
            ArgumentException or OverflowException)
        {
            if (!closing)
            {
                ProxySaveButton.IsEnabled = false;
                ProxyRemoveButton.IsEnabled = false;
                ProxyStatusText.Text = "Proxy ledger cannot be verified: " +
                    ex.Message + ". Real collection data was not changed.";
            }
        }
    }

    private void OnProxySelected(object sender, SelectionChangedEventArgs e)
    {
        if (ProxyList is null || ProxyNameBox is null ||
            ProxyQuantityBox is null || ProxyRemoveButton is null) return;
        var selected = ProxyList.SelectedItem as NativeProxyEditing.ProxyRow;
        ProxyRemoveButton.IsEnabled = !proxyBusy && selected is not null &&
            !string.IsNullOrWhiteSpace(expectedBackupSha);
        if (selected is null) return;
        ProxyNameBox.Text = selected.Name;
        ProxyQuantityBox.Text = selected.Quantity.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private async void OnProxySaveClicked(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(ProxyQuantityBox.Text.Trim(), out var quantity) ||
            quantity is < 1 or > 99_999)
        {
            ProxyStatusText.Text = "Enter a positive proxy count between 1 and 99,999. " +
                "Use Remove selected proxy to delete a record.";
            return;
        }
        await SetProxyCountAsync(ProxyNameBox.Text, quantity);
    }

    private async void OnProxyRemoveClicked(object sender, RoutedEventArgs e)
    {
        if (ProxyList?.SelectedItem is not NativeProxyEditing.ProxyRow selected)
            return;
        await SetProxyCountAsync(selected.Name, 0);
    }

    private async Task SetProxyCountAsync(string name, int quantity)
    {
        if (closing || proxyBusy || string.IsNullOrWhiteSpace(nativePath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha)) return;
        proxyBusy = true;
        ProxySaveButton.IsEnabled = false;
        ProxyRemoveButton.IsEnabled = false;
        ProxyStatusText.Text = "Verifying complete backup and updating proxy ledger. " +
            "No real printing or collection price is being changed.";
        try
        {
            string path = nativePath, sha = expectedBackupSha;
            var replacement = await Task.Run(() =>
                NativeWorkspaceSafety.SetProxyQuantity(path, name, quantity, sha));
            if (closing) return;
            expectedBackupSha = replacement.Current.BackupSha256;
            ProxyStatusText.Text = quantity == 0
                ? $"Removed Proxy {name}. Real ownership and prices are unchanged."
                : $"Recorded {quantity:N0} Proxy copies of {name}. " +
                  "Excluded from collection valuation and price lookup.";
            // Rebuild from the verified replacement, just as physical and
            // Arena edits already do. The host preserves selected tab index.
            if (reopenNative is not null)
                await reopenNative();
            else
                await LoadProxyRowsAsync();
        }
        catch (Exception ex) when (ex is InvalidDataException or
            SqliteException or IOException or UnauthorizedAccessException or
            ArgumentException or OverflowException)
        {
            if (!closing)
                ProxyStatusText.Text =
                    "Proxy update not applied: " + ex.Message +
                    ". The active workspace and real collection were preserved.";
        }
        finally
        {
            proxyBusy = false;
            if (!closing)
            {
                ProxySaveButton.IsEnabled = !string.IsNullOrWhiteSpace(nativePath) &&
                    !string.IsNullOrWhiteSpace(expectedBackupSha);
                ProxyRemoveButton.IsEnabled =
                    ProxyList?.SelectedItem is NativeProxyEditing.ProxyRow;
            }
        }
    }
}
