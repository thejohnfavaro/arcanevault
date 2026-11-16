using ArcaneVault.Next.EDHLab;
using System.IO;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// One-click, draft-only mana optimization. Uses the verified commander
/// catalog and actual owned copies. Never saves a deck or mutates inventory.
/// </summary>
public partial class NativeDeckEditorWindow
{
    private int smartManaVersion;

    private async void OnEdhSmartManaClicked(object sender, RoutedEventArgs e)
    {
        if (busy || edhCatalog is null || edhLabCommander is null ||
            !TryLandTarget(out int target))
        {
            EdhManaBasePlanStatus.Text =
                "Select a verified commander and a valid land target first.";
            return;
        }
        int version = ++smartManaVersion;
        var source = MainboardBox.Text;
        var snapshot = edhCatalog.AllEligibleCards;
        string colors = edhLabColors ?? "";
        bool arena = OwnedPoolBox?.SelectedIndex == 1;
        EdhSmartManaButton.IsEnabled = false;
        EdhManaBasePlanStatus.Text =
            "Smart Mana: checking real owned shock, untapped and fallback lands…";
        try
        {
            var entries = NativeDeckEditing.ParseMainboard(source)
                .Select(x => (x.Name, x.Quantity)).ToArray();
            var plan = await Task.Run(() =>
                EdhLabManaBaseBuilder.Build(snapshot, colors, entries, target,
                    paperProxies: null, arena: arena, optimizeBasics: true,
                    smartMana: true));
            if (version != smartManaVersion || !IsLoaded || busy ||
                edhCatalog is null ||
                !ReferenceEquals(snapshot, edhCatalog.AllEligibleCards) ||
                !string.Equals(MainboardBox.Text, source, StringComparison.Ordinal) ||
                !string.Equals(edhLabColors ?? "", colors, StringComparison.Ordinal) ||
                (OwnedPoolBox?.SelectedIndex == 1) != arena ||
                !TryLandTarget(out int current) || current != target)
            {
                EdhManaBasePlanStatus.Text =
                    "The draft, commander or inventory changed. Run Smart Mana again.";
                return;
            }
            if (!plan.CanApply)
            {
                EdhManaBasePlanStatus.Text =
                    plan.Warning.Length > 0 ? "Smart Mana made no edits: " + plan.Warning :
                    "Smart Mana: nothing eligible to improve using your recorded owned lands.";
                return;
            }
            // Full diff is prepared offscreen. A single final assignment ensures
            // that a partial failure cannot leave an incompletely edited draft.
            string updated = source;
            foreach (var change in plan.Changes.Where(x => x.Delta < 0))
                for (int i = 0; i < -change.Delta; i++)
                    updated = NativeDeckOwnedPicker.RemoveOne(updated, change.Name);
            foreach (var change in plan.Changes.Where(x => x.Delta > 0))
            {
                var card = edhCatalog.Find(change.Name);
                if (card is null || !card.Type.Contains("Land",
                        StringComparison.OrdinalIgnoreCase) ||
                    card.RecordedCopies < change.Delta)
                    throw new InvalidDataException("Land eligibility or owned quantity changed.");
                for (int i = 0; i < change.Delta; i++)
                    updated = NativeDeckOwnedPicker.AddOne(updated, change.Name);
            }
            var result = NativeDeckEditing.ParseMainboard(updated);
            int actualLands = result.Sum(x =>
                edhCatalog.Find(x.Name) is { } c &&
                c.Type.Contains("Land", StringComparison.OrdinalIgnoreCase)
                    ? x.Quantity : 0);
            if (actualLands != plan.ResultingLands ||
                result.Sum(x => x.Quantity) != plan.ResultingCopies)
                throw new InvalidDataException("Computed land or deck totals did not match.");

            // Only lands may change: nonlands are byte-for-byte equivalent
            // by name and quantity, even if their text ordering changes.
            var beforeSpells = entries.Where(x =>
                edhCatalog.Find(x.Name) is not { } c ||
                !c.Type.Contains("Land", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            var afterSpells = result.Where(x =>
                edhCatalog.Find(x.Name) is not { } c ||
                !c.Type.Contains("Land", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            if (!beforeSpells.SequenceEqual(afterSpells))
                throw new InvalidDataException("Smart Mana refused a nonland change.");

            if (EdhAutoBalanceLands?.IsChecked == true)
                EdhAutoBalanceLands.IsChecked = false;
            lastManaBaseProposal = null;
            lastManaBaseSource = null;
            EdhManaBaseApplyButton.IsEnabled = false;
            EdhManaBasePlanChanges.ItemsSource = plan.Changes;
            MainboardBox.Text = updated;
            EdhManaBasePlanStatus.Text =
                $"Smart Mana applied {plan.Changes.Count} land changes to the UNSAVED draft. " +
                "Shock → untapped → conditional → tapped priority; only real recorded owned " +
                "land copies used. Review & Save remains separate.";
            StatusText.Text = EdhManaBasePlanStatus.Text;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or
            OverflowException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            EdhManaBasePlanStatus.Text = "Smart Mana made no changes: " + ex.Message;
        }
        finally
        {
            EdhSmartManaButton.IsEnabled = !busy;
        }
    }
}
