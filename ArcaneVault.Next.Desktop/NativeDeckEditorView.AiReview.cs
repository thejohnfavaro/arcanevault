using ArcaneVault.Next.Collection;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Read-only AI advice in the existing EDH Lab workspace. Explicit opt-in:
/// only the exact prompt in the preview is ever sent or copied.
/// </summary>
public partial class NativeDeckEditorView
{
    private string aiLastResponse = "";
    private bool aiReviewRunning;
    private MainWindow? AiOwner => Window.GetWindow(this) as MainWindow;

    public void OpenAiReview()
    {
        if (AiReviewWideSurface is null || EdhEditorColumns is null) return;
        EdhEditorColumns.Visibility = Visibility.Collapsed;
        AiReviewWideSurface.Visibility = Visibility.Visible;
        RefreshAiReviewPrompt();
    }

    private void OnAiReviewCurrentDeckClicked(object sender, RoutedEventArgs e) => OpenAiReview();

    private void OnAiReturnToEditorClicked(object sender, RoutedEventArgs e)
    {
        AiReviewWideSurface.Visibility = Visibility.Collapsed;
        EdhEditorColumns.Visibility = Visibility.Visible;
    }

    private string CurrentReviewDeck()
    {
        var board = MainboardBox?.Text.Trim() ?? "";
        if (board.Length == 0) return "";
        var result = new StringBuilder();
        result.AppendLine("Format: " + FormatBox?.Text.Trim());
        if (!string.IsNullOrWhiteSpace(CommanderBox?.Text))
        {
            result.AppendLine("Commander");
            result.AppendLine("1 " + CommanderBox.Text.Trim());
        }
        result.AppendLine("Deck");
        result.AppendLine(board);
        return result.ToString();
    }

    private string? ReviewCoverage()
    {
        if (MainboardBox is null || CommanderBox is null || FormatBox is null) return null;
        try
        {
            var pool = OwnedPoolBox?.SelectedIndex == 1
                ? NativeDeckOwnedPicker.Pool.Arena : NativeDeckOwnedPicker.Pool.Physical;
            var summary = NativeDeckOwnedPicker.Review(
                ownership, pool, MainboardBox.Text, CommanderBox.Text, FormatBox.Text, proxyLedger);
            var lines = new StringBuilder();
            lines.AppendLine("Inventory: " + pool);
            lines.AppendLine(summary.Header);
            foreach (var card in summary.Shortages.Take(100))
                lines.AppendLine(card.Name + ": needed " + card.Required +
                    ", confirmed owned " + card.Owned +
                    ", proxy " + card.Proxy + ", still missing " + card.Missing);
            return lines.ToString();
        }
        catch (Exception)
        {
            // The deck can still be reviewed without ownership metadata.
            return null;
        }
    }

    private void RefreshAiReviewPrompt()
    {
        if (AiReviewPromptPreview is null || AiIncludeOwnershipBox is null ||
            AiReviewModeBox is null || AiAnalyzeButton is null) return;
        var deck = CurrentReviewDeck();
        var mode = (AiReviewModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "health";
        if (AiIncludeOwnershipBox.IsChecked == true)
        {
            var coverage = ReviewCoverage();
            if (!string.IsNullOrWhiteSpace(coverage))
                deck += "\n\nDECK-ONLY OWNERSHIP COVERAGE (opted in)\n" + coverage;
        }
        AiReviewPromptPreview.Text = AiDeckReviewPrompt.Build(deck, mode);
        var connected = AiOwner?.AiClient.IsConnected == true;
        AiAnalyzeButton.IsEnabled = !aiReviewRunning && connected &&
            !string.IsNullOrWhiteSpace(CurrentReviewDeck());
        if (AiAccountStatus is not null)
            AiAccountStatus.Text = AiOwner?.AiClient.Status ?? "Not connected to ChatGPT";
    }

    private void OnAiReviewOptionsChanged(object sender, RoutedEventArgs e)
    {
        if (!aiReviewRunning) RefreshAiReviewPrompt();
    }

    private async void OnAiAnalyzeClicked(object sender, RoutedEventArgs e)
    {
        var client = AiOwner?.AiClient;
        if (client?.IsConnected != true || aiReviewRunning)
        {
            AiReviewStatus.Text = "Connect ChatGPT from Options before analyzing.";
            return;
        }
        RefreshAiReviewPrompt(); // Snapshot the latest unsaved draft and selected options.
        if (string.IsNullOrWhiteSpace(CurrentReviewDeck())) return;
        var prompt = AiReviewPromptPreview.Text;
        aiReviewRunning = true;
        AiAnalyzeButton.IsEnabled = false;
        AiReviewStatus.Text = "Analyzing the previewed deck…";
        try
        {
            var (model, answer) = await client.ReviewAsync(prompt);
            aiLastResponse = answer;
            var document = AiReviewMarkdown.Render(answer);
            if (!string.IsNullOrWhiteSpace(catalogPath))
            {
                // Catalog validation happens off the UI thread. The AI can
                // suggest unknown card names, but only indexed cards are links.
                string path = catalogPath!;
                var draft = MainboardBox?.Text ?? "";
                var commander = CommanderBox?.Text ?? "";
                var confirmed = await Task.Run(() =>
                {
                    var names = new List<string>();
                    foreach (var candidate in AiReviewCardLinks.Candidates(answer, draft, commander))
                    {
                        try
                        {
                            var details = CollectionCatalogReview.ReadDetails(path, candidate);
                            if (details is not null) names.Add(details.Name);
                        }
                        catch (Exception) { /* Catalog may be refreshing. */ }
                    }
                    return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                });
                AiReviewCardLinks.Apply(document, confirmed, OpenAiReviewCardDetails);
            }
            AiReviewResultViewer.Document = document;
            AiReviewStatus.Text = "Review complete (" + model + "). Suggestions are read-only; no deck edits were made.";
        }
        catch (Exception ex)
        {
            AiReviewStatus.Text = "Analysis unavailable: " + ex.Message;
        }
        finally
        {
            aiReviewRunning = false;
            RefreshAiReviewPrompt();
        }
    }

    private async void OpenAiReviewCardDetails(string cardName)
    {
        if (string.IsNullOrWhiteSpace(catalogPath)) return;
        try
        {
            var detail = await Task.Run(() =>
                CollectionCatalogReview.ReadDetails(catalogPath!, cardName));
            if (detail is null)
            {
                AiReviewStatus.Text = "Card not found in the local catalog: " + cardName;
                return;
            }
            // Reuse the full native card screen. No deck or inventory writes.
            var window = new NativeCardDetailsWindow(detail.Name,
                "AI Review recommendation · read-only card details",
                detail, catalogPath: catalogPath)
            {
                Owner = Window.GetWindow(this)
            };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            AiReviewStatus.Text = "Card preview unavailable: " + ex.Message;
        }
    }

    private void OnAiCopyPromptClicked(object sender, RoutedEventArgs e)
    {
        RefreshAiReviewPrompt();
        if (string.IsNullOrWhiteSpace(CurrentReviewDeck())) return;
        try
        {
            Clipboard.SetText(AiReviewPromptPreview.Text);
            AiReviewStatus.Text = "Prompt copied. You can paste it into ChatGPT manually.";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            AiReviewStatus.Text = "Clipboard busy. Try copying again.";
        }
    }

    private void OnAiCopyReviewClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(aiLastResponse))
        {
            AiReviewStatus.Text = "No review to copy yet.";
            return;
        }
        try
        {
            Clipboard.SetText(aiLastResponse);
            AiReviewStatus.Text = "Review copied as Markdown.";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            AiReviewStatus.Text = "Clipboard busy. Try copying again.";
        }
    }
}
