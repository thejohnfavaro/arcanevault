using ArcaneVault.Next.Collection;
using Microsoft.Win32;
using System.Text;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Read-only source and reconciliation audit. A second acknowledgement is
/// required for unresolved/ambiguous printings. Caller must still verify
/// selected file SHA, workspace SHA and candidate SHA at commit.
/// </summary>
public partial class NativeImportDiffWindow : Window
{
    private readonly NativeCollectionReconciliation.Result report;
    private readonly string sourceFilename;
    private readonly string sourceSha256;

    public NativeImportDiffWindow(string filename,
        NativeCollectionReconciliation.Result report, string sourceSha256 = "")
    {
        ArgumentNullException.ThrowIfNull(report);
        this.report = report;
        this.sourceFilename = filename;
        this.sourceSha256 = sourceSha256;
        InitializeComponent();
        Title = "Review " + report.CollectionKind + " collection differences";
        FileNameText.Text = filename +
            (sourceSha256.Length == 0 ? "" : "\nSource SHA-256: " + sourceSha256);
        AuditText.Text = report.Audit.Display;
        SummaryText.Text = report.Summary;
        ChangesTable.ItemsSource = report.Lines
            .OrderBy(x => x.Verification.StartsWith("Unresolved",
                StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x.Delta == 0)
            .ThenBy(x => x.Action)
            .ThenBy(x => x.Card, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Identity, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        UnresolvedAcknowledged.Visibility = report.Audit.UnresolvedRows > 0
            ? Visibility.Visible : Visibility.Collapsed;
        UpdateApply();
        SafetyText.Text = report.HasChanges
            ? "Only the reviewed quantity differences are applied. A verified recovery copy is saved. Export the read-only audit before applying if desired."
            : "All imported targets already match. No write or recovery point is needed; local-only holdings, if any, remain owned.";
    }

    private void UpdateApply() =>
        ApplyButton.IsEnabled = report.HasChanges &&
            (report.Audit.UnresolvedRows == 0 ||
             UnresolvedAcknowledged.IsChecked == true);

    private void OnAcknowledgementChanged(object sender, RoutedEventArgs e)
    {
        if (ApplyButton is not null && UnresolvedAcknowledged is not null)
            UpdateApply();
    }

    private void OnExportAuditClicked(object sender, RoutedEventArgs e)
    {
        var picker = new SaveFileDialog
        {
            Title = "Export read-only collection import audit",
            Filter = "Text audit (*.txt)|*.txt",
            FileName = Path.GetFileNameWithoutExtension(sourceFilename) +
                "-import-audit.txt",
            OverwritePrompt = true
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var content = new StringBuilder();
            content.AppendLine("Arcane Vault import integrity audit — READ ONLY");
            content.AppendLine("This report is NOT a collection import file.");
            content.AppendLine("Source: " + sourceFilename);
            content.AppendLine("Source SHA-256: " +
                (sourceSha256.Length > 0 ? sourceSha256 : "not captured"));
            content.AppendLine("Scope: " + report.CollectionKind);
            content.AppendLine("Proposed candidate SHA-256: " + report.CandidateSha256);
            content.AppendLine();
            content.AppendLine(report.Audit.Display);
            content.AppendLine();
            content.AppendLine(report.Summary);
            content.AppendLine();
            content.AppendLine("Action\tCard\tPrinting / identity\tCurrent\tImported\tDifference\tVerification");
            static string Safe(string s) => s.Replace('\t', ' ')
                .Replace('\r', ' ').Replace('\n', ' ');
            foreach (var line in report.Lines)
                content.AppendLine(string.Join("\t", Safe(line.Action),
                    Safe(line.Card), Safe(line.Identity),
                    line.Current.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    line.Imported.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    line.Delta.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Safe(line.Verification)));
            File.WriteAllText(picker.FileName, content.ToString(), new UTF8Encoding(true));
            SafetyText.Text = "Read-only audit exported: " + picker.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not export the audit. No collection data was changed.\n\n" +
                ex.Message, "Audit export failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        if (ApplyButton.IsEnabled != true) return;
        DialogResult = true;
    }
}
