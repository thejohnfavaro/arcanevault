using ArcaneVault.Next.Collection;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ArcaneVault.Next.Desktop;

/// <summary>Native WPF graph for actual observed valuation days only.
 /// No calculated historical extrapolation, no backing network requests.</summary>
internal static class NativeValueHistoryChart
{
    private static readonly Brush Muted = BrushFrom("#C5CEDE");
    private static readonly Brush Gold = BrushFrom("#E5BF79");
    private static readonly Brush Purple = BrushFrom("#B59BFC");
    private static Brush BrushFrom(string code) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(code));

    private static void Label(Canvas canvas, string value, double left,
        double top, Brush color, double size = 10)
    {
        var label = new TextBlock
        {
            Text = value, Foreground = color, FontSize = size,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        canvas.Children.Add(label);
    }

    public static void Draw(Canvas canvas,
        IReadOnlyList<NativeValueHistory.Point> points,
        string heading)
    {
        if (canvas is null) return;
        canvas.Children.Clear();
        double width = Math.Max(150, canvas.ActualWidth);
        double height = Math.Max(116, canvas.ActualHeight);
        Label(canvas, heading.ToUpperInvariant(), 12, 7, Gold, 11);
        if (points.Count < 2)
        {
            Label(canvas, "History begins with the first priced day", 12,
                Math.Max(39, height / 2 - 12), Muted, 12);
            Label(canvas, "Previous values have not been reconstructed", 12,
                Math.Max(57, height / 2 + 7), Muted, 10);
            return;
        }
        double left = 12, right = width - 12, top = 43, bottom = height - 25;
        decimal lowest = points.Min(p => p.Usd);
        decimal highest = points.Max(p => p.Usd);
        decimal extent = highest > lowest ? highest - lowest : 1m;
        int dayRange = Math.Max(1, points[^1].Day.DayNumber - points[0].Day.DayNumber);
        var line = new Polyline
        {
            Stroke = Purple, StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false
        };
        foreach (var point in points)
        {
            double x = left + (right - left) *
                (point.Day.DayNumber - points[0].Day.DayNumber) / dayRange;
            double y = bottom - (bottom - top) *
                (double)((point.Usd - lowest) / extent);
            line.Points.Add(new Point(x, y));
        }
        canvas.Children.Add(new Line
        {
            X1 = left, X2 = right, Y1 = bottom, Y2 = bottom,
            Stroke = BrushFrom("#38465D"), StrokeThickness = 1
        });
        canvas.Children.Add(line);
        Point last = line.Points[^1];
        var circle = new Ellipse
        {
            Width = 7, Height = 7, Fill = Gold,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(circle, last.X - 3.5);
        Canvas.SetTop(circle, last.Y - 3.5);
        canvas.Children.Add(circle);
        Label(canvas, points[0].Day.ToString("yyyy-MM-dd",
            CultureInfo.InvariantCulture), left, height - 20, Muted);
        Label(canvas, points[^1].Day.ToString("yyyy-MM-dd",
            CultureInfo.InvariantCulture), Math.Max(left, right - 72),
            height - 20, Muted);
        Label(canvas, highest.ToString("C2",
            CultureInfo.GetCultureInfo("en-US")), left, 26, Muted);
    }
}
