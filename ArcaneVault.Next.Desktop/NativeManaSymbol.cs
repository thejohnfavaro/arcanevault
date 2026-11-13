using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Local, vector-drawn MTG mana color-identity indicators. No remote icon
/// requests, external font dependency, catalog edits or per-card image loads.
/// These represent COLOR IDENTITY, not the mana symbols in the casting cost.
/// </summary>
public sealed class NativeManaSymbol : Image
{
    public sealed record ManaIdentity(string Symbol, string Name);
    private static readonly string Order = "WUBRG";

    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(nameof(Symbol), typeof(string),
            typeof(NativeManaSymbol), new PropertyMetadata("C",
                (obj, _) => ((NativeManaSymbol)obj).Refresh()));

    public string Symbol
    {
        get => (string)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public NativeManaSymbol()
    {
        Stretch = Stretch.Uniform;
        SnapsToDevicePixels = true;
        Refresh();
    }

    public static IReadOnlyList<ManaIdentity> ParseColorIdentity(string? raw)
    {
        string value = (raw ?? "").Trim().ToUpperInvariant();
        if (value is "" or "C" or "COLORLESS" or "[]")
            return new[] { new ManaIdentity("C", "Colorless") };

        // Scryfall may return WUBRG, W,U or a JSON-like list. Reject arbitrary
        // prose instead of treating the letters in a word as a mana identity.
        string compact = new(value.Where(ch => Order.Contains(ch)).ToArray());
        if (!value.All(ch => Order.Contains(ch) ||
                char.IsWhiteSpace(ch) || ch is ',' or '[' or ']' or '"' or '\'' or '/')
            || compact.Length == 0)
            return Array.Empty<ManaIdentity>();
        return Order.Where(compact.Contains).Select(ch =>
            new ManaIdentity(ch.ToString(), NameFor(ch.ToString()))).ToArray();
    }

    public static string NameFor(string symbol) => symbol switch
    {
        "W" => "White",
        "U" => "Blue",
        "B" => "Black",
        "R" => "Red",
        "G" => "Green",
        "C" => "Colorless",
        _ => "Unknown"
    };

    private void Refresh()
    {
        string code = (Symbol ?? "").Trim().ToUpperInvariant();
        Source = CreateSymbol(code);
        ToolTip = NameFor(code) + " mana color";
        System.Windows.Automation.AutomationProperties.SetName(this,
            NameFor(code) + " color identity");
    }

    private static ImageSource CreateSymbol(string code)
    {
        var (background, ink, glyph) = code switch
        {
            // A sun disk, water drop, skull, fire and tree silhouette.
            "W" => ("#F7E9B8", "#524828",
                "M12,2 L13.5,6.4 17.5,3.8 16.5,8.3 21,8.5 17.5,11.2 21,15 16.4,14.9 17.3,19.5 13.3,16.7 12,22 10.6,16.8 6.6,19.5 7.6,14.9 3,15 6.5,11.2 3,8.5 7.5,8.3 6.5,3.8 10.5,6.4 Z"),
            "U" => ("#ACDCF4", "#175479",
                "M12,2 C10.2,7.7 4.4,11.2 4.4,15.5 C4.4,19.7 7.8,22 12,22 C16.2,22 19.6,19.7 19.6,15.5 C19.6,11.2 13.8,7.7 12,2 Z"),
            "B" => ("#BEC0C5", "#34373F",
                "M12,3 C6.6,3 3.4,7.2 3.4,12 C3.4,15.5 5.3,17.4 7.1,18.2 L7.1,21 L9.3,21 L9.3,19 L10.6,19 L10.6,21 L13.4,21 L13.4,19 L14.7,19 L14.7,21 L16.9,21 L16.9,18.2 C18.7,17.4 20.6,15.5 20.6,12 C20.6,7.2 17.4,3 12,3 Z"),
            "R" => ("#FFC4A4", "#8E3525",
                "M12,2 C13.6,6.9 17.4,7.8 17,12.4 C18.2,11.6 19.3,10.3 19.4,8.5 C23.4,15.5 19.1,22 12,22 C5,22 2.3,16.8 5.2,11.8 C5.5,15.2 7.7,15.5 8.8,15 C8.1,10.5 10.1,6.7 12,2 Z"),
            "G" => ("#BDE2B9", "#27643A",
                "M11,3 C6.8,2.9 5.4,5.9 6.6,8.2 C3.5,8.7 3.1,12.1 5,14.1 C3,17.9 6.1,20.7 9.9,18.9 L10,22 L14,22 L14.1,18.9 C18.1,20.8 21,17.8 19,14.1 C20.9,11.8 20.5,8.8 17.4,8.2 C18.6,5.9 17,3 13,3 L12,5 Z"),
            _ => ("#CBD1DB", "#48556A",
                "M12,2 L21,12 L12,22 L3,12 Z M12,7 L7.5,12 L12,17 L16.5,12 Z")
        };
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            Brush(background), new Pen(Brush("#4A4E58"), 0.75),
            new EllipseGeometry(new Point(12, 12), 11.5, 11.5)));
        group.Children.Add(new GeometryDrawing(Brush(ink), null, Geometry.Parse(glyph)));
        if (code == "W")
            group.Children.Add(new GeometryDrawing(Brush(background), null,
                new EllipseGeometry(new Point(12, 12), 3.3, 3.3)));
        if (code == "B")
        {
            group.Children.Add(new GeometryDrawing(Brush(background), null,
                new EllipseGeometry(new Point(8.5, 12.4), 2, 2.1)));
            group.Children.Add(new GeometryDrawing(Brush(background), null,
                new EllipseGeometry(new Point(15.5, 12.4), 2, 2.1)));
            group.Children.Add(new GeometryDrawing(Brush(background), null,
                Geometry.Parse("M12,14.2 L10.3,17 L13.7,17 Z")));
        }
        group.Freeze();
        var picture = new DrawingImage(group);
        picture.Freeze();
        return picture;
    }

    private static Brush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!);
        brush.Freeze();
        return brush;
    }
}
