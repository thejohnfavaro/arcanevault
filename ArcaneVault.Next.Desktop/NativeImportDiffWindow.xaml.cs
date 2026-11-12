using ArcaneVault.Next.Collection;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Read-only reconciliation table. An explicit Apply action merely confirms
/// the already-computed diff; the caller must reverify the source backup hash
/// and proposed candidate digest inside the native transaction lock.
/// </summary>
public partial class NativeImportDiffWindow : Window
{
    public NativeImportDiffWindow(string filename,
        NativeCollectionReconciliation.Result report)
    {
        ArgumentNullException.ThrowIfNull(report);
        InitializeComponent();
        Title = "Review " + report.CollectionKind + " collection differences";
        FileNameText.Text = filename;
        SummaryText.Text = report.Summary;
        ChangesTable.ItemsSource = report.Lines.OrderBy(x => x.Delta == 0)
            .ThenBy(x => x.Action)
            .ThenBy(x => x.Card, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Identity, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        ApplyButton.IsEnabled = report.HasChanges;
        SafetyText.Text = report.HasChanges
            ? "Applying creates a full native recovery point. Unrelated Arena, physical, deck and artwork records remain unchanged."
            : "All imported values already match. Nothing to apply; no backup or write is needed.";
    }

    private void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
