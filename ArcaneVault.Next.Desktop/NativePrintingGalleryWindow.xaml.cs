using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Gallery reads only the native Scryfall catalog and verified workspace.
/// Selecting artwork never adds physical ownership, and a preference save
/// always goes through the optimistic-SHA SQLite recovery pipeline.
/// </summary>
public partial class NativePrintingGalleryWindow : Window
{
    private sealed record Choice(NativePrintingArtwork.Printing Printing,
        int PhysicalOwned, int ArenaOwned, bool IsPreferred, bool ArenaScope)
    {
        public string Title => ArenaScope ? "Base card artwork" : Printing.Title;
        public string Subtitle => ArenaScope
            ? "Arena card name · style inventory not synced"
            : Printing.Subtitle;
        public string OwnedLabel => ArenaScope
            ? (ArenaOwned > 0 ? "Owned card (by name)" : "Ownership unknown")
            : (PhysicalOwned > 0 ? $"Owned ×{PhysicalOwned}" : "Not verified owned");
        public string PreferredLabel => IsPreferred ? "★ Saved artwork" : "";
    }

    private readonly string cardName;
    private readonly string catalogPath;
    private readonly string? nativePath;
    private readonly string initialSet;
    private readonly string initialCollector;
    private readonly bool arenaOnly;
    private List<Choice> choices = new();
    private NativePrintingArtwork.Preferred? preferred;
    private string? expectedSha;
    private CancellationTokenSource? previewCancellation;
    private bool busy, closing, loading;

    public bool PreferenceChanged { get; private set; }

    public NativePrintingGalleryWindow(string name, string catalogPath,
        string? nativePath, string? selectedSet = null, string? selectedCollector = null,
        bool arenaOnly = false)
    {
        InitializeComponent();
        cardName = name;
        this.catalogPath = catalogPath;
        this.nativePath = nativePath;
        initialSet = selectedSet ?? "";
        initialCollector = selectedCollector ?? "";
        this.arenaOnly = arenaOnly;
        if (arenaOnly)
        {
            Title = "Arcane Vault — Arena Card & Styles";
            SavePreferenceButton.IsEnabled = false;
            ResetPreferenceButton.Visibility = Visibility.Collapsed;
            OwnedOnly.Visibility = Visibility.Collapsed;
        }
        CardHeading.Text = name;
        Loaded += async (_, _) => await LoadGalleryAsync();
        Closed += (_, _) =>
        {
            closing = true;
            previewCancellation?.Cancel();
        };
    }

    private async Task LoadGalleryAsync()
    {
        loading = true;
        try
        {
            var data = await Task.Run(() =>
            {
                var gallery = NativePrintingArtwork.Read(catalogPath, cardName, 400, arenaOnly: arenaOnly);
                NativeCatalogOwnership? ownership = null;
                NativePrintingArtwork.Preferred? saved = null;
                string? sha = null;
                if (!string.IsNullOrWhiteSpace(nativePath) && File.Exists(nativePath))
                {
                    var workspace = NativeWorkspace.Open(nativePath);
                    ownership = NativeCatalogOwnership.From(workspace.Collection);
                    saved = NativeWorkspaceSafety.GetPreferredArtwork(nativePath, cardName);
                    sha = workspace.BackupSha256;
                }
                var rows = gallery.Printings.Select(p =>
                {
                    var counts = ownership?.ForPrinting(p.Id.ToString("D"),
                        p.Name, p.SetCode, p.CollectorNumber, p.Paper);
                    return new Choice(p, counts?.VerifiedPhysical ?? 0,
                        counts?.Arena ?? 0, saved?.Id == p.Id, arenaOnly);
                }).ToList();
                return (rows, gallery.Truncated, saved, sha);
            });
            if (closing) return;
            choices = data.rows;
            preferred = data.saved;
            expectedSha = data.sha;

            bool anyOwned = !arenaOnly && choices.Any(x => x.PhysicalOwned > 0);
            bool preferredUnowned = choices.Any(x => x.IsPreferred && x.PhysicalOwned == 0);
            OwnedOnly.IsEnabled = anyOwned;
            OwnedOnly.IsChecked = anyOwned && !preferredUnowned;
            UpdateList();
            GalleryStatus.Text =
                (arenaOnly ? "Base artwork preview · Arena card styles not synced" :
                    $"{choices.Count:N0} cached Scryfall printings") +
                (data.Truncated ? " (first 400; refine the official catalog to see more)" : "") +
                (expectedSha is null
                    ? " · Browse only: create/open your native collection to save a preference."
                    : arenaOnly
                        ? " · Scryfall printing records do not establish available or owned Arena cosmetics."
                        : " · Only confirmed paper printing IDs count as owned.");
            ResetPreferenceButton.IsEnabled = preferred is not null && expectedSha is not null;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or Microsoft.Data.Sqlite.SqliteException or ArgumentException)
        {
            if (!closing)
                GalleryStatus.Text = "Printing gallery unavailable: " + ex.Message +
                    " The collection and catalog were not changed.";
        }
        finally
        {
            loading = false;
        }
    }

    private void UpdateList()
    {
        if (loading && choices.Count == 0) return;
        var selected = (PrintingsList.SelectedItem as Choice)?.Printing.Id;
        var filtered = (!arenaOnly && OwnedOnly.IsChecked == true
                ? choices.Where(x => x.PhysicalOwned > 0) : choices)
            .ToArray();
        PrintingsList.ItemsSource = filtered;
        Choice? initial = filtered.FirstOrDefault(x => x.Printing.Id == selected)
            ?? filtered.FirstOrDefault(x =>
                x.Printing.SetCode.Equals(initialSet, StringComparison.OrdinalIgnoreCase) &&
                x.Printing.CollectorNumber.Equals(initialCollector, StringComparison.OrdinalIgnoreCase))
            ?? filtered.FirstOrDefault(x => x.IsPreferred)
            ?? filtered.FirstOrDefault(x => x.PhysicalOwned > 0)
            ?? filtered.FirstOrDefault();
        if (initial is not null)
        {
            PrintingsList.SelectedItem = initial;
            PrintingsList.ScrollIntoView(initial);
        }
        else
        {
            ArtPreview.Source = null;
            ArtPreview.Visibility = Visibility.Collapsed;
            ArtPlaceholder.Visibility = Visibility.Visible;
            PreviewInfo.Text = arenaOnly
                ? "No Arena-available styles in the current catalog for this card."
                : "No verified owned printings in this catalog view.";
            SavePreferenceButton.IsEnabled = false;
        }
    }

    private void OnOwnedOnlyChanged(object sender, RoutedEventArgs e)
    {
        if (PrintingsList is null || loading) return;
        UpdateList();
    }

    private async void OnPrintingSelected(object sender, SelectionChangedEventArgs e)
    {
        if (closing) return;
        var choice = PrintingsList.SelectedItem as Choice;
        previewCancellation?.Cancel();
        var cts = new CancellationTokenSource();
        previewCancellation = cts;
        ArtPreview.Source = null;
        ArtPreview.Visibility = Visibility.Collapsed;
        ArtPlaceholder.Visibility = Visibility.Visible;
        SavePreferenceButton.IsEnabled = !arenaOnly && !busy && choice is not null &&
            expectedSha is not null &&
            NativeCardArtwork.IsTrustedArtworkUrl(choice.Printing.ImageUrl) &&
            !choice.IsPreferred;
        if (choice is null) return;
        PreviewInfo.Text = arenaOnly
            ? "Base card preview only. Cosmetic card-style availability and ownership are not imported from MTG Arena."
            : $"{choice.Printing.Title} · {choice.Printing.Subtitle}\n" +
              $"{choice.OwnedLabel}. Changing displayed art never changes inventory.";
        if (!NativeCardArtwork.IsTrustedArtworkUrl(choice.Printing.ImageUrl))
        {
            ArtPlaceholder.Text = "No supported official Scryfall artwork for this printing.";
            return;
        }
        ArtPlaceholder.Text = "Loading artwork…";
        try
        {
            byte[]? bytes = await NativeCardArtwork.LoadAsync(
                choice.Printing.ImageUrl, cts.Token);
            if (closing || cts.IsCancellationRequested ||
                !ReferenceEquals(previewCancellation, cts)) return;
            if (bytes is null)
            {
                ArtPlaceholder.Text = "Artwork unavailable; metadata remains accessible.";
                return;
            }
            using var input = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = input;
            bitmap.EndInit();
            bitmap.Freeze();
            ArtPreview.Source = bitmap;
            ArtPreview.Visibility = Visibility.Visible;
            ArtPlaceholder.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!cts.IsCancellationRequested && !closing)
                ArtPlaceholder.Text = "Artwork could not load; preference was not modified.";
        }
    }

    private async void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (arenaOnly || busy || expectedSha is null || nativePath is null ||
            PrintingsList.SelectedItem is not Choice selected) return;
        if (selected.PhysicalOwned == 0 &&
            NativeInAppDialog.Show(this,
                "This edition is not verified in your physical collection. " +
                "Do you still want to use its art for this card name?\n\n" +
                "This changes artwork only, never ownership, printing selection, or Set Progress.",
                "Choose artwork from an unowned printing?",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        busy = true;
        SavePreferenceButton.IsEnabled = false;
        ResetPreferenceButton.IsEnabled = false;
        try
        {
            var output = await Task.Run(() =>
                NativeWorkspaceSafety.SavePreferredArtwork(nativePath, catalogPath,
                    cardName, selected.Printing.Id, expectedSha));
            if (closing) return;
            expectedSha = output.Current.BackupSha256;
            PreferenceChanged = true;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            if (!closing)
                NativeInAppDialog.Show(this, "Artwork preference was not saved:\n" + ex.Message +
                    "\n\nIf your collection changed in another window, close and reopen Card Details.",
                    "Artwork not changed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            busy = false;
            if (!closing) SavePreferenceButton.IsEnabled = true;
        }
    }

    private async void OnResetClicked(object sender, RoutedEventArgs e)
    {
        if (arenaOnly || busy || expectedSha is null || nativePath is null || preferred is null) return;
        if (NativeInAppDialog.Show(this,
                "Reset this card to its default Scryfall artwork?\n\n" +
                "This removes only the saved display preference and preserves all ownership.",
                "Reset artwork preference", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        busy = true;
        ResetPreferenceButton.IsEnabled = false;
        SavePreferenceButton.IsEnabled = false;
        try
        {
            var output = await Task.Run(() =>
                NativeWorkspaceSafety.ResetPreferredArtwork(
                    nativePath, cardName, expectedSha));
            if (closing) return;
            expectedSha = output.Current.BackupSha256;
            PreferenceChanged = true;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            if (!closing)
                NativeInAppDialog.Show(this,
                    "Saved preference could not be reset:\n" + ex.Message,
                    "Artwork not changed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            busy = false;
            if (!closing) ResetPreferenceButton.IsEnabled = preferred is not null;
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
