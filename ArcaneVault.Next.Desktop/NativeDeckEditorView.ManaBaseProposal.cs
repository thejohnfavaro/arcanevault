using ArcaneVault.Next.Collection;
using ArcaneVault.Next.EDHLab;
using System.IO;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// A preview of exact edits to the unsaved deck only. No collection, saved
/// library entry, or Proxy ledger is mutated by generating or applying it.
/// </summary>
public partial class NativeDeckEditorView
{
    private EdhLabManaBaseBuilder.Proposal? lastManaBaseProposal;
    private string? lastManaBaseSource;
    private int manaBaseRequestVersion;

    private async void OnEdhManaBaseBuildClicked(object sender, RoutedEventArgs e)
    {
        if (busy || edhLabCommander is null || edhCatalog is null ||
            MainboardBox is null || EdhManaBaseBuildButton is null ||
            EdhManaBaseApplyButton is null || !TryLandTarget(out int target))
        {
            if (EdhManaBasePlanStatus is not null)
                EdhManaBasePlanStatus.Text =
                    "Select a verified commander and a land target between 20 and 60.";
            return;
        }
        int request = ++manaBaseRequestVersion;
        string source = MainboardBox.Text;
        var catalog = edhCatalog.AllEligibleCards;
        string colors = edhLabColors ?? "";
        bool arena = OwnedPoolBox?.SelectedIndex == 1;
        var proxySnapshot = arena ? null :
            new Dictionary<string, long>(proxyCounts, StringComparer.OrdinalIgnoreCase);
        lastManaBaseProposal = null;
        lastManaBaseSource = null;
        EdhManaBaseApplyButton.IsEnabled = false;
        EdhManaBasePlanChanges.ItemsSource = null;
        EdhManaBaseBuildButton.IsEnabled = false;
        EdhManaBasePlanStatus.Text = "Evaluating verified land availability and draft color gaps…";
        try
        {
            var entries = NativeDeckEditing.ParseMainboard(source)
                .Select(x => (x.Name, x.Quantity)).ToArray();
            var proposal = await Task.Run(() =>
                EdhLabManaBaseBuilder.Build(catalog, colors, entries,
                    target, proxySnapshot, arena));
            if (request != manaBaseRequestVersion || !IsLoaded ||
                !string.Equals(source, MainboardBox.Text, StringComparison.Ordinal) ||
                edhCatalog is null ||
                !ReferenceEquals(catalog, edhCatalog.AllEligibleCards) ||
                (edhLabColors ?? "") != colors ||
                (OwnedPoolBox?.SelectedIndex == 1) != arena ||
                !TryLandTarget(out int currentTarget) || currentTarget != target)
            {
                EdhManaBasePlanStatus.Text =
                    "The draft or commander settings changed. Build a new proposal.";
                return;
            }
            lastManaBaseProposal = proposal;
            lastManaBaseSource = source;
            EdhManaBasePlanChanges.ItemsSource = proposal.Changes;
            EdhManaBasePlanStatus.Text = proposal.Summary;
            EdhManaBaseApplyButton.IsEnabled = proposal.CanApply;
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException or
            System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            EdhManaBasePlanStatus.Text = "No proposal applied: " + ex.Message;
        }
        finally
        {
            EdhManaBaseBuildButton.IsEnabled = !busy;
        }
    }

    private void OnEdhManaBaseApplyClicked(object sender, RoutedEventArgs e)
    {
        var proposal = lastManaBaseProposal;
        if (busy || edhLabCommander is null || edhCatalog is null ||
            proposal is null || !proposal.CanApply ||
            lastManaBaseSource is null ||
            MainboardBox is null || EdhManaBaseApplyButton is null)
            return;
        try
        {
            if (!ReferenceEquals(edhCatalog.AllEligibleCards, proposal.CatalogSnapshot) ||
                !string.Equals(MainboardBox.Text, lastManaBaseSource,
                    StringComparison.Ordinal) ||
                !string.Equals(edhLabColors ?? "", proposal.CommanderColors,
                    StringComparison.Ordinal) ||
                (OwnedPoolBox?.SelectedIndex == 1) != proposal.IsArena ||
                !TryLandTarget(out int target) || target != proposal.Target)
                throw new InvalidDataException(
                    "The draft, commander, availability pool, or land target changed. Rebuild the proposal.");

            var entries = NativeDeckEditing.ParseMainboard(MainboardBox.Text);
            string fingerprint = EdhLabManaBaseBuilder.Fingerprint(
                entries.Select(x => (x.Name, x.Quantity)));
            if (!fingerprint.Equals(proposal.SourceFingerprint,
                StringComparison.Ordinal))
                throw new InvalidDataException(
                    "The mainboard changed. Rebuild the mana-base proposal.");

            string review = proposal.Summary + "\n\n" +
                string.Join("\n", proposal.Changes.Select(x => x.Label)) +
                "\n\nApply these exact changes to the UNSAVED mainboard? " +
                "Existing nonbasic lands and spells will not be removed. " +
                "This never changes real ownership, Proxy holdings, or prices. " +
                "Review and save deck is still a separate action.";
            if (NativeInAppDialog.Show(Window.GetWindow(this), review,
                    "Review mana-base changes", MessageBoxButton.YesNo,
                    MessageBoxImage.Question, MessageBoxResult.No)
                != MessageBoxResult.Yes)
                return;

            string updated = MainboardBox.Text;
            foreach (var change in proposal.Changes.Where(x => x.Delta < 0))
                for (int i = 0; i < -change.Delta; i++)
                    updated = NativeDeckOwnedPicker.RemoveOne(updated, change.Name);
            foreach (var change in proposal.Changes.Where(x => x.Delta > 0))
            {
                var card = edhCatalog.Find(change.Name);
                if (card is null || !card.Type.Contains("Land",
                    StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Proposed land is no longer eligible.");
                for (int i = 0; i < change.Delta; i++)
                    updated = NativeDeckOwnedPicker.AddOne(updated, change.Name);
            }
            var resulting = NativeDeckEditing.ParseMainboard(updated);
            int lands = resulting.Sum(x => edhCatalog.Find(x.Name) is { } card &&
                card.Type.Contains("Land", StringComparison.OrdinalIgnoreCase)
                    ? x.Quantity : 0);
            if (lands != proposal.ResultingLands ||
                resulting.Sum(x => x.Quantity) != proposal.ResultingCopies)
                throw new InvalidDataException(
                    "The reviewed mana-base counts did not match. No changes were applied.");

            // Auto-balance basics is a separate feature. Disable it so it
            // cannot immediately rewrite this explicitly approved diff.
            if (EdhAutoBalanceLands?.IsChecked == true)
                EdhAutoBalanceLands.IsChecked = false;
            lastManaBaseProposal = null;
            lastManaBaseSource = null;
            EdhManaBaseApplyButton.IsEnabled = false;
            EdhManaBasePlanChanges.ItemsSource = null;
            MainboardBox.Text = updated;
            EdhManaBasePlanStatus.Text =
                "Approved changes applied to the unsaved mainboard. " +
                "Review and save the deck separately; no collection data changed.";
            StatusText.Text = EdhManaBasePlanStatus.Text;
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException)
        {
            EdhManaBaseApplyButton.IsEnabled = false;
            EdhManaBasePlanStatus.Text = "Mana-base changes not applied: " +
                ex.Message;
        }
    }
}
