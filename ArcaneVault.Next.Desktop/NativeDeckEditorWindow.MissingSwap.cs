using ArcaneVault.Next.Collection;
using ArcaneVault.Next.EDHLab;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

public partial class NativeDeckEditorWindow
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
    }

    private void OnEdhMissingShortageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (EdhMissingAlternativesList is null ||
            EdhMissingShortagesList is null) return;
        EdhMissingAlternativesList.ItemsSource = null;
        EdhApplyMissingSwapButton.IsEnabled = false;
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
                edhCatalog, entries.Select(x => (x.Name, x.Quantity)), shortage.Name);
            EdhMissingAlternativesList.ItemsSource = proposals;
            EdhMissingAlternativesStatus.Text = proposals.Count == 0
                ? "No conservative, recorded role-match found. The missing card may " +
                  "lack recognized metadata or all matching eligible copies are " +
                  "already in the draft. Use Cards or Swap Lab for manual review."
                : $"{proposals.Count:N0} locally eligible recorded role matches for " +
                  $"{shortage.Name} (not verified equivalents). Review card text " +
                  "and strategy before choosing any swap.";
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException)
        {
            EdhMissingAlternativesStatus.Text =
                "Correct the draft before proposing replacements: " + ex.Message;
        }
    }

    private void OnEdhMissingAlternativeSelected(object sender, SelectionChangedEventArgs e)
    {
        var selected = EdhMissingAlternativesList?.SelectedItem as
            EdhLabMissingCardAdvisor.Alternative;
        EdhMissingAlternativeDetails.Text = selected?.Explanation ??
            "Choose a recorded alternative to review its shared role hints.";
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
                entries.Select(x => (x.Name, x.Quantity)), shortage.Name);
            if (freshShortage is null || freshShortage.Missing < 1 ||
                !freshAlternatives.Any(x =>
                    x.Name.Equals(alternative.Name, StringComparison.OrdinalIgnoreCase)))
            {
                RefreshDraftSummary();
                StatusText.Text = "Shortages or eligible alternatives changed. " +
                    "Review the refreshed list before choosing a swap.";
                return;
            }
            if (NativeInAppDialog.Show(this,
                $"Replace ONE unsaved mainboard copy of {shortage.Name} with " +
                $"{alternative.Name}?\n\n{alternative.Explanation}\n\n" +
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
