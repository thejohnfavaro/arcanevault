using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// On-screen-only card image. An item is loaded by WPF's virtualized list,
/// which triggers a request through the existing bounded HTTPS Scryfall cache.
/// Unloading/recycling a row cancels the request. At most four images download
/// at once; no images are fetched for off-screen rows or ordinary text views.
/// </summary>
public sealed class NativeCardArtImage : Image
{
    private static readonly SemaphoreSlim LoadingGate = new(4, 4);
    private CancellationTokenSource? current;
    private int version;

    public static readonly DependencyProperty ArtUrlProperty =
        DependencyProperty.Register(nameof(ArtUrl), typeof(string),
            typeof(NativeCardArtImage),
            new PropertyMetadata("", OnArtUrlChanged));

    public string ArtUrl
    {
        get => (string)GetValue(ArtUrlProperty);
        set => SetValue(ArtUrlProperty, value);
    }

    // Increase decode resolution for wide set banners without forcing every
    // catalog card thumbnail to allocate a large bitmap.
    public static readonly DependencyProperty DecodeWidthProperty =
        DependencyProperty.Register(nameof(DecodeWidth), typeof(int),
            typeof(NativeCardArtImage), new PropertyMetadata(240, OnArtUrlChanged));

    public int DecodeWidth
    {
        get => (int)GetValue(DecodeWidthProperty);
        set => SetValue(DecodeWidthProperty, value);
    }

    public NativeCardArtImage()
    {
        Loaded += (_, _) => Start();
        Unloaded += (_, _) => Stop();
        Stretch = System.Windows.Media.Stretch.Uniform;
        SnapsToDevicePixels = true;
    }

    private static void OnArtUrlChanged(DependencyObject value,
        DependencyPropertyChangedEventArgs change)
    {
        var card = (NativeCardArtImage)value;
        if (card.IsLoaded) card.Start();
        else card.Source = null;
    }

    private void Stop()
    {
        version++;
        current?.Cancel();
        current = null;
        Source = null;
    }

    private async void Start()
    {
        Stop();
        if (!IsLoaded || !NativeCardArtwork.IsTrustedArtworkUrl(ArtUrl)) return;
        var request = new CancellationTokenSource();
        current = request;
        var serial = version;
        bool acquired = false;
        try
        {
            await LoadingGate.WaitAsync(request.Token);
            acquired = true;
            var bytes = await NativeCardArtwork.LoadAsync(ArtUrl, request.Token);
            if (bytes is null || bytes.Length == 0 ||
                request.IsCancellationRequested || serial != version || !IsLoaded)
                return;

            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = Math.Clamp(DecodeWidth, 96, 800); // Limit thumbnail and banner allocations.
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            if (serial == version && !request.IsCancellationRequested && IsLoaded)
                Source = image;
        }
        catch (OperationCanceledException) { }
        catch (Exception) { /* Missing art leaves card text usable. */ }
        finally
        {
            if (acquired) LoadingGate.Release();
        }
    }
}
