using System.Text;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

public partial class NativeDeckEditorView
{
    /// <summary>
    /// Creates a read-only snapshot of the on-screen draft. Does not save,
    /// export a collection database, or contact an AI service.
    /// </summary>
    private void OnAiReviewCurrentDeckClicked(object sender, RoutedEventArgs e)
    {
        if (MainboardBox is null || CommanderBox is null || FormatBox is null) return;
        if (Window.GetWindow(this) is not MainWindow main)
        {
            MessageBox.Show("AI review is currently available in the main Arcane Vault window.",
                "AI Review", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var board = MainboardBox.Text.Trim();
        if (board.Length == 0)
        {
            MessageBox.Show("Add cards to the mainboard before requesting a deck review.",
                "AI Review", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = new StringBuilder();
        result.AppendLine("Format: " + FormatBox.Text.Trim());
        if (!string.IsNullOrWhiteSpace(CommanderBox.Text))
        {
            result.AppendLine("Commander");
            result.AppendLine("1 " + CommanderBox.Text.Trim());
        }
        result.AppendLine("Deck");
        result.AppendLine(board);
        // Include only the chosen pool's card-by-card coverage, not a full inventory.
        // The review dialog keeps this optional and unchecked by default.
        string? coverage = null;
        try
        {
            var pool = OwnedPoolBox?.SelectedIndex == 1
                ? ArcaneVault.Next.Collection.NativeDeckOwnedPicker.Pool.Arena
                : ArcaneVault.Next.Collection.NativeDeckOwnedPicker.Pool.Physical;
            var summary = ArcaneVault.Next.Collection.NativeDeckOwnedPicker.Review(
                ownership, pool, board, CommanderBox.Text, FormatBox.Text, proxyLedger);
            var lines = new StringBuilder();
            lines.AppendLine("Inventory: " + pool);
            lines.AppendLine(summary.Header);
            foreach (var card in summary.Shortages.Take(100))
                lines.AppendLine(card.Name + ": needed " + card.Required +
                    ", confirmed owned " + card.Owned +
                    ", proxy " + card.Proxy + ", still missing " + card.Missing);
            coverage = lines.ToString();
        }
        catch (Exception)
        {
            // Missing ownership data must not prevent reviewing a deck.
            coverage = null;
        }
        main.OpenAiDeckReview(result.ToString(), coverage);
    }
}
