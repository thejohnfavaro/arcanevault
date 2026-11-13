using ArcaneVault.Next.Collection;
using ArcaneVault.Next.EDHLab;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Read-only thumbnails for realized/visible EDH Mainboard Manage rows.
/// WPF ListBox recycling drives Loaded/Unloaded and DataContextChanged;
/// no background scan, bulk image prefetch or deck/ownership mutations.
/// </summary>
public partial class NativeDeckEditorWindow
{
    private sealed class MainboardArtRequest
    {
        public CancellationTokenSource? Active;
    }

    private readonly ConditionalWeakTable<Image, MainboardArtRequest>
        mainboardArtRequests = new();
    private readonly CancellationTokenSource mainboardArtLifetime = new();
    private readonly SemaphoreSlim mainboardArtGate = new(3, 3);
    private readonly Dictionary<string, BitmapSource?> mainboardArtCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> mainboardArtCacheOrder = new();
    private const int MainboardArtMemoryLimit = 128;
    private const int MainboardThumbDecodeWidth = 64;

    private void OnMainboardThumbnailLoaded(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, e.OriginalSource) && sender is Image image)
            RequestMainboardThumbnail(image);
    }

    private void OnMainboardThumbnailUnloaded(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, e.OriginalSource) && sender is Image image)
        {
            CancelMainboardThumbnail(image);
            image.Source = null; // Recycled ListBox rows must never retain stale art.
        }
    }

    private void OnMainboardThumbnailContextChanged(object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (sender is Image image)
            RequestMainboardThumbnail(image);
    }

    private void CancelMainboardThumbnail(Image image)
    {
        var state = mainboardArtRequests.GetValue(image,
            _ => new MainboardArtRequest());
        var previous = state.Active;
        state.Active = null;
        // The async owner disposes its CTS in finally after unwinding.
        previous?.Cancel();
    }

    private void RequestMainboardThumbnail(Image image)
    {
        CancelMainboardThumbnail(image);
        image.Source = null;
        if (!image.IsLoaded || mainboardArtLifetime.IsCancellationRequested ||
            edhLabCommander is null || string.IsNullOrWhiteSpace(catalogPath) ||
            image.DataContext is not EdhLabMainboardView.Row
                { IsHeader: false } row)
            return;

        if (mainboardArtCache.TryGetValue(row.Name, out var cached))
        {
            image.Source = cached;
            return;
        }

        var request = CancellationTokenSource.CreateLinkedTokenSource(
            mainboardArtLifetime.Token);
        mainboardArtRequests.GetValue(image,
            _ => new MainboardArtRequest()).Active = request;
        _ = LoadMainboardThumbnailAsync(image, row.Name, request);
    }

    private void RememberMainboardThumbnail(string name, BitmapSource? image)
    {
        if (mainboardArtCache.ContainsKey(name)) return;
        while (mainboardArtCache.Count >= MainboardArtMemoryLimit &&
               mainboardArtCacheOrder.Count > 0)
            mainboardArtCache.Remove(mainboardArtCacheOrder.Dequeue());
        mainboardArtCache[name] = image;
        mainboardArtCacheOrder.Enqueue(name);
    }

    private async Task LoadMainboardThumbnailAsync(Image target, string name,
        CancellationTokenSource request)
    {
        CancellationToken token = request.Token;
        bool acquired = false;
        try
        {
            // At most three requests across the editor, including SQLite
            // metadata reads. Offscreen/recycled rows cancel while queued.
            await mainboardArtGate.WaitAsync(token);
            acquired = true;
            if (!mainboardArtCache.TryGetValue(name, out BitmapSource? bitmap))
            {
                bool arena = OwnedPoolBox.SelectedIndex == 1;
                string url = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    var info = CollectionCatalogReview.ReadDetails(catalogPath!, name);
                    if (info is null) return "";
                    if (!arena) return info.ArtworkUrl;
                    // Arena rows require Arena-eligible art, rather than
                    // claiming the physical image is an owned digital style.
                    return NativePrintingArtwork.Read(catalogPath!, name,
                        limit: 1, cancellation: token, arenaOnly: true)
                        .Printings.FirstOrDefault()?.ImageUrl ?? "";
                }, token);
                token.ThrowIfCancellationRequested();

                if (NativeCardArtwork.IsTrustedArtworkUrl(url))
                {
                    // Existing bounded disk cache + official Scryfall-only URL
                    // policy. Never cache full-resolution bitmaps in memory.
                    byte[]? bytes = await NativeCardArtwork.LoadAsync(url, token);
                    token.ThrowIfCancellationRequested();
                    if (bytes is not null)
                        bitmap = await Task.Run(() =>
                        {
                            using var stream = new MemoryStream(bytes);
                            var thumbnail = new BitmapImage();
                            thumbnail.BeginInit();
                            thumbnail.CacheOption = BitmapCacheOption.OnLoad;
                            thumbnail.DecodePixelWidth = MainboardThumbDecodeWidth;
                            thumbnail.StreamSource = stream;
                            thumbnail.EndInit();
                            thumbnail.Freeze();
                            return thumbnail;
                        }, token);
                }
                token.ThrowIfCancellationRequested();
                // Cache missing-metadata fallbacks, but don't negatively
                // cache failed/transient downloads so they can retry later.
                if (!NativeCardArtwork.IsTrustedArtworkUrl(url) || bitmap is not null)
                    RememberMainboardThumbnail(name, bitmap);
            }

            if (!token.IsCancellationRequested && target.IsLoaded &&
                ReferenceEquals(mainboardArtRequests.GetValue(target,
                    _ => new MainboardArtRequest()).Active, request) &&
                target.DataContext is EdhLabMainboardView.Row
                    { IsHeader: false } current &&
                current.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                target.Source = bitmap;
        }
        catch (OperationCanceledException) { /* Offscreen row or editor closed. */ }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            SqliteException or InvalidDataException or ArgumentException or
            HttpRequestException or NotSupportedException or OverflowException)
        {
            // Optional artwork never blocks deck editing or changes data.
            // The styled row placeholder remains visible.
        }
        finally
        {
            if (acquired) mainboardArtGate.Release();
            var state = mainboardArtRequests.GetValue(target,
                _ => new MainboardArtRequest());
            if (ReferenceEquals(state.Active, request))
                state.Active = null;
            request.Dispose();
        }
    }
}
