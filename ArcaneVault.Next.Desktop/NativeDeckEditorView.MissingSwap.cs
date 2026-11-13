using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using ArcaneVault.Next.EDHLab;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

public partial class NativeDeckEditorView
{
    private readonly bool focusShortageReview;
    private readonly string? preferredShortageName;

    private sealed record MissingDeckRow(string Name, int Required, long Owned, long Missing,
        bool IsMainboard)
    {
        public string Display => $"{Missing:N0} missing · {Name}  ({Owned:N0}/{Required:N0} recorded)" +
            (IsMainboard ? "" : " · commander only");
    }

    // This review consumes the exact same pool-isolated shortage accounting as
    // Deck Library. It does not reserve copies for different saved decks.
    private void RefreshMissingCardReview(NativeDeckOwnedPicker.DraftSummary summary)
    {
        if (edhLabCommander is null || EdhMissingShortagesList is null) return;
        string? previous = (EdhMissingShortagesList.SelectedItem as MissingDeckRow)?.Name;
        var mainboard = NativeDeckEditing.ParseMainboard(MainboardBox.Text);
        var names = mainboard.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = summary.Shortages
            .Select(x => new MissingDeckRow(x.Name, x.Required, x.Owned,
                x.Missing, names.Contains(x.Name))).ToArray();

        EdhMissingHeadline.Text = rows.Length == 0
            ? "All required copies are recorded in this format's selected inventory."
            : $"{summary.MissingCopies:N0} missing copies across {rows.Length:N0} card names";
        EdhMissingShortagesList.ItemsSource = rows;
        EdhMissingShortagesList.SelectedItem = rows.FirstOrDefault(x =>
                x.Name.Equals(previous ?? "", StringComparison.OrdinalIgnoreCase)) ??
            rows.FirstOrDefault(x =>
                x.Name.Equals(preferredShortageName ?? "", StringComparison.OrdinalIgnoreCase)) ??
            rows.FirstOrDefault(x => x.IsMainboard) ?? rows.FirstOrDefault();

        if (rows.Length == 0)
        {
            EdhMissingAlternativesList.ItemsSource = null;
            EdhMissingAlternativesStatus.Text =
                "No recorded-copy shortages. Cards are not reserved across decks.";
            EdhMissingAlternativeDetails.Text =
                "This is a name-level comparison, not printing or deck legality.";
            EdhApplyMissingSwapButton.IsEnabled = false;
            EdhMissingComparisonPanel.Visibility = Visibility.Collapsed;
            EdhMissingImpactPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ClearMissingCardReview(string status)
    {
        if (EdhMissingShortagesList is null) return;
        EdhMissingShortagesList.ItemsSource = null;
        EdhMissingAlternativesList.ItemsSource = null;
        EdhMissingHeadline.Text = "Missing-card review unavailable";
        EdhMissingAlternativesStatus.Text = status;
        EdhMissingAlternativeDetails.Text = "No cards were added, removed or saved.";
        EdhApplyMissingSwapButton.IsEnabled = false;
        EdhMissingComparisonPanel.Visibility = Visibility.Collapsed;
        EdhMissingImpactPanel.Visibility = Visibility.Collapsed;
    }

    private void OnEdhMissingShortageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (EdhMissingAlternativesList is null ||
            EdhMissingShortagesList is null) return;
        EdhMissingAlternativesList.ItemsSource = null;
        EdhApplyMissingSwapButton.IsEnabled = false;
        EdhMissingComparisonPanel.Visibility = Visibility.Collapsed;
        EdhMissingImpactPanel.Visibility = Visibility.Collapsed;
        EdhMissingAlternativeDetails.Text =
            "Select an alternative to read why it was listed. No automatic substitutions.";
        if (EdhMissingShortagesList.SelectedItem is not MissingDeckRow shortage)
        {
            EdhMissingAlternativesStatus.Text = "Choose a missing card name.";
            return;
        }
        if (!shortage.IsMainboard)
        {
            EdhMissingAlternativesStatus.Text =
                "This shortage is only the separate commander slot. The commander " +
                "cannot be replaced by a mainboard swap; obtain it or deliberately " +
                "edit/change the commander in a separate validated workflow.";
            return;
        }
        if (edhCatalog is null)
        {
            EdhMissingAlternativesStatus.Text =
                "Waiting for the local format/identity-verified card catalog…";
            return;
        }
        try
        {
            var entries = NativeDeckEditing.ParseMainboard(MainboardBox.Text);
            var proposals = EdhLabMissingCardAdvisor.Analyze(
                edhCatalog, entries.Select(x => (x.Name, x.Quantity)), shortage.Name,
                focus: SelectedStrategyFocus,
                landTarget: TryLandTarget(out int targetLands) ? targetLands : 36);
            EdhMissingAlternativesList.ItemsSource = proposals;
            EdhMissingAlternativesStatus.Text = proposals.Count == 0
                ? "No conservative, recorded role-match found. The missing card may " +
                  "lack recognized metadata or all matching eligible copies are " +
                  "already in the draft. Use Cards or Swap Lab for manual review."
                : $"{proposals.Count:N0} ranked, locally eligible recorded role matches " +
                  $"for {shortage.Name}. Priority: " +
                  $"{EdhLabStrategyGoals.Label(SelectedStrategyFocus)}. " +
                  "Select a candidate for a read-only, card-by-card explanation.";
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException)
        {
            EdhMissingAlternativesStatus.Text =
                "Correct the draft before proposing replacements: " + ex.Message;
        }
    }

    private void ShowMissingCardComparison(EdhLabMissingCardAdvisor.Alternative? alternative)
    {
        if (EdhMissingComparisonPanel is null) return;
        if (alternative?.MissingCard is not { } source ||
            alternative.ReplacementCard is not { } candidate)
        {
            EdhMissingComparisonPanel.Visibility = Visibility.Collapsed;
            return;
        }
        // The comparison uses only the already-validated local catalog
        // snapshot. No network request, new window or collection operation.
        EdhCompareSourceName.Text = source.Name;
        EdhCompareSourceType.Text = source.Type;
        EdhCompareSourceManaValue.Text = source.ManaLabel;
        EdhCompareSourceMana.Document = NativeOracleReading.Build(
            string.IsNullOrWhiteSpace(source.ManaCost)
                ? "Mana cost unavailable" : source.ManaCost, 12);
        EdhCompareSourceRoles.Text = source.RoleLabel;
        EdhCompareSourceInventory.Text = source.RecordedLabel;
        EdhCompareSourceOracle.Document = NativeOracleReading.Build(
            string.IsNullOrWhiteSpace(source.Oracle)
                ? "No Oracle rules text recorded." : source.Oracle, 11);

        EdhCompareCandidateName.Text = candidate.Name;
        EdhCompareCandidateType.Text = candidate.Type;
        EdhCompareCandidateManaValue.Text = candidate.ManaLabel;
        EdhCompareCandidateMana.Document = NativeOracleReading.Build(
            string.IsNullOrWhiteSpace(candidate.ManaCost)
                ? "Mana cost unavailable" : candidate.ManaCost, 12);
        EdhCompareCandidateRoles.Text = candidate.RoleLabel;
        EdhCompareCandidateInventory.Text = candidate.RecordedLabel;
        EdhCompareCandidateOracle.Document = NativeOracleReading.Build(
            string.IsNullOrWhiteSpace(candidate.Oracle)
                ? "No Oracle rules text recorded." : candidate.Oracle, 11);
        EdhMissingRankingRationale.Text = alternative.RankingReasons;
        EdhMissingComparisonPanel.Visibility = Visibility.Visible;
    }

    // Project a 1-for-1 change into a temporary text buffer. Never assign
    // MainboardBox.Text, write SQLite, or infer ownership between pools here.
    private void ShowProjectedMissingSwapImpact(
        EdhLabMissingCardAdvisor.Alternative? alternative)
    {
        if (EdhMissingImpactPanel is null) return;
        EdhMissingImpactPanel.Visibility = Visibility.Collapsed;
        if (alternative is null || edhCatalog is null ||
            EdhMissingShortagesList.SelectedItem is not MissingDeckRow
                { IsMainboard: true } missing) return;
        try
        {
            var entries = NativeDeckEditing.ParseMainboard(MainboardBox.Text);
            int targetLands = TryLandTarget(out int desired) ? desired : 36;
            var impact = EdhLabSwapImpact.Analyze(edhCatalog,
                entries.Select(x => (x.Name, x.Quantity)),
                missing.Name, alternative.Name, targetLands);

            string projected = NativeDeckOwnedPicker.AddOne(
                NativeDeckOwnedPicker.RemoveOne(MainboardBox.Text, missing.Name),
                alternative.Name);
            var pool = edhLabFormat == EdhLabFormat.ArenaBrawl
                ? NativeDeckOwnedPicker.Pool.Arena
                : NativeDeckOwnedPicker.Pool.Physical;
            var before = NativeDeckOwnedPicker.Review(ownership, pool,
                MainboardBox.Text, CommanderBox.Text, FormatBox.Text);
            var after = NativeDeckOwnedPicker.Review(ownership, pool,
                projected, CommanderBox.Text, FormatBox.Text);
            string scope = pool == NativeDeckOwnedPicker.Pool.Arena
                ? "Arena" : "Physical";
            EdhMissingImpactSummary.Text = impact.Summary;
            EdhMissingImpactAvailability.Text =
                $"{scope} name-level coverage if applied: " +
                $"{before.CoveredCopies:N0}/{before.TotalWithCommander:N0} → " +
                $"{after.CoveredCopies:N0}/{after.TotalWithCommander:N0} covered. " +
                $"Missing: {before.MissingCopies:N0} → {after.MissingCopies:N0}. " +
                "Copies are not reserved for other decks.";
            EdhMissingImpactPanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or
            OverflowException)
        {
            EdhMissingImpactSummary.Text = "Projection unavailable: " + ex.Message;
            EdhMissingImpactAvailability.Text =
                "No draft or collection data was changed.";
            EdhMissingImpactPanel.Visibility = Visibility.Visible;
        }
    }

    private void OnEdhMissingAlternativeSelected(object sender, SelectionChangedEventArgs e)
    {
        var selected = EdhMissingAlternativesList?.SelectedItem as
            EdhLabMissingCardAdvisor.Alternative;
        EdhMissingAlternativeDetails.Text = selected?.Explanation ??
            "Choose a recorded alternative to review its shared role hints.";
        ShowMissingCardComparison(selected);
        ShowProjectedMissingSwapImpact(selected);
        EdhApplyMissingSwapButton.IsEnabled = !busy &&
            selected is not null &&
            EdhMissingShortagesList?.SelectedItem is MissingDeckRow { IsMainboard: true };
    }

    private void OnEdhApplyMissingSwapClicked(object sender, RoutedEventArgs e)
    {
        if (busy || edhCatalog is null ||
            EdhMissingShortagesList?.SelectedItem is not MissingDeckRow shortage ||
            !shortage.IsMainboard ||
            EdhMissingAlternativesList?.SelectedItem is not
                EdhLabMissingCardAdvisor.Alternative alternative) return;
        try
        {
            var entries = NativeDeckEditing.ParseMainboard(MainboardBox.Text);
            var pool = edhLabFormat == EdhLabFormat.ArenaBrawl
                ? NativeDeckOwnedPicker.Pool.Arena
                : NativeDeckOwnedPicker.Pool.Physical;
            var freshShortage = NativeDeckOwnedPicker.Review(
                ownership, pool, MainboardBox.Text, CommanderBox.Text, FormatBox.Text)
                .Shortages.FirstOrDefault(x =>
                    x.Name.Equals(shortage.Name, StringComparison.OrdinalIgnoreCase));
            var freshAlternatives = EdhLabMissingCardAdvisor.Analyze(edhCatalog,
                entries.Select(x => (x.Name, x.Quantity)), shortage.Name,
                focus: SelectedStrategyFocus,
                landTarget: TryLandTarget(out int targetLands) ? targetLands : 36);
            if (freshShortage is null || freshShortage.Missing < 1 ||
                !freshAlternatives.Any(x =>
                    x.Name.Equals(alternative.Name, StringComparison.OrdinalIgnoreCase) &&
                    x.Score == alternative.Score &&
                    x.RankingReasons == alternative.RankingReasons))
            {
                RefreshDraftSummary();
                StatusText.Text = "Shortages or eligible alternatives changed. " +
                    "Review the refreshed list before choosing a swap.";
                return;
            }
            // Recompute the whole-draft impact at confirmation time; stale
            // cached role/curve summaries never authorize a substitution.
            var freshImpact = EdhLabSwapImpact.Analyze(edhCatalog,
                entries.Select(x => (x.Name, x.Quantity)),
                shortage.Name, alternative.Name,
                TryLandTarget(out int previewLands) ? previewLands : 36);
            if (NativeInAppDialog.Show(Window.GetWindow(this),
                $"Replace ONE unsaved mainboard copy of {shortage.Name} with " +
                $"{alternative.Name}?\n\n{alternative.Explanation}\n\n" +
                "Projected whole-deck impact:\n" +
                freshImpact.Summary + "\n\n" +
                "This edits only the current draft. It does not purchase cards, " +
                "change the collection, verify printings or save the deck. " +
                "Review and save remains a separate action.",
                "Review recorded-card replacement",
                MessageBoxButton.YesNo, MessageBoxImage.Question,
                MessageBoxResult.No) != MessageBoxResult.Yes)
                return;

            int before = entries.Sum(x => x.Quantity);
            string after = NativeDeckOwnedPicker.AddOne(
                NativeDeckOwnedPicker.RemoveOne(MainboardBox.Text, shortage.Name),
                alternative.Name);
            if (NativeDeckEditing.ParseMainboard(after).Sum(x => x.Quantity) != before)
                throw new InvalidDataException("Replacement changed the number of mainboard copies.");
            MainboardBox.Text = after;
            StatusText.Text = $"Replaced one UNSAVED mainboard copy: " +
                $"-1 {shortage.Name}, +1 {alternative.Name}. " +
                "Other shortages may remain. No inventory, saved decks or " +
                "revision history changed; Review and save is still required.";
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException)
        {
            StatusText.Text = "Replacement not applied: " + ex.Message;
        }
    }
}
