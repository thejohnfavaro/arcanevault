using ArcaneVault.Next.Collection;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ArcaneVault.Next.Desktop;

public partial class NativeCardDetailsWindow : Window
{
    private readonly CancellationTokenSource artworkCancellation = new();

    public NativeCardDetailsWindow(string name, string ownership,
        CollectionCatalogReview.CardDetails? info)
    {
        InitializeComponent();
        CardName.Text = name;
        Ownership.Text = ownership;
        if (info is null)
        {
            CardMetadata.Text =
                "No local card metadata available — download or open the Scryfall catalog.";
            OracleText.Text =
                "Card name and owned quantities remain in your collection. " +
                "No catalog metadata is currently loaded.";
        }
        else
        {
            CardMetadata.Text =
                $"{info.TypeLine}  ·  {info.Rarity}  ·  Mana value {info.ManaValue:G}  · " +
                $"Color identity: {(string.IsNullOrEmpty(info.ColorIdentity) ? "Colorless" : info.ColorIdentity)}" +
                $"\nCached legalities: {info.Legalities}";
            OracleText.Text = info.OracleText;
        }
        ArtworkStatus.Text = "No cached artwork yet. Card details remain available offline.";
        Loaded += async (_, _) => await LoadArtworkAsync(info?.ArtworkUrl);
        Closed += (_, _) => artworkCancellation.Cancel();
    }

    private async Task LoadArtworkAsync(string? url)
    {
        if (!NativeCardArtwork.IsTrustedArtworkUrl(url))
        {
            ArtworkStatus.Text = "No official Scryfall artwork URL in this local catalog.";
            return;
        }
        ArtworkStatus.Text = "Loading artwork from local cache or official Scryfall CDN…";
        try
        {
            var bytes = await NativeCardArtwork.LoadAsync(url, artworkCancellation.Token);
            if (artworkCancellation.IsCancellationRequested) return;
            if (bytes is null)
            {
                ArtworkStatus.Text =
                    "Artwork unavailable. Card text and ownership are unaffected.";
                return;
            }
            using var input = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = input;
            bitmap.EndInit();
            bitmap.Freeze();
            CardArtwork.Source = bitmap;
            CardArtwork.Visibility = Visibility.Visible;
            ArtworkFallback.Visibility = Visibility.Collapsed;
            ArtworkStatus.Text = "Card art preview · cached locally for offline use.";
        }
        catch (OperationCanceledException) { /* Closing details stops pending download. */ }
        catch (Exception)
        {
            if (!artworkCancellation.IsCancellationRequested)
                ArtworkStatus.Text =
                    "Artwork could not load. Local card details remain accessible.";
        }
    }
}
