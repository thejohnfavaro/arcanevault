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
        if (!IsLoaded) return;
        if (!NativeCardArtwork.IsTrustedArtworkUrl(ArtUrl))
        {
            ToolTip = "No supported official artwork URL was resolved for this card. " +
                "The selected deck art is still saved.";
            return;
        }
        var request = new CancellationTokenSource();
        current = request;
        var serial = version;
        bool acquired = false;
        string artUrl = ArtUrl;
        try
        {
            await LoadingGate.WaitAsync(request.Token);
            acquired = true;
            // One retry covers transient CDN failures; corrupt cached images
            // are evicted and fetched again instead of leaving a blank cover.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                request.Token.ThrowIfCancellationRequested();
                if (serial != version || !IsLoaded) return;
                try
                {
                    var bytes = await NativeCardArtwork.LoadAsync(artUrl, request.Token);
                    if (bytes is null || bytes.Length == 0)
                        throw new IOException("Artwork download returned no image.");
                    using var stream = new MemoryStream(bytes);
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = Math.Clamp(DecodeWidth, 96, 800);
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    if (serial == version && !request.IsCancellationRequested && IsLoaded)
                    {
                        Source = image;
                        ToolTip = null;
                    }
                    return;
                }
                catch (OperationCanceledException) when (request.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (ex is IOException or HttpRequestException or
                    TimeoutException or System.Runtime.InteropServices.COMException or
                    System.NotSupportedException or System.IO.FileFormatException)
                {
                    if (request.IsCancellationRequested || serial != version || !IsLoaded)
                        return;
                    if (attempt == 1)
                    {
                        ToolTip = "Official Scryfall artwork could not load: " +
                            ex.Message + ". The saved card selection is preserved.";
                        System.Diagnostics.Debug.WriteLine(
                            "Arcane Vault deck artwork failed: " + ex);
                        return;
                    }
                    // Removing a disposable local image on any artwork error
                    // is safe. The next attempt uses the same official URL.
                    NativeCardArtwork.InvalidateCachedImage(artUrl);
                    await Task.Delay(800, request.Token);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (serial == version && IsLoaded)
            {
                ToolTip = "Official Scryfall artwork could not load: " +
                    ex.Message + ". The saved card selection is preserved.";
                System.Diagnostics.Debug.WriteLine(
                    "Arcane Vault deck artwork failed: " + ex);
            }
        }
        finally
        {
            if (acquired) LoadingGate.Release();
        }
    }
}
