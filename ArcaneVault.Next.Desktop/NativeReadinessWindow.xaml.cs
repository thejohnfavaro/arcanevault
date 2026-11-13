using ArcaneVault.Next.Domain;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

public partial class NativeReadinessWindow : Window
{
    private string? lastSuccessfulReport;
    private bool checking;

    public NativeReadinessWindow()
    {
        InitializeComponent();
    }

    private async void OnChooseBackupClicked(object sender, RoutedEventArgs e)
    {
        if (checking) return;
        var picker = new OpenFileDialog
        {
            Title = "Select an exported Arcane Vault full backup",
            Filter = "Full backups (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (picker.ShowDialog(this) != true) return;

        checking = true;
        ChooseBackupButton.IsEnabled = false;
        CopyReadinessButton.IsEnabled = false;
        lastSuccessfulReport = null;
        ReadinessReport.Text = "Checking selected full backup locally (read-only)…";
        try
        {
            var report = await Task.Run(() =>
                NativeReleaseReadiness.Inspect(picker.FileName));
            if (!IsLoaded) return;
            lastSuccessfulReport = report.Display;
            ReadinessReport.Text = lastSuccessfulReport;
            CopyReadinessButton.IsEnabled = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidDataException or ArgumentException or System.Text.Json.JsonException)
        {
            if (!IsLoaded) return;
            ReadinessReport.Text =
                "The selected file could not be validated. No data was changed.\n\n" +
                ex.Message + "\n\nSelect an exported schema-2 full backup, " +
                "not a QR/mobile transfer file or active SQLite workspace.";
        }
        finally
        {
            checking = false;
            if (IsLoaded) ChooseBackupButton.IsEnabled = true;
        }
    }

    private void OnCopyReportClicked(object sender, RoutedEventArgs e)
    {
        if (lastSuccessfulReport is null) return;
        try { Clipboard.SetText(lastSuccessfulReport); }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            ReadinessReport.Text = lastSuccessfulReport +
                "\n\nClipboard unavailable; source file remains unchanged.";
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}