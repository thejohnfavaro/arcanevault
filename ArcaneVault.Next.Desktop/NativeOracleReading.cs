using System.Windows;
using System.Windows.Documents;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Consistent read-only Oracle presentation for Deck Library and EDH Lab.
/// Uses the same official Scryfall mana-symbol control as Card Details.
/// Never interprets rules text or changes card/deck data.
/// </summary>
public static class NativeOracleReading
{
    public static FlowDocument Build(string? oracle, double fontSize = 13)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontSize = fontSize,
            LineHeight = fontSize + 7
        };
        foreach (string line in (oracle ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 6) };
            foreach (var segment in NativeOracleSymbolTokenizer.Split(line))
            {
                if (segment.IsSymbol && segment.Text.Length >= 3)
                {
                    var glyph = new NativeManaSymbol
                    {
                        Symbol = segment.Text[1..^1],
                        Width = fontSize + 5,
                        Height = fontSize + 5
                    };
                    paragraph.Inlines.Add(new InlineUIContainer(glyph)
                    {
                        BaselineAlignment = BaselineAlignment.Center
                    });
                }
                else
                    paragraph.Inlines.Add(new Run(segment.Text));
            }
            document.Blocks.Add(paragraph);
        }
        return document;
    }
}
