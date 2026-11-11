using ArcaneVault.Next.Collection;
using System.IO;
using System.Windows.Media;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Presentation-only series for WPF. Copies are counted by their primary card
/// type; color identity is displayed as exclusive colorless/single/multicolor
/// categories. All bars in a series share one safe maximum for comparison.
/// </summary>
public static class NativeDeckAnalyticsCharts
{
    public sealed record Bar(string Label, int Count, int Maximum, Brush Fill);
    public sealed record Series(
        IReadOnlyList<Bar> Curve,
        IReadOnlyList<Bar> Identity,
        IReadOnlyList<Bar> Types);

    private static Brush FrozenBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }

    private static IReadOnlyList<Bar> Build(params (string Label, int Count, string Hex)[] values)
    {
        if (values.Any(x => x.Count < 0))
            throw new InvalidDataException("Analytics contains a negative count.");
        int maximum = Math.Max(1, values.Length == 0 ? 0 : values.Max(x => x.Count));
        return values.Select(x => new Bar(x.Label, x.Count, maximum,
            FrozenBrush(x.Hex))).ToArray();
    }

    public static Series FromReport(NativeDeckAnalytics.Report report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Curve.Count != 8)
            throw new InvalidDataException("Mana curve must have eight buckets.");
        var curve = Build(Enumerable.Range(0, 8)
            .Select(i => (i == 7 ? "7+" : i.ToString(),
                report.Curve[i], "#967FD4")).ToArray());

        int Count(string key) => report.Identity.GetValueOrDefault(key);
        var identity = Build(
            ("White", Count("W"), "#EBD8A4"),
            ("Blue", Count("U"), "#6CAFE0"),
            ("Black", Count("B"), "#AA90C1"),
            ("Red", Count("R"), "#D96C64"),
            ("Green", Count("G"), "#70B992"),
            ("Multi", Count("Multicolor"), "#D5AD65"),
            ("Colorless", Count("Colorless"), "#A8B2C2"));

        var types = Build(
            ("Creatures", report.Creatures, "#6FAFC2"),
            ("Lands", report.Lands, "#70AB8E"),
            ("Instants", report.Instants, "#9A8DE2"),
            ("Sorceries", report.Sorceries, "#BA88C9"),
            ("Artifacts", report.Artifacts, "#A8AFBD"),
            ("Enchantments", report.Enchantments, "#E1B36A"),
            ("Planeswalkers", report.Planeswalkers, "#D98982"),
            ("Other", report.Other, "#7D92A7"));
        return new Series(curve, identity, types);
    }
}
