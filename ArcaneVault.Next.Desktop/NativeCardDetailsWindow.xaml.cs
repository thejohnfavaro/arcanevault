using ArcaneVault.Next.Collection;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ArcaneVault.Next.Desktop;

public partial class NativeCardDetailsWindow : Window
{
    private readonly CancellationTokenSource artworkCancellation = new();
    private readonly string? catalogPath;
    private readonly string? nativePath;
    private readonly string? selectedSet;
    private readonly string? selectedCollector;
    private readonly string? defaultArtworkUrl;
    private readonly bool arenaScope;
    private int artworkVersion;
    private bool bookmarkBusy;
    private bool bookmarkSaved;

    public bool ShortlistChanged { get; private set; }

    /// <summary>Set only after the gallery's SHA-guarded native write succeeds.</summary>
    public bool PreferenceChanged { get; private set; }

    public NativeCardDetailsWindow(string name, string ownership,
        CollectionCatalogReview.CardDetails? info,
        string? catalogPath = null, string? nativePath = null,
        string? selectedSet = null, string? selectedCollector = null,
        bool arenaScope = false, string? selectedArtworkUrl = null)
    {
        this.catalogPath = catalogPath;
        this.nativePath = nativePath;
        this.selectedSet = selectedSet;
        this.selectedCollector = selectedCollector;
        this.arenaScope = arenaScope;
        defaultArtworkUrl = !string.IsNullOrWhiteSpace(selectedArtworkUrl)
            ? selectedArtworkUrl : info?.ArtworkUrl;
        InitializeComponent();
        OpenArtworkGalleryButton.IsEnabled = !string.IsNullOrWhiteSpace(catalogPath);
        BookmarkCardButton.IsEnabled = !string.IsNullOrWhiteSpace(nativePath) &&
            File.Exists(nativePath);
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
            string rarity = string.IsNullOrWhiteSpace(info.Rarity)
                ? "Unknown rarity"
                : System.Globalization.CultureInfo.InvariantCulture.TextInfo
                    .ToTitleCase(info.Rarity.Replace('_', ' '));
            CardMetadata.Text =
                $"{info.TypeLine}\n" +
                $"{rarity}  ·  Mana value {info.ManaValue:G}\n" +
                $"Color identity: {(string.IsNullOrWhiteSpace(info.ColorIdentity) ? "Colorless" : info.ColorIdentity)}";
            OracleText.Text = info.OracleText;
        }
        // Convert cached Scryfall format status codes to readable rows without
        // changing the SQLite catalog, deck validator, or stored JSON.
        var formatLegality = NativeFormatLegality.Parse(info?.Legalities);
        LegalitySummary.Text = "FORMAT LEGALITIES  ·  " + formatLegality.Summary;
        LegalityRows.ItemsSource = formatLegality.Rows;
        LegalityNotice.Text = formatLegality.Available
            ? "Cached Scryfall snapshot · current format bans may have changed"
            : "No usable format legalities in the local catalog. Refresh official card data to try again.";

        ArtworkStatus.Text = "No cached artwork yet. Card details remain available offline.";
        Loaded += async (_, _) =>
        {
            _ = RefreshBookmarkAsync();
            await ShowPreferredArtworkAsync();
        };
        Closed += (_, _) => artworkCancellation.Cancel();
    }

    private async Task RefreshBookmarkAsync()
    {
        if (string.IsNullOrWhiteSpace(nativePath) ||
            !File.Exists(nativePath))
        {
            BookmarkCardButton.IsEnabled = false;
            BookmarkStatus.Text =
                "Start a native collection before saving a shortlist bookmark.";
            return;
        }
        try
        {
            var names = await Task.Run(() =>
                NativeWorkspaceSafety.ListShortlist(nativePath));
            if (artworkCancellation.IsCancellationRequested) return;
            bookmarkSaved = names.Any(n => n.Equals(
                CardName.Text, StringComparison.OrdinalIgnoreCase));
            BookmarkCardButton.Content = bookmarkSaved
                ? "★  Remove from My Shortlist" : "☆  Bookmark in My Shortlist";
            BookmarkCardButton.IsEnabled = !bookmarkBusy;
            BookmarkStatus.Text = bookmarkSaved
                ? "Saved as a planning bookmark; this does not mean you own the card."
                : "A shortlist bookmark does not change your collection.";
        }
        catch (Exception ex)
        {
            if (artworkCancellation.IsCancellationRequested) return;
            BookmarkCardButton.IsEnabled = false;
            BookmarkStatus.Text = "Shortlist unavailable: " + ex.Message;
        }
    }

    private async void OnBookmarkClicked(object sender, RoutedEventArgs e)
    {
        if (bookmarkBusy || artworkCancellation.IsCancellationRequested ||
            string.IsNullOrWhiteSpace(nativePath)) return;
        bookmarkBusy = true;
        BookmarkCardButton.IsEnabled = false;
        try
        {
            bool next = await Task.Run(() =>
            {
                var workspace = NativeWorkspace.Open(nativePath);
                var names = NativeWorkspaceSafety.ListShortlist(nativePath);
                bool isSaved = names.Any(n => n.Equals(
                    CardName.Text, StringComparison.OrdinalIgnoreCase));
                NativeWorkspaceSafety.SetShortlistCard(
                    nativePath, CardName.Text, !isSaved, workspace.BackupSha256);
                return !isSaved;
            });
            if (artworkCancellation.IsCancellationRequested) return;
            bookmarkSaved = next;
            ShortlistChanged = true;
            BookmarkStatus.Text = next
                ? "Bookmarked with a full recovery point. Ownership unchanged."
                : "Bookmark removed with a full recovery point. Ownership unchanged.";
        }
        catch (Exception ex)
        {
            if (!artworkCancellation.IsCancellationRequested)
                MessageBox.Show(this,
                    "Could not change the bookmark:\n" + ex.Message +
                    "\n\nIf another window modified the native collection, reopen Card Details.",
                    "My Shortlist", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            bookmarkBusy = false;
            if (!artworkCancellation.IsCancellationRequested)
                await RefreshBookmarkAsync();
        }
    }

    private async Task ShowPreferredArtworkAsync()
    {
        string? url = defaultArtworkUrl;
        bool chosen = false;
        if (!string.IsNullOrWhiteSpace(nativePath))
        {
            try
            {
                var preference = await Task.Run(() =>
                    NativeWorkspaceSafety.GetPreferredArtwork(nativePath, CardName.Text));
                if (artworkCancellation.IsCancellationRequested) return;
                bool allowed = preference is not null &&
                    NativeCardArtwork.IsTrustedArtworkUrl(preference.Image);
                if (allowed && arenaScope)
                {
                    // Preferred artwork can originate from the physical gallery.
                    // Only apply it here when its exact print is Arena-available.
                    allowed = !string.IsNullOrWhiteSpace(catalogPath) &&
                        NativePrintingArtwork.Read(catalogPath, CardName.Text, 400,
                            arenaOnly: true).Printings.Any(p =>
                                p.Id == preference!.Id && p.ImageUrl == preference.Image);
                }
                if (allowed)
                {
                    url = preference.Image;
                    chosen = true;
                }
            }
            catch (Exception)
            {
                // An unavailable or changing workspace must never prevent the
                // cached official default artwork and text from opening.
            }
        }
        if (artworkCancellation.IsCancellationRequested) return;
        int version = ++artworkVersion;
        await LoadArtworkAsync(url, version);
        if (artworkCancellation.IsCancellationRequested || version != artworkVersion) return;
        if (chosen && CardArtwork.Visibility == Visibility.Visible)
            ArtworkStatus.Text = "Saved preferred printing artwork · cached for offline use.";
    }

    private async void OnOpenArtworkGalleryClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(catalogPath)) return;
        OpenArtworkGalleryButton.IsEnabled = false;
        try
        {
            var dialog = new NativePrintingGalleryWindow(CardName.Text,
                catalogPath, nativePath, selectedSet, selectedCollector,
                arenaOnly: arenaScope)
            { Owner = this };
            if (dialog.ShowDialog() == true && dialog.PreferenceChanged)
            {
                PreferenceChanged = true;
                await ShowPreferredArtworkAsync();
            }
        }
        catch (Exception ex)
        {
            ArtworkStatus.Text = "Printing gallery could not open: " + ex.Message;
        }
        finally
        {
            if (!artworkCancellation.IsCancellationRequested)
                OpenArtworkGalleryButton.IsEnabled = true;
        }
    }

    private async Task LoadArtworkAsync(string? url, int version)
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
            if (artworkCancellation.IsCancellationRequested || version != artworkVersion) return;
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
