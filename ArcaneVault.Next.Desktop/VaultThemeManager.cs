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

        foreach (var replacement in replacements)
            resources[replacement.Key] = replacement.Brush;
        resources["VaultThemeBanner"] = gradient;
        CurrentId = theme.Id;
    }
}
