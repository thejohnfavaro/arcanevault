using ArcaneVault.Next.Domain;
using ArcaneVault.Next.EDHLab;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Non-destructive suggestions for the free-text commander field.
/// Matches the local Scryfall commander index, never changes saved decks or ownership.
/// </summary>
internal static class CommanderNameSuggestions
{
    public static void Attach(TextBox editor, ComboBox format, string? catalogPath)
    {
        if (string.IsNullOrWhiteSpace(catalogPath)) return;
        var results = new ListBox
        {
            MaxHeight = 270,
            MinWidth = 280,
            Background = new SolidColorBrush(Color.FromRgb(28, 34, 44)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(91, 110, 127)),
            BorderThickness = new Thickness(1),
            DisplayMemberPath = nameof(Suggestion.Display)
        };
        var popup = new Popup
        {
            PlacementTarget = editor, Placement = PlacementMode.Bottom,
            AllowsTransparency = true, StaysOpen = false,
            Child = results
        };
        List<EdhLabCommanderCard> paper = [];
        List<EdhLabCommanderCard> brawl = [];
        bool ready = false, selecting = false, loaded = false;

        static string FrontFace(string name)
        {
            var at = name.IndexOf(" // ", StringComparison.Ordinal);
            return at < 0 ? name : name[..at];
        }
        bool IsBrawl() => format.Text.Trim().Equals("Arena Brawl",
            StringComparison.OrdinalIgnoreCase);
        void Refresh()
        {
            if (!ready || selecting || !editor.IsKeyboardFocusWithin ||
                editor.IsReadOnly || editor.Text.Trim().Length < 2)
            {
                popup.IsOpen = false;
                return;
            }
            var typed = editor.Text.Trim();
            var candidates = IsBrawl() ? brawl : paper;
            var matches = candidates
                .Where(c => FrontFace(c.Name).Contains(typed, StringComparison.OrdinalIgnoreCase) ||
                            c.Name.Contains(typed, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => FrontFace(c.Name).StartsWith(typed,
                    StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(c => FrontFace(c.Name), StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .Select(c => new Suggestion(FrontFace(c.Name), c.Name))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First()).ToArray();
            results.ItemsSource = matches;
            popup.IsOpen = matches.Length > 0 &&
                !(matches.Length == 1 && string.Equals(matches[0].Name,
                    typed, StringComparison.OrdinalIgnoreCase));
            if (popup.IsOpen)
                results.MinWidth = Math.Max(280, editor.ActualWidth);
        }
        void Accept()
        {
            if (results.SelectedItem is not Suggestion candidate) return;
            selecting = true;
            try
            {
                editor.Text = candidate.Name;
                editor.CaretIndex = editor.Text.Length;
                popup.IsOpen = false;
                editor.Focus();
            }
            finally { selecting = false; }
        }
        editor.TextChanged += (_, _) => Refresh();
        editor.GotKeyboardFocus += (_, _) => Refresh();
        editor.LostKeyboardFocus += (_, _) =>
        {
            // The popup handles mouse selection separately; never modify the input.
            if (!results.IsKeyboardFocusWithin) popup.IsOpen = false;
        };
        editor.PreviewKeyDown += (_, e) =>
        {
            if (!popup.IsOpen) return;
            if (e.Key == Key.Escape) { popup.IsOpen = false; e.Handled = true; }
            else if (e.Key == Key.Down)
            {
                results.SelectedIndex = Math.Min(results.Items.Count - 1,
                    results.SelectedIndex + 1);
                results.ScrollIntoView(results.SelectedItem);
                e.Handled = true;
            }
            else if ((e.Key == Key.Enter || e.Key == Key.Tab) &&
                     results.SelectedItem is not null)
            {
                Accept();
                e.Handled = e.Key == Key.Enter;
            }
        };
        results.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (ItemsControl.ContainerFromElement(results,
                    e.OriginalSource as DependencyObject) is ListBoxItem item)
            {
                results.SelectedItem = item.DataContext;
                Accept();
                e.Handled = true;
            }
        };
        format.SelectionChanged += (_, _) => Refresh();
        editor.Loaded += async (_, _) =>
        {
            if (loaded) return;
            loaded = true;
            try
            {
                var available = await Task.Run(() =>
                    CommanderCatalog.OpenReadOnly(catalogPath));
                paper = available.Filter(new EdhLabQuery(
                    Format: EdhLabFormat.PaperCommander, PageSize: 200)).ToList();
                brawl = available.Filter(new EdhLabQuery(
                    Format: EdhLabFormat.ArenaBrawl, PageSize: 200)).ToList();
                ready = true;
                Refresh();
            }
            catch (Exception)
            {
                // Optional suggestions must never prevent deck editing or saving.
                popup.IsOpen = false;
            }
        };
        editor.Unloaded += (_, _) => popup.IsOpen = false;
    }

    private sealed record Suggestion(string Name, string CatalogName)
    {
        public string Display => Name == CatalogName ? Name :
            Name + "  (transforming card)";
    }
}
