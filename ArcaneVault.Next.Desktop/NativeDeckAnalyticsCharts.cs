using ArcaneVault.Next.Collection;
using System.Windows.Media;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// WPF adapter for tested, framework-independent chart metrics.
/// Bars are immutable; colors are frozen before binding to WPF controls.
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

    private static IReadOnlyList<Bar> WpfBars(IReadOnlyList<NativeDeckAnalyticsSeries.Bar> input) =>
        input.Select(x => new Bar(x.Label, x.Count, x.Maximum, FrozenBrush(x.HexColor)))
            .ToArray();

    public static Series FromReport(NativeDeckAnalytics.Report report)
    {
        var values = NativeDeckAnalyticsSeries.FromReport(report);
        return new Series(WpfBars(values.Curve), WpfBars(values.Identity),
            WpfBars(values.Types));
    }
}
