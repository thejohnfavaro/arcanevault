using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Official Scryfall set SVG icon, downloaded once into a stable per-user
/// LocalAppData cache. Drawn as native WPF vector paths, never WebView/HTML.
/// Missing/unsupported symbols safely fall back to the three-letter set code.
/// </summary>
public sealed class NativeSetSymbol : Grid
{
    private static readonly HttpClient Http = CreateHttp();
    private static readonly SemaphoreSlim Gate = new(3, 3);
    private CancellationTokenSource? pending;
    private readonly Image icon = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock fallback = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        FontWeight = FontWeights.Bold,
        FontSize = 14,
        Foreground = Brushes.White,
        TextAlignment = TextAlignment.Center
    };

    public static readonly DependencyProperty SetCodeProperty =
        DependencyProperty.Register(nameof(SetCode), typeof(string), typeof(NativeSetSymbol),
            new PropertyMetadata("", (o, _) => ((NativeSetSymbol)o).Refresh()));

    public string SetCode
    {
        get => (string)GetValue(SetCodeProperty);
        set => SetValue(SetCodeProperty, value);
    }

    public NativeSetSymbol()
    {
        Children.Add(icon);
        Children.Add(fallback);
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => Cancel();
    }

    private static HttpClient CreateHttp()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(9) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ArcaneVaultNative/0.9 (set-icon-cache)");
        return client;
    }

    private void Cancel()
    {
        pending?.Cancel();
        pending?.Dispose();
        pending = null;
    }

    private async void Refresh()
    {
        Cancel();
        icon.Source = null;
        var code = (SetCode ?? "").Trim().ToLowerInvariant();
        fallback.Text = code.ToUpperInvariant();
        fallback.Visibility = Visibility.Visible;
        if (!IsLoaded || code.Length is < 2 or > 12 ||
            code.Any(ch => !char.IsAsciiLetterOrDigit(ch))) return;

        var cancellation = new CancellationTokenSource();
        pending = cancellation;
        try
        {
            var drawing = await ReadDrawingAsync(code, cancellation.Token);
            if (cancellation.IsCancellationRequested || !IsLoaded ||
                !ReferenceEquals(pending, cancellation) || drawing is null) return;
            icon.Source = drawing;
            fallback.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch (Exception) { /* Unknown set / unavailable connection: show code. */ }
    }

    private static async Task<ImageSource?> ReadDrawingAsync(
        string code, CancellationToken cancellation)
    {
        string directory = Path.Combine(
            ArcaneVault.Next.Collection.NativeWorkspace.RootDirectory,
            "cache", "set-symbols");
        string path = Path.Combine(directory, code + ".svg");
        string? xml = null;
        if (File.Exists(path))
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Length is > 0 and <= 128_000)
                    xml = await File.ReadAllTextAsync(path, cancellation);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        if (xml is null)
        {
            await Gate.WaitAsync(cancellation);
            try
            {
                using var response = await Http.GetAsync(
                    "https://svgs.scryfall.io/sets/" + code + ".svg",
                    HttpCompletionOption.ResponseHeadersRead, cancellation);
                if (response.StatusCode == HttpStatusCode.NotFound) return null;
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is > 128_000) return null;
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellation);
                if (bytes.Length is 0 or > 128_000) return null;
                xml = System.Text.Encoding.UTF8.GetString(bytes);
                // Parse before caching to avoid storing invalid SVG.
                if (ParseSvg(xml) is null) return null;
                try
                {
                    Directory.CreateDirectory(directory);
                    await File.WriteAllTextAsync(path, xml, cancellation);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            finally { Gate.Release(); }
        }
        cancellation.ThrowIfCancellationRequested();
        return ParseSvg(xml);
    }

    private static DrawingImage? ParseSvg(string svg)
    {
        // No DTD/entity resolution, scripts, external references or raster content.
        // Symbols returned by Scryfall use SVG paths. Other elements fail soft.
        if (svg.Length > 128_000) return null;
        var settings = new System.Xml.XmlReaderSettings
        {
            DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 128_000
        };
        using var reader = System.Xml.XmlReader.Create(new StringReader(svg), settings);
        var root = XDocument.Load(reader).Root;
        if (root is null || root.Name.LocalName != "svg") return null;
        var box = ((string?)root.Attribute("viewBox") ?? "").Split(
            new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (box.Length != 4 ||
            !double.TryParse(box[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !double.TryParse(box[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !double.TryParse(box[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ||
            !double.TryParse(box[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h) ||
            w <= 0 || h <= 0 || w > 10_000 || h > 10_000) return null;

        var group = new DrawingGroup();
        foreach (var node in root.Descendants().Where(e => e.Name.LocalName == "path").Take(64))
        {
            var data = (string?)node.Attribute("d");
            if (string.IsNullOrWhiteSpace(data) || data.Length > 25_000) continue;
            try
            {
                var geometry = Geometry.Parse(data);
                group.Children.Add(new GeometryDrawing(Brushes.White, null, geometry));
            }
            catch (FormatException) { }
        }
        if (group.Children.Count == 0) return null;
        // Fit source viewBox into one uniformly scaled vector coordinate space.
        group.Transform = new TranslateTransform(-x, -y);
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
