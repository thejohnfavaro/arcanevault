using ArcaneVault.Next.Collection;
using System.IO;
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
    private string aiLastModel = "";
    private string aiLastMode = "";
    private string aiReviewDeckFingerprint = "";
    private bool aiReviewRunning;
    private CancellationTokenSource? aiReviewCancellation;
    private MainWindow? AiOwner => Window.GetWindow(this) as MainWindow;

    public void OpenAiReview()
    {
        if (AiReviewWideSurface is null || EdhEditorColumns is null) return;
        EdhEditorColumns.Visibility = Visibility.Collapsed;
        AiReviewWideSurface.Visibility = Visibility.Visible;
        RefreshAiReviewPrompt();
        // Reopen reviews stored in the native deck backup; keep them available
        // offline even without a connected ChatGPT session.
        if (aiLastResponse.Length == 0 && original?.HasAiReview == true)
        {
            aiLastResponse = original.AiReviewMarkdown;
            aiLastModel = original.AiReviewModel;
            aiLastMode = original.AiReviewMode;
            aiReviewDeckFingerprint = original.AiReviewStale ? "" :
                NativeDeckEditing.ReviewFingerprint(original.Format, original.Commander,
                    NativeDeckEditing.ParseMainboard(original.MainboardText));
            AiReviewResultViewer.Document = AiReviewMarkdown.Render(aiLastResponse);
            _ = RestoreSavedReviewCardLinksAsync();
        }
        RefreshSavedReviewStatus();
    }

    private async Task RestoreSavedReviewCardLinksAsync()
    {
        if (string.IsNullOrWhiteSpace(catalogPath) || aiLastResponse.Length == 0)
            return;
        string markdown = aiLastResponse, path = catalogPath;
        try
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var resolved = await Task.Run(() =>
            {
                string mentions = string.Join("\n", AiReviewCardLinks.ExplicitNames(markdown));
                var exact = CollectionCatalogReview.FindMentionedNames(
                    path, mentions, budget.Token);
                var deckNames = NativeDeckEditing.ParseMainboard(original?.MainboardText ?? "")
                    .Select(entry => ArcaneVault.Next.Domain.ArenaDeckCardName.Identity(entry.Name))
                    .Append(ArcaneVault.Next.Domain.ArenaDeckCardName.Identity(
                        original?.Commander ?? ""));
                var aliases = CollectionCatalogReview.FindShortNameLinks(
                    path, mentions, deckNames, budget.Token);
                return (exact, aliases);
            }, budget.Token).WaitAsync(budget.Token);
            if (aiLastResponse != markdown) return;
            var names = resolved.exact.Concat(resolved.aliases.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            AiReviewResultViewer.Document = AiReviewMarkdown.Render(markdown, names,
                name => OpenAiReviewCardDetails(
                    resolved.aliases.TryGetValue(name, out var fullName) ? fullName : name));
        }
        catch (Exception ex) when (ex is OperationCanceledException or
            IOException or InvalidDataException or Microsoft.Data.Sqlite.SqliteException)
        {
            // Stored Markdown remains readable when the local catalog is missing.
        }
    }

    private void RefreshSavedReviewStatus()
    {
        if (AiSaveReviewButton is null || AiReviewStatus is null) return;
        bool matching = false;
        try
        {
            matching = original is not null &&
                NativeDeckEditing.ReviewFingerprint(FormatBox.Text,
                    CommanderBox.Text, NativeDeckEditing.ParseMainboard(MainboardBox.Text)) ==
                NativeDeckEditing.ReviewFingerprint(original.Format,
                    original.Commander, NativeDeckEditing.ParseMainboard(original.MainboardText));
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            // Invalid in-progress drafts are still editable, just not savable reviews.
        }
        bool currentReview = matching && aiLastResponse.Length > 0 &&
            original is not null && aiLastModel.Length > 0 &&
            aiReviewDeckFingerprint == NativeDeckEditing.ReviewFingerprint(
                original.Format, original.Commander,
                NativeDeckEditing.ParseMainboard(original.MainboardText));
        AiSaveReviewButton.IsEnabled = !aiReviewRunning && !busy &&
            original is not null && currentReview &&
            aiLastResponse != original.AiReviewMarkdown;
        if (original?.HasAiReview == true && aiLastResponse == original.AiReviewMarkdown)
            AiReviewStatus.Text = original.AiReviewStale || !matching
                ? "Saved AI review is OUTDATED for this deck. Analyze the current deck again."
                : "Saved AI review · " + original.AiReviewModel + " · " +
                  original.AiReviewSavedUtc + ". This is advisory, not a deck edit.";
        else if (original is null && aiLastResponse.Length > 0)
            AiReviewStatus.Text = "Review received. Save the deck first to persist its review.";
        else if (original is not null && aiLastResponse.Length > 0 && !currentReview)
            AiReviewStatus.Text = "Review refers to an earlier draft. Analyze the current saved deck before persisting.";
    }

    private async void OnAiSaveReviewClicked(object sender, RoutedEventArgs e)
    {
        if (original is null || aiReviewRunning || busy ||
            aiLastResponse.Length == 0 || aiLastModel.Length == 0) return;
        try
        {
            var fingerprint = NativeDeckEditing.ReviewFingerprint(
                FormatBox.Text, CommanderBox.Text,
                NativeDeckEditing.ParseMainboard(MainboardBox.Text));
            var savedFingerprint = NativeDeckEditing.ReviewFingerprint(
                original.Format, original.Commander,
                NativeDeckEditing.ParseMainboard(original.MainboardText));
            if (fingerprint != savedFingerprint ||
                fingerprint != aiReviewDeckFingerprint)
                throw new InvalidDataException(
                    "The draft changed. Save the deck and analyze it again before persisting its review.");
            AiSaveReviewButton.IsEnabled = false;
            AiReviewStatus.Text = "Saving review with verified deck metadata and recovery backup…";
            var replacement = await Task.Run(() =>
                NativeWorkspaceSafety.SaveDeckAiReview(nativePath, original,
                    aiLastResponse, aiLastModel, aiLastMode, expectedSha));
            SavedReplacement = replacement;
            AiReviewStatus.Text = "AI review saved with the deck. " +
                "It will be marked outdated when card contents change.";
            Saved?.Invoke(this, replacement);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or
            IOException or UnauthorizedAccessException or
            Microsoft.Data.Sqlite.SqliteException)
        {
            AiReviewStatus.Text = "Review was not saved: " + ex.Message;
            RefreshSavedReviewStatus();
        }
        finally
        {
            if (AiSaveReviewButton is not null) AiSaveReviewButton.IsEnabled = false;
        }
    }

    private void OnComputeCommanderBracketClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var format = FormatBox?.Text?.Trim() ?? "";
            // Arena decks can be inspected as hypothetical paper Commander
            // lists without changing their saved format or assigning them a bracket.
            bool paperCommander = format.Equals("Commander", StringComparison.OrdinalIgnoreCase);
            bool arenaDeck = format.Contains("Arena", StringComparison.OrdinalIgnoreCase) ||
                format.Contains("Brawl", StringComparison.OrdinalIgnoreCase);
            var entries = NativeDeckEditing.ParseMainboard(MainboardBox?.Text ?? "");
            var cards = entries.Select(c => c.Name).ToArray();
            if (cards.Length == 0)
            {
                AiReviewStatus.Text = "Add cards to your deck before assessing its bracket.";
                return;
            }
            var assessment = ArcaneVault.Next.Domain.CommanderBracketAssessment.Evaluate(
                cards, CommanderBox?.Text);
            var report = new StringBuilder();
            report.AppendLine(paperCommander
                ? "## Commander Bracket — preliminary local assessment"
                : "## Hypothetical paper Commander bracket — preliminary assessment");
            report.AppendLine();
            if (!paperCommander)
            {
                report.AppendLine("**Simulation only:** This " +
                    (arenaDeck ? "Arena" : "non-Commander") +
                    " deck has no official Commander bracket. This check only screens the current cards " +
                    "for paper Commander bracket indicators; it does not convert the deck, validate " +
                    "paper legality, or write any bracket to its saved record.");
                report.AppendLine();
                if (string.IsNullOrWhiteSpace(CommanderBox?.Text))
                    report.AppendLine("**No commander selected:** Choose a paper-legal commander " +
                        "and construct a valid 100-card singleton list before treating this as a " +
                        "paper Commander deck. The result below is card-pool analysis only.");
                else
                    report.AppendLine("**Paper conversion required:** Check commander eligibility, " +
                        "color identity, card legality, singleton rules and deck size before " +
                        "using this deck at a tabletop Commander game.");
                report.AppendLine();
            }
            report.AppendLine("**Rules-backed minimum based on checked cards: Bracket " +
                assessment.MinimumBracket + "**");
            report.AppendLine();
            report.AppendLine("Game Changers: **" + assessment.GameChangerCount + "**");
            if (assessment.MatchedCards.Count > 0)
                foreach (var name in assessment.MatchedCards)
                    report.AppendLine("- " + name);
            report.AppendLine();
            report.AppendLine(assessment.Interpretation);
            report.AppendLine();
            report.AppendLine("### Deck construction indicators (advisory)");
            report.AppendLine("These card-name matches flag areas to discuss, not verified combo lines or automatic bracket increases.");
            report.AppendLine();
            void Evidence(string heading, IReadOnlyList<string> cards)
            {
                report.AppendLine("**" + heading + ": " + cards.Count + "**");
                report.AppendLine(cards.Count == 0 ? "- None of the named reference cards detected" :
                    "- " + string.Join(", ", cards));
                report.AppendLine();
            }
            Evidence("Fast mana", assessment.FastManaIndicators);
            Evidence("Efficient tutors", assessment.TutorIndicators);
            Evidence("Resource denial / lock pieces", assessment.ResourceDenialIndicators);
            report.AppendLine("**Potential combo packages: " + assessment.PotentialEngines.Count + "**");
            if (assessment.PotentialEngines.Count == 0)
                report.AppendLine("- No named reference packages detected; this does not prove combo absence.");
            else
                foreach (string engine in assessment.PotentialEngines)
                    report.AppendLine("- " + engine);
            report.AppendLine();
            report.AppendLine("### Still requires assessment");
            foreach (var item in assessment.Unknowns)
                report.AppendLine("- " + item);
            report.AppendLine();
            report.AppendLine("Rules: " + assessment.RulesRevision);
            report.AppendLine("This is not a final bracket rating. No deck changes or AI requests were made.");
            aiLastResponse = report.ToString();
            // Offline bracket analysis is not the ChatGPT response; never
            // allow it to masquerade as an AI result eligible for saving.
            aiLastModel = "";
            aiReviewDeckFingerprint = "";
            AiSaveReviewButton.IsEnabled = false;
            AiReviewResultViewer.Document = AiReviewMarkdown.Render(report.ToString());
            AiReviewStatus.Text = paperCommander
                ? "Local bracket check complete (no AI or network). This is a floor, not a final bracket."
                : "Hypothetical paper Commander card check complete (offline). Arena format unchanged; no bracket saved.";
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
        string analyzedFingerprint;
        try
        {
            analyzedFingerprint = NativeDeckEditing.ReviewFingerprint(
                FormatBox.Text, CommanderBox.Text,
                NativeDeckEditing.ParseMainboard(MainboardBox.Text));
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            AiReviewStatus.Text = "Correct the draft before analyzing: " + ex.Message;
            return;
        }
        string analyzedMode = (AiReviewModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "health";
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
            aiLastModel = model;
            aiLastMode = analyzedMode;
            aiReviewDeckFingerprint = analyzedFingerprint;
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
            if (AiSaveReviewButton is not null)
                AiSaveReviewButton.IsEnabled = original is not null &&
                    aiLastResponse.Length > 0 && aiLastModel.Length > 0 &&
                    aiReviewDeckFingerprint == analyzedFingerprint &&
                    NativeDeckEditing.ReviewFingerprint(FormatBox.Text, CommanderBox.Text,
                        NativeDeckEditing.ParseMainboard(MainboardBox.Text)) == analyzedFingerprint;
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
                "AI Review recommendation · local collection ownership",
                detail, catalogPath: catalogPath,
                nativePath: File.Exists(nativePath) ? nativePath : null)
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
