using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Produces a native PNG for Stream Deck hardware instead of nesting artwork
/// data URIs in an SVG. All WPF rendering happens on a dedicated STA thread.
/// Images are presentation-only; the deck and collection are never modified.
/// </summary>
internal static class NativeStreamDeckTileRenderer
{
    public static Task<byte[]> DrawAsync(string name, int wins, int losses,
        byte[]? commanderArt, CancellationToken cancellation = default,
        bool fullCardFallback = false)
    {
        var completion = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                if (cancellation.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellation);
                    return;
                }
                completion.TrySetResult(Draw(name, wins, losses, commanderArt,
                    fullCardFallback));
            }
            catch (Exception ex) { completion.TrySetException(ex); }
        });
        thread.Name = "Arcane Vault Stream Deck tile";
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static byte[] Draw(string name, int wins, int losses, byte[]? art,
        bool fullCardFallback)
    {
        const double size = 144;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(25, 33, 47)),
                null, new Rect(0, 0, size, size));
            if (art is { Length: > 0 })
            {
                using var stream = new MemoryStream(art, writable: false);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                var brush = new ImageBrush(image)
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };
                // A full card image is only a fallback. Crop to its
                // illustration rectangle rather than shrinking the entire
                // frame, rules box and title into the Stream Deck tile.
                if (fullCardFallback)
                {
                    brush.ViewboxUnits = BrushMappingMode.RelativeToBoundingBox;
                    brush.Viewbox = new Rect(0.09, 0.115, 0.82, 0.43);
                }
                dc.DrawRectangle(brush, null, new Rect(0, 0, size, size));
            }
            var shade = new LinearGradientBrush(
                Color.FromArgb(165, 0, 0, 0),
                Color.FromArgb(236, 0, 0, 0), 90);
            dc.DrawRectangle(shade, null, new Rect(0, 0, size, size));
            void Text(string value, double y, double pt, Brush color)
            {
                var typeface = new Typeface(
                    new FontFamily("Segoe UI"), FontStyles.Normal,
                    FontWeights.Bold, FontStretches.Normal);
                var text = new FormattedText(value,
                    CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, pt, color, 1.0)
                {
                    MaxTextWidth = 132,
                    MaxTextHeight = 36,
                    Trimming = TextTrimming.CharacterEllipsis,
                    TextAlignment = TextAlignment.Center
                };
                dc.DrawText(text, new Point(6, y));
            }
            Text(name, 10, 13, Brushes.White);
            Text(wins.ToString(CultureInfo.InvariantCulture) + " - " +
                losses.ToString(CultureInfo.InvariantCulture), 65, 34,
                Brushes.White);
            string caption = wins + losses > 0
                ? Math.Round((double)wins * 100 / (wins + losses),
                    MidpointRounding.AwayFromZero)
                    .ToString(CultureInfo.InvariantCulture) + "% WIN"
                : "NO MATCHES";
            Text(caption, 113, 15,
                new SolidColorBrush(Color.FromRgb(162, 235, 179)));
        }
        var output = new RenderTargetBitmap(144, 144, 96, 96,
            PixelFormats.Pbgra32);
        output.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(output));
        using var result = new MemoryStream();
        encoder.Save(result);
        return result.ToArray();
    }
}
