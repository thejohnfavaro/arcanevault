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

    private void OnInputsChanged(object sender, RoutedEventArgs e)
    {
        if (ScopeBox is null || ConfirmationText is null ||
            ConfirmButton is null || ConfirmationPrompt is null ||
            Acknowledged is null) return;
        int selected = ScopeBox.SelectedIndex;
        ConfirmationPrompt.Text = selected >= 0 && selected < Phrases.Length
            ? $"To continue, type exactly: {Phrases[selected]}"
            : "Select a scope to see its confirmation phrase.";
        ConfirmButton.IsEnabled = selected >= 0 && selected < Phrases.Length &&
            ConfirmationText.Text.Trim().Equals(Phrases[selected],
                StringComparison.Ordinal) &&
            Acknowledged.IsChecked == true;
    }

    private void OnConfirmClicked(object sender, RoutedEventArgs e)
    {
        if (ConfirmButton.IsEnabled != true) return;
        SelectedScope = ScopeBox.SelectedIndex switch
        {
            0 => NativeWorkspaceSafety.InventoryScope.Physical,
            1 => NativeWorkspaceSafety.InventoryScope.Arena,
            2 => NativeWorkspaceSafety.InventoryScope.Both,
            _ => throw new InvalidOperationException("An inventory scope must be selected.")
        };
        DialogResult = true;
    }
}
