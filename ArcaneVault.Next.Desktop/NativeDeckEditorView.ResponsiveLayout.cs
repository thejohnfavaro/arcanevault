using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

public partial class NativeDeckEditorView
{
    // Compact EDH layout: the *same* card inspector moves into a Preview
    // tab. No duplicate visual tree, selection, card artwork request, or
    // re-created data model. Do not use the containing window width: the
    // sidebar and splitters determine the actual available editor viewport.
    private const double CompactPreviewThreshold = 880;
    private const double WidePreviewThreshold = 935;
    private bool previewDockedInTab;
    private bool movingPreview;

    private void OnEdhEditorRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (EdhEditorRoot is null || AnalyticsPanel is null) return;
        double height = EdhEditorRoot.ActualHeight;
        if (!double.IsFinite(height) || height <= 0) return;

        // Expanded analytics must not consume most of a short embedded
        // editor, making the authoritative mainboard and preview unusable.
        // Its existing internal chart ScrollViewer remains scrollable.
        AnalyticsPanel.MaxHeight = Math.Clamp(height * 0.23, 92, 280);
        ConstrainEdhWorkspaceScrollers();
    }

    private void OnEdhEditorColumnsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (movingPreview || EdhEditorColumns is null ||
            EdhPreviewTab is null || CardPreviewPanel is null ||
            PreviewColumn is null || PreviewSplitterColumn is null ||
            PreviewGridSplitter is null) return;

        double available = EdhEditorColumns.ActualWidth;
        if (!double.IsFinite(available) || available <= 0) return;

        // Hysteresis prevents oscillating between modes on a boundary when
        // a splitter and the tab strip change layout measurements.
        if (!previewDockedInTab && available < CompactPreviewThreshold)
            SetCompactPreview(true);
        else if (previewDockedInTab && available > WidePreviewThreshold)
            SetCompactPreview(false);
    }

    private void SetCompactPreview(bool compact)
    {
        if (movingPreview || previewDockedInTab == compact ||
            EdhEditorColumns is null || CardPreviewPanel is null ||
            EdhPreviewTab is null || PreviewColumn is null ||
            PreviewSplitterColumn is null || PreviewGridSplitter is null)
            return;

        movingPreview = true;
        try
        {
            if (compact)
            {
                // WPF visuals may have only one logical/visual parent.
                // Detach first, then dock exactly that existing preview.
                EdhEditorColumns.Children.Remove(CardPreviewPanel);
                PreviewColumn.MinWidth = 0;
                PreviewColumn.Width = new GridLength(0);
                PreviewSplitterColumn.Width = new GridLength(0);
                PreviewGridSplitter.Visibility = Visibility.Collapsed;
                EdhPreviewTab.Content = CardPreviewPanel;
                EdhPreviewTab.Visibility = Visibility.Visible;
                DeckEditorIntro.Visibility = Visibility.Collapsed;
                previewDockedInTab = true;
            }
            else
            {
                if (ReferenceEquals(EdhWorkspaceTabs.SelectedItem, EdhPreviewTab))
                    EdhWorkspaceTabs.SelectedItem = EdhCardsTab;
                EdhPreviewTab.Content = null;
                CardPreviewPanel.SetValue(Grid.ColumnProperty, 4);
                EdhEditorColumns.Children.Add(CardPreviewPanel);
                PreviewColumn.MinWidth = 220;
                PreviewColumn.Width = new GridLength(1.8, GridUnitType.Star);
                PreviewSplitterColumn.Width = new GridLength(10);
                PreviewGridSplitter.Visibility = Visibility.Visible;
                EdhPreviewTab.Visibility = Visibility.Collapsed;
                DeckEditorIntro.Visibility = Visibility.Visible;
                previewDockedInTab = false;
            }
            ConstrainEdhWorkspaceScrollers();
        }
        finally { movingPreview = false; }
    }
}
