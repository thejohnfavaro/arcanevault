using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

public partial class NativeCollectionClearWindow : Window
{
    private static readonly string[] Phrases =
        ["CLEAR PHYSICAL", "CLEAR ARENA", "CLEAR BOTH"];

    public NativeWorkspaceSafety.InventoryScope SelectedScope { get; private set; }

    public NativeCollectionClearWindow(NativeBackupView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        InitializeComponent();
        CurrentTotals.Text =
            $"Current physical: {view.PhysicalCopies:N0} copies in {view.PhysicalCards.Count:N0} rows\n" +
            $"Current Arena: {view.ArenaCopies:N0} copies in {view.ArenaCards.Count:N0} rows\n" +
            $"Saved decks: {view.Decks:N0} · revisions: {view.DeckRevisions:N0} (preserved)";
    }

    private bool TryValidate(out NativeWorkspaceSafety.InventoryScope scope,
        out string reason)
    {
        scope = default;
        int selected = ScopeBox?.SelectedIndex ?? -1;
        if (selected < 0 || selected >= Phrases.Length)
        {
            reason = "Choose Physical only, Arena only, or Both before clearing.";
            return false;
        }
        string typed = ConfirmationText?.Text?.Trim() ?? "";
        if (!typed.Equals(Phrases[selected], StringComparison.OrdinalIgnoreCase))
        {
            reason = $"Type {Phrases[selected]} (capitalization does not matter).";
            return false;
        }
        if (Acknowledged?.IsChecked != true)
        {
            reason = "Phrase accepted. Check the full-backup acknowledgement to continue. " +
                "This is required even when the confirmation phrase is correct.";
            return false;
        }
        scope = selected switch
        {
            0 => NativeWorkspaceSafety.InventoryScope.Physical,
            1 => NativeWorkspaceSafety.InventoryScope.Arena,
            2 => NativeWorkspaceSafety.InventoryScope.Both,
            _ => throw new ArgumentOutOfRangeException(nameof(selected))
        };
        reason = "Ready to clear the selected inventory; a native recovery file will be kept.";
        return true;
    }

    private void OnInputsChanged(object sender, RoutedEventArgs e)
    {
        if (ConfirmationPrompt is null || ConfirmButton is null ||
            ScopeBox is null || ConfirmationText is null || Acknowledged is null)
            return;
        TryValidate(out _, out string explanation);
        ConfirmationPrompt.Text = explanation;
        // Always permit the click so validation can display exactly what is
        // missing, including when a checkbox is clipped, an input notification
        // is missed, or accessibility tools change focus. Nothing is cleared
        // until OnConfirmClicked repeats every precondition.
        ConfirmButton.IsEnabled = true;
    }

    private void OnConfirmClicked(object sender, RoutedEventArgs e)
    {
        if (!TryValidate(out var scope, out string explanation))
        {
            ConfirmationPrompt.Text = explanation;
            MessageBox.Show(this, explanation, "Clear inventory — confirmation needed",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        SelectedScope = scope;
        DialogResult = true;
    }
}
