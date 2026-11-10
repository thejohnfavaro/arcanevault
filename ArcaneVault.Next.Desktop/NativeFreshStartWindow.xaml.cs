using ArcaneVault.Next.Domain;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

public partial class NativeFreshStartWindow : Window
{
    public NativeFreshStartWindow(NativeBackupView? current)
    {
        InitializeComponent();
        if (current is null)
        {
            ImpactText.Text =
                "No native collection exists yet. This creates a brand-new, empty native collection without an export or legacy app.";
            ProtectionText.Text =
                "No recovery copy is needed because there is no native collection to replace. " +
                "Your existing Scryfall catalog, if present, remains available.";
        }
        else
        {
            ImpactText.Text =
                $"Current native collection: {current.PhysicalCopies:N0} physical copies " +
                $"({current.PhysicalCards.Count:N0} printing rows), {current.ArenaCopies:N0} Arena copies, " +
                $"{current.Decks:N0} decks, {current.DeckRevisions:N0} revisions, " +
                $"{current.PreferredArtworks:N0} preferred art entries.";
            ProtectionText.Text =
                "Before the new empty collection becomes active, the complete previous native " +
                "SQLite workspace is retained as a uniquely named recovery point. " +
                "Use 'Restore native recovery' to undo. No existing recovery point is erased.";
        }
    }

    private void OnConfirmationChanged(object sender, TextChangedEventArgs e)
    {
        if (ConfirmButton is not null && ConfirmationInput is not null)
            ConfirmButton.IsEnabled =
                string.Equals(ConfirmationInput.Text.Trim(), "START FRESH", StringComparison.Ordinal);
    }

    private void OnConfirmClicked(object sender, RoutedEventArgs e)
    {
        if (ConfirmationInput.Text.Trim() != "START FRESH") return;
        DialogResult = true;
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e) => DialogResult = false;
}
