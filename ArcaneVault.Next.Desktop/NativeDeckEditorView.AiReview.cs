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
        main.OpenAiDeckReview(result.ToString());
    }
}
