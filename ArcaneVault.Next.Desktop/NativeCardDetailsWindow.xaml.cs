using ArcaneVault.Next.Collection;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
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
    private string oraclePlainText = "";

    // Match the monitor containing the owner, not necessarily the primary
    // display. Windows reports physical work-area pixels; convert to DIPs.
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor, Work;
        public int Flags;
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    private static (double Width, double Height) WorkingSize(Window? owner)
    {
        if (owner is not null)
        {
            nint handle = new WindowInteropHelper(owner).Handle;
            if (handle != 0)
            {
                nint monitor = MonitorFromWindow(handle, 2);
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (monitor != 0 && GetMonitorInfo(monitor, ref info))
                {
                    Matrix fromDevice = PresentationSource.FromVisual(owner)?
                        .CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
                    return ((info.Work.Right - info.Work.Left) * fromDevice.M11,
                        (info.Work.Bottom - info.Work.Top) * fromDevice.M22);
                }
            }
        }
        Rect fallback = SystemParameters.WorkArea;
        return (fallback.Width, fallback.Height);
    }

    private void FitInitialWindow()
    {
        var area = WorkingSize(Owner);
        var fit = NativeCardDetailsLayout.InitialSize(area.Width, area.Height);
        MinWidth = fit.MinWidth;
        MinHeight = fit.MinHeight;
        Width = fit.Width;
        Height = fit.Height;
        MaxWidth = Math.Max(fit.Width, area.Width);
        MaxHeight = Math.Max(fit.Height, area.Height);
    }

    private void AdaptContentHeight()
    {
        if (CardContentGrid is null || CardArtFrame is null ||
            CardMetadataPanel is null) return;
        double space = CardContentGrid.ActualHeight;
        if (space <= 0) return;
        double artwork = NativeCardDetailsLayout.ArtworkHeight(space);
        if (Math.Abs(CardArtFrame.Height - artwork) > 1)
            CardArtFrame.Height = artwork;
        // A long legality list must never consume the Oracle-text row.
        CardMetadataPanel.MaxHeight = Math.Clamp(space - 205, 100, 230);
    }

    private void RenderOracleText(string? original)
    {
        oraclePlainText = original ?? "";
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontSize = 13.5,
            LineHeight = 20
        };
        foreach (string line in oraclePlainText.Replace("\r\n", "\n").Split('\n'))
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 3) };
            foreach (var segment in NativeOracleSymbolTokenizer.Split(line))
            {
                if (segment.IsSymbol && segment.Text.Length >= 3)
                {
                    var glyph = new NativeManaSymbol
                    {
                        Symbol = segment.Text[1..^1],
                        Width = 19, Height = 19,
                        Margin = new Thickness(1, 0, 1, 0)
                    };
                    paragraph.Inlines.Add(new InlineUIContainer(glyph)
                    {
                        BaselineAlignment = BaselineAlignment.Center
                    });
                }
                else paragraph.Inlines.Add(new Run(segment.Text));
            }
            doc.Blocks.Add(paragraph);
        }
        OracleText.Document = doc;
    }

    private void OnCopyOracleClicked(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(oraclePlainText); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not copy Oracle text: " + ex.Message,
                "Clipboard unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

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
        // Arena never falls back to generic card-level artwork: it might be
        // a paper-only printing, not a selectable Arena style.
        defaultArtworkUrl = arenaScope ? selectedArtworkUrl
            : (!string.IsNullOrWhiteSpace(selectedArtworkUrl)
                ? selectedArtworkUrl : info?.ArtworkUrl);
        InitializeComponent();
        OpenArtworkGalleryButton.IsEnabled = !string.IsNullOrWhiteSpace(catalogPath);
        BookmarkCardButton.IsEnabled = !string.IsNullOrWhiteSpace(nativePath) &&
            File.Exists(nativePath);
        CardName.Text = name;
        CardName.ToolTip = name;
        Ownership.Text = ownership;
        CardRarityBadge.Text = "RARITY UNKNOWN";
        CardRarityBadge.Tag = "";
        ColorIdentitySymbols.ItemsSource = Array.Empty<NativeManaIdentity.Color>();
        ColorIdentityLabel.Text = "Identity metadata unavailable";
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
            CardRarityBadge.Text = rarity.ToUpperInvariant();
            CardRarityBadge.Tag = (info.Rarity ?? "").Trim().ToLowerInvariant();
            CardMetadata.Text =
                $"{info.TypeLine}\n" +
                $"Mana value {info.ManaValue:G}";
            var colors = NativeManaIdentity.ParseColorIdentity(info.ColorIdentity);
            ColorIdentitySymbols.ItemsSource = colors;
            ColorIdentityLabel.Text = colors.Count == 0
                ? "Unknown identity"
                : string.Join(" · ", colors.Select(color => color.Name));
            RenderOracleText(info.OracleText);
        }
        if (info is null) RenderOracleText("");
        // Convert cached Scryfall format status codes to readable rows without
        // changing the SQLite catalog, deck validator, or stored JSON.
        var formatLegality = NativeFormatLegality.Parse(info?.Legalities);
        LegalitySummary.Text = "FORMAT LEGALITIES  ·  " + formatLegality.Summary;
        LegalityRows.ItemsSource = formatLegality.Rows;
        LegalityNotice.Text = formatLegality.Available
            ? "Cached Scryfall snapshot · current format bans may have changed"
            : "No usable format legalities in the local catalog. Refresh official card data to try again.";

        ArtworkStatus.Text = "No cached artwork yet. Card details remain available offline.";
        SourceInitialized += (_, _) => FitInitialWindow();
        SizeChanged += (_, _) => AdaptContentHeight();
        Loaded += async (_, _) =>
        {
            AdaptContentHeight();
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
        // The legacy preferredArtwork record stores a physical printing UUID,
        // not an MTG Arena cosmetic style. Never apply it in Arena scope.
        if (!arenaScope && !string.IsNullOrWhiteSpace(nativePath))
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
