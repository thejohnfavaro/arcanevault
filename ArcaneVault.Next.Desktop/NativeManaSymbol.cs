using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// A mana glyph taken from the exact official SVG supplied by Scryfall.
/// Prior hand-drawn approximations have been removed. When offline before
/// first load, the original {token} remains readable instead of a fake icon.
/// Used identically in Color Identity and in Oracle text.
/// </summary>
public sealed class NativeManaSymbol : Grid
{
    private readonly Image icon = new()
    {
        Stretch = Stretch.Uniform,
        Visibility = Visibility.Collapsed,
        SnapsToDevicePixels = true
    };
    private readonly TextBlock fallback = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        FontSize = 10,
        FontWeight = FontWeights.SemiBold,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Foreground = Brushes.WhiteSmoke
    };

    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(nameof(Symbol), typeof(string),
            typeof(NativeManaSymbol), new PropertyMetadata("C",
                (obj, _) => ((NativeManaSymbol)obj).OnSymbolChanged()));

    public string Symbol
    {
        get => (string)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public NativeManaSymbol()
    {
        Children.Add(fallback);
        Children.Add(icon);
        Loaded += (_, _) => _ = ShowOfficialAsync();
        OnSymbolChanged();
    }

    private void OnSymbolChanged()
    {
        string code = Normalize(Symbol);
        fallback.Text = "{" + code + "}";
        string label = code.Length == 1
            ? NativeManaIdentity.NameFor(code)
            : code;
        ToolTip = label + " (" + fallback.Text + ")";
        AutomationProperties.SetName(this, label + " Magic symbol");
        icon.Source = null;
        icon.Visibility = Visibility.Collapsed;
        fallback.Visibility = Visibility.Visible;
        if (IsLoaded) _ = ShowOfficialAsync();
    }

    private static string Normalize(string? code) =>
        (code ?? "").Trim().Trim('{', '}').ToUpperInvariant();

    private async Task ShowOfficialAsync()
    {
        string expected = Normalize(Symbol);
        ImageSource? official = await NativeOfficialCardSymbols.GetAsync(expected);
        if (!IsLoaded || expected != Normalize(Symbol) || official is null) return;
        icon.Source = official;
        icon.Visibility = Visibility.Visible;
        fallback.Visibility = Visibility.Collapsed;
    }
}
