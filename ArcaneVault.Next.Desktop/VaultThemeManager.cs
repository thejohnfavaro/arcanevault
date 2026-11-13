using ArcaneVault.Next.Collection;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Replace shared application palette brushes, including resources WPF froze
/// while loading compiled XAML. Consumers use DynamicResource so existing
/// windows update without changing collection or catalog state.
/// </summary>
public static class VaultThemeManager
{
    public static string CurrentId { get; private set; } = NativeThemes.Default.Id;

    public static void LoadSaved() => Apply(NativeThemeSettings.Load());

    // Keep accent washes subtle enough for readable text across light, black
    // and all Magic color themes. No frozen shared brush is ever mutated.
    private static Color Mix(Color under, Color tint, double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        byte Channel(byte a, byte b) =>
            (byte)Math.Clamp((int)Math.Round(a + (b - a) * fraction), 0, 255);
        return Color.FromRgb(Channel(under.R, tint.R),
            Channel(under.G, tint.G), Channel(under.B, tint.B));
    }

    private static LinearGradientBrush Wash(
        (Color Color, double Offset) first,
        (Color Color, double Offset) second,
        (Color Color, double Offset)? third = null,
        bool vertical = false)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = vertical ? new Point(0.85, 1) : new Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop(first.Color, first.Offset));
        brush.GradientStops.Add(new GradientStop(second.Color, second.Offset));
        if (third is { } stop)
            brush.GradientStops.Add(new GradientStop(stop.Color, stop.Offset));
        return brush;
    }


    public static void Apply(string id)
    {
        var theme = NativeThemes.Find(id) ??
            throw new InvalidDataException("Unknown native appearance theme.");
        var resources = Application.Current?.Resources ??
            throw new InvalidOperationException("Application resources unavailable.");

        // Pre-validate the entire palette before replacing any resource. Never
        // assign Color to a shared SolidColorBrush because WPF may freeze it.
        var replacements = theme.Brushes.Select(pair =>
        {
            if (resources[pair.Key] is not SolidColorBrush)
                throw new InvalidOperationException("Missing theme brush: " + pair.Key);
            var color = (Color)ColorConverter.ConvertFromString(pair.Value)!;
            return (Key: pair.Key, Brush: new SolidColorBrush(color));
        }).ToArray();

        var colors = theme.PreviewColors
            .Select(value => (Color)ColorConverter.ConvertFromString(value)!)
            .ToArray();
        if (colors.Length == 0)
            throw new InvalidOperationException("Theme has no visible accent colors.");
        if (resources["VaultThemeBanner"] is not LinearGradientBrush)
            throw new InvalidOperationException("Missing theme banner brush.");
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5)
        };
        if (colors.Length == 1)
        {
            gradient.GradientStops.Add(new GradientStop(colors[0], 0));
            gradient.GradientStops.Add(new GradientStop(colors[0], 1));
        }
        else
            for (int i = 0; i < colors.Length; i++)
                gradient.GradientStops.Add(new GradientStop(colors[i],
                    (double)i / (colors.Length - 1)));

        // Validate every new theme-aware decorative token BEFORE any writes,
        // just like the core palette. This protects appearance changes against
        // half-applied states and avoids the frozen-brush crash regression.
        foreach (string key in new[] { "VaultHeroWash", "VaultSidebarWash", "VaultTileWash" })
            if (resources[key] is not LinearGradientBrush)
                throw new InvalidOperationException("Missing theme wash: " + key);
        if (resources["VaultNavHoverWash"] is not SolidColorBrush ||
            resources["VaultEdgeLift"] is not SolidColorBrush)
            throw new InvalidOperationException("Missing theme accent surface tokens.");

        Color panel = (Color)ColorConverter.ConvertFromString(theme.Panel)!;
        Color raised = (Color)ColorConverter.ConvertFromString(theme.PanelRaised)!;
        Color sidebar = (Color)ColorConverter.ConvertFromString(theme.Sidebar)!;
        Color accent = (Color)ColorConverter.ConvertFromString(theme.Accent)!;
        Color line = (Color)ColorConverter.ConvertFromString(theme.Line)!;

        var heroWash = Wash((Mix(panel, accent, 0.19), 0),
            (Mix(panel, raised, 0.42), 0.55),
            (Mix(panel, sidebar, 0.29), 1));
        var sidebarWash = Wash((Mix(sidebar, accent, 0.075), 0),
            (sidebar, 1), vertical: true);
        var tileWash = Wash((Mix(raised, accent, 0.085), 0),
            (Mix(raised, panel, 0.2), 1));
        var navHoverWash = new SolidColorBrush(Mix(raised, accent, 0.15));
        var edgeLift = new SolidColorBrush(Mix(line, accent, 0.32));

        foreach (var replacement in replacements)
            resources[replacement.Key] = replacement.Brush;
        resources["VaultThemeBanner"] = gradient;
        resources["VaultHeroWash"] = heroWash;
        resources["VaultSidebarWash"] = sidebarWash;
        resources["VaultTileWash"] = tileWash;
        resources["VaultNavHoverWash"] = navHoverWash;
        resources["VaultEdgeLift"] = edgeLift;
        CurrentId = theme.Id;
    }
}
