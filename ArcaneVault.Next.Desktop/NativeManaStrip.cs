using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Exact mana-cost sequence or distinct color-identity symbols. Both modes
/// reuse NativeManaSymbol, which renders the original official Scryfall SVG.
/// No W/U/B/R/G letter facsimiles and no rewriting {2}{U} as {U}{U}.
/// A missing icon stays its literal {token} so meaning is preserved offline.
/// </summary>
public sealed class NativeManaStrip : WrapPanel
{
    public enum Presentation { Identity, Cost }
    public sealed record Piece(string Text, bool IsSymbol);

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string),
            typeof(NativeManaStrip), new PropertyMetadata("",
                (d, _) => ((NativeManaStrip)d).Refresh()));

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(nameof(Kind), typeof(Presentation),
            typeof(NativeManaStrip), new PropertyMetadata(Presentation.Identity,
                (d, _) => ((NativeManaStrip)d).Refresh()));

    public static readonly DependencyProperty SymbolSizeProperty =
        DependencyProperty.Register(nameof(SymbolSize), typeof(double),
            typeof(NativeManaStrip), new PropertyMetadata(22.0,
                (d, _) => ((NativeManaStrip)d).Refresh()));

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
    public Presentation Kind
    {
        get => (Presentation)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }
    public double SymbolSize
    {
        get => (double)GetValue(SymbolSizeProperty);
        set => SetValue(SymbolSizeProperty, value);
    }

    public NativeManaStrip()
    {
        Orientation = Orientation.Horizontal;
        Refresh();
    }

    public static IReadOnlyList<Piece> Pieces(string? value, Presentation kind)
    {
        if (kind == Presentation.Identity)
        {
            // Identity is an ordered, distinct set; generic and hybrid
            // symbols are NOT valid individual identity indicators.
            var colors = NativeManaIdentity.ParseColorIdentity(value);
            if (colors.Count == 0)
                return new[] { new Piece("Unknown identity", false) };
            return colors.Select(x => new Piece(x.Symbol, true)).ToArray();
        }

        if (kind != Presentation.Cost)
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<Piece>();

        // The token stream preserves every generic value, hybrid ratio,
        // Phyrexian slash and split-face separator exactly as stored.
        return NativeOracleSymbolTokenizer.Split(value)
            .Select(segment => new Piece(segment.IsSymbol
                    ? segment.Text[1..^1] : segment.Text, segment.IsSymbol))
            .ToArray();
    }

    private void Refresh()
    {
        Children.Clear();
        double size = SymbolSize is >= 12 and <= 48 ? SymbolSize : 22;
        var pieces = Pieces(Value, Kind);
        foreach (var piece in pieces)
        {
            if (piece.IsSymbol)
            {
                Children.Add(new NativeManaSymbol
                {
                    Symbol = piece.Text,
                    Width = size, Height = size,
                    Margin = new Thickness(0, 0, 3, 2)
                });
            }
            else if (piece.Text.Length > 0)
            {
                var text = new TextBlock
                {
                    Text = piece.Text,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 3, 2),
                    FontSize = size * 0.65
                };
                text.SetResourceReference(TextBlock.ForegroundProperty, "VaultText");
                Children.Add(text);
            }
        }
        System.Windows.Automation.AutomationProperties.SetName(this,
            Kind == Presentation.Cost
                ? "Exact mana cost: " + (Value ?? "")
                : "Color identity: " + string.Join(", ",
                    NativeManaIdentity.ParseColorIdentity(Value).Select(x => x.Name)));
    }
}
