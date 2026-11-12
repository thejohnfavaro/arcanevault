using ArcaneVault.Next.Collection;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Recolors the existing shared WPF brush objects in place so screens using
/// StaticResource update immediately. Never replaces data sources or edits a
/// card collection. Brushes are created unfrozen in App.xaml.
/// </summary>
public static class VaultThemeManager
{
    public static string CurrentId { get; private set; } = NativeThemes.Default.Id;

    public static void LoadSaved()
    {
        Apply(NativeThemeSettings.Load());
    }

    public static void Apply(string id)
    {
        var theme = NativeThemes.Find(id) ??
            throw new InvalidDataException("Unknown native appearance theme.");
        var resources = Application.Current?.Resources ??
            throw new InvalidOperationException("Application resources unavailable.");
        // Validate every resource before changing any live brush, so one
        // missing or frozen token cannot partially apply a palette.
        var changes = theme.Brushes.Select(pair =>
        {
            var brush = resources[pair.Key] as SolidColorBrush ??
                throw new InvalidOperationException("Missing theme brush: " + pair.Key);
            if (brush.IsFrozen)
                throw new InvalidOperationException("Theme brush is frozen: " + pair.Key);
            return (Brush: brush, Color: (Color)ColorConverter.ConvertFromString(pair.Value)!);
        }).ToArray();
        foreach (var change in changes)
            change.Brush.Color = change.Color;
        CurrentId = theme.Id;
    }
}
