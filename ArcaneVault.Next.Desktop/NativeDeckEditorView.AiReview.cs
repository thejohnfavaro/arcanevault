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
    private CancellationTokenSource? aiReviewCancellation;
    private MainWindow? AiOwner => Window.GetWindow(this) as MainWindow;

    public void OpenAiReview()
    {
        if (AiReviewWideSurface is null || EdhEditorColumns is null) return;
        EdhEditorColumns.Visibility = Visibility.Collapsed;
        AiReviewWideSurface.Visibility = Visibility.Visible;
        RefreshAiReviewPrompt();
    }

    private void OnComputeCommanderBracketClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var format = FormatBox?.Text ?? "";
            if (!format.Contains("Commander", StringComparison.OrdinalIgnoreCase) ||
                format.Contains("Brawl", StringComparison.OrdinalIgnoreCase))
            {
                AiReviewStatus.Text = "Commander Brackets apply to tabletop Commander, not Arena Brawl.";
                return;
            }
            var cards = NativeDeckEditing.ParseMainboard(MainboardBox?.Text ?? "")
                .Select(c => c.Name).ToArray();
            if (cards.Length == 0)
            {
                AiReviewStatus.Text = "Add cards to your deck before assessing its bracket.";
                return;
            }
            var assessment = ArcaneVault.Next.Domain.CommanderBracketAssessment.Evaluate(
                cards, CommanderBox?.Text);
            var report = new StringBuilder();
            report.AppendLine("## Commander Bracket — preliminary local assessment");
            report.AppendLine();
            report.AppendLine("**Rules-backed minimum: Bracket " +
                assessment.MinimumBracket + "**");
            report.AppendLine();
            report.AppendLine("Game Changers: **" + assessment.GameChangerCount + "**");
            if (assessment.MatchedCards.Count > 0)
                foreach (var name in assessment.MatchedCards)
                    report.AppendLine("- " + name);
            report.AppendLine();
            report.AppendLine(assessment.Interpretation);
            report.AppendLine();
            report.AppendLine("### Still requires assessment");
            foreach (var item in assessment.Unknowns)
                report.AppendLine("- " + item);
            report.AppendLine();
            report.AppendLine("Rules: " + assessment.RulesRevision);
            report.AppendLine("This is not a final bracket rating. No deck changes or AI requests were made.");
            AiReviewResultViewer.Document = AiReviewMarkdown.Render(report.ToString());
            AiReviewStatus.Text = "Local bracket check complete (no AI or network). " +
                "This is a floor, not a final bracket.";
        }
        catch (Exception ex)
        {
            AiReviewStatus.Text = "Bracket check unavailable: " + ex.Message;
        }
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

    private void OnAiCancelClicked(object sender, RoutedEventArgs e)
    {
        aiReviewCancellation?.Cancel();
        AiReviewStatus.Text = "Stopping the review request…";
    }

    private async void OnAiAnalyzeClicked(object sender, RoutedEventArgs e)
    {
        var client = AiOwner?.AiClient;
        if (client?.IsConnected != true || aiReviewRunning)
        {
            AiReviewStatus.Text = "Connect ChatGPT from Options before analyzing.";
            return;
        }
        RefreshAiReviewPrompt();
        if (string.IsNullOrWhiteSpace(CurrentReviewDeck()))
        {
            AiReviewStatus.Text = "Add at least one card to the current deck.";
            return;
        }
        var prompt = AiReviewPromptPreview.Text;
        aiReviewRunning = true;
        AiAnalyzeButton.IsEnabled = false;
        aiReviewCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        if (AiCancelReviewButton is not null)
            AiCancelReviewButton.IsEnabled = true;
        AiReviewStatus.Text = "Starting request…";
        var received = "";
        // Render partial text while the network is still streaming, rather
        // than waiting for hundreds of unrelated catalog card lookups.
        var updates = new Progress<(string Stage, string Text)>(update =>
        {
            AiReviewStatus.Text = update.Stage;
            if (update.Text.Length > 0 && update.Text != received)
            {
                received = update.Text;
                AiReviewResultViewer.Document = AiReviewMarkdown.Render(received);
            }
        });
        try
        {
            var (model, answer) = await client.ReviewAsync(
                prompt, aiReviewCancellation.Token, updates);
            aiLastResponse = answer;
            AiReviewResultViewer.Document = AiReviewMarkdown.Render(answer);
            AiReviewStatus.Text = "Review received (" + model +
                "). Identifying clickable cards in the local catalog…";

            // Card linking is an enhancement, not a prerequisite for showing
            // the result. Slow/unavailable catalog data cannot hide an answer.
            if (!string.IsNullOrWhiteSpace(catalogPath))
            {
                string path = catalogPath!;
                var draft = MainboardBox?.Text ?? "";
                var commander = CommanderBox?.Text ?? "";
                using var lookupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                try
                {
                    // Discover catalog-backed mentions across the complete
                    // response, including plain prose such as "The One Ring".
                    // The previous bold/table/mainboard candidate list omitted
                    // ordinary card names that were not already in the deck.
                    // Resolve full names and context-sensitive nicknames
                    // against the actual local catalog. An ambiguous nickname
                    // cannot silently navigate to the wrong card.
                    var explicitMentions = string.Join("\n",
                        AiReviewCardLinks.ExplicitNames(answer));
                    var resolved = await Task.Run(() =>
                    {
                        var exact = CollectionCatalogReview.FindMentionedNames(
                            path, explicitMentions, lookupTimeout.Token);
                        var deckNames = NativeDeckEditing.ParseMainboard(draft)
                            .Select(entry => ArcaneVault.Next.Domain.ArenaDeckCardName.Identity(entry.Name))
                            .Append(ArcaneVault.Next.Domain.ArenaDeckCardName.Identity(commander))
                            .Where(name => !string.IsNullOrWhiteSpace(name));
                        var aliases = CollectionCatalogReview.FindShortNameLinks(
                            path, explicitMentions, deckNames, lookupTimeout.Token);
                        return (Exact: exact, Aliases: aliases);
                    }, lookupTimeout.Token).WaitAsync(lookupTimeout.Token);
                    var confirmed = resolved.Exact;
                    var nicknameMap = resolved.Aliases;
                    var visibleNames = confirmed.Concat(nicknameMap.Keys)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    // Build a NEW WPF document; each alias carries its
                    // canonical target when clicked (e.g. Tezzeret).
                    AiReviewResultViewer.Document = AiReviewMarkdown.Render(
                        answer, visibleNames, name =>
                        {
                            OpenAiReviewCardDetails(
                                nicknameMap.TryGetValue(name, out var canonical)
                                    ? canonical : name);
                        });
                    AiReviewStatus.Text = visibleNames.Length > 0
                        ? "Review complete (" + model + "). " + visibleNames.Length +
                          " explicitly tagged card references verified — click gold names for full details."
                        : "Review complete (" + model +
                          "). No mentioned card names matched the local catalog. " +
                          "Check that Card Catalog has finished refreshing.";
                }
                catch (OperationCanceledException)
                {
                    // The complete review remains readable without links.
                    AiReviewStatus.Text = "Review complete. Card links are unavailable because catalog lookup timed out.";
                    return;
                }
                catch (Exception ex)
                {
                    AiReviewStatus.Text = "Review complete, but card links could not be prepared (" +
                        ex.GetType().Name + ": " +
                        ex.Message[..Math.Min(ex.Message.Length, 150)] +
                        "). The review is still readable.";
                    return;
                }
            }
            if (string.IsNullOrWhiteSpace(catalogPath))
                AiReviewStatus.Text = "Review complete (" + model +
                    "). Card previews require the local Scryfall catalog.";
        }
        catch (OperationCanceledException)
        {
            AiReviewStatus.Text = "Review cancelled or timed out. " +
                "Any received text remains visible; no deck changes were made.";
        }
        catch (Exception ex)
        {
            AiReviewStatus.Text = "Analysis unavailable: " + ex.Message +
                (received.Length > 0 ? " Partial text is shown below." : "");
            if (received.Length == 0)
                AiReviewResultViewer.Document = AiReviewMarkdown.Render(
                    "## Review unavailable\n" +
                    "The request did not return a complete review. " +
                    "See the status above for the failure stage and reason.");
        }
        finally
        {
            aiReviewRunning = false;
            aiReviewCancellation?.Dispose();
            aiReviewCancellation = null;
            if (AiCancelReviewButton is not null)
                AiCancelReviewButton.IsEnabled = false;
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
