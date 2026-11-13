using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using ArcaneVault.Next.EDHLab;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

public partial class NativeDeckEditorWindow : Window
{
    private readonly string nativePath;
    private readonly string expectedSha;
    // On a confirmed save, carry the already fully verified replacement
    // back to the embedded library; do not reopen the whole collection.
    public NativeWorkspaceSafety.Replacement? SavedReplacement { get; private set; }
    private readonly NativeDeckEditing.DeckRow? original;
    private readonly NativeBackupView ownership;
    private readonly string? catalogPath;
    private bool busy;
    private readonly string? edhLabCommander;
    private readonly EdhLabFormat? edhLabFormat;
    private readonly string? edhLabColors;
    private EdhLabDraftCatalog? edhCatalog;
    private EdhLabDeckOptimizer.Report? lastOptimization;
    private EdhLabDeckHealth.Report? lastHealthReport;
    // Only the analysis pass is deferred while typing. Draft text, the
    // explicit save path and manual card editing remain synchronous.
    private System.Windows.Threading.DispatcherTimer? edhAnalysisTimer;
    private const int EdhAnalysisQuietMilliseconds = 220;
    private EdhLabSwapAdvisor.Report? lastSwapAdvice;
    private EdhLabSynergyPackages.Report? lastPackageReport;
    private EdhLabSynergyChains.Report? lastChainReport;
    private EdhLabThreeCardEngines.Report? lastEngineReport;
    private CancellationTokenSource? chainArtworkRequest;
    private int chainArtworkVersion;
    private bool updatingPackageTheme;

    private sealed record PackageDisplay(string Name, string Theme,
        string Group, string Kind, string Explanation, long Recorded, string Roles)
    {
        public string Display => $"[{Theme}] {Group}: {Name} · {Kind}";
    }

    private bool browsingOptimizationCandidates;
    // Rebinding the deck-aware Cards browser should not repeatedly fetch the
    // selected card's artwork or clear an intentional mainboard preview.
    private bool updatingOwnedBrowser;
    private bool applyingManaBalance;
    private int draftVersion;
    private int previewVersion;
    private CancellationTokenSource? previewRequest;
    private OwnedDisplay? previewedCard;
    private int previewedPool = -1;
    private sealed record OwnedDisplay(string Name, long Owned, string RoleHints = "",
        bool LabSuggestion = false, string SynergyExplanation = "", bool MechanicMatch = false,
        int InDraftCopies = 0, long Proxied = 0)
    {
        public string Display => LabSuggestion
            ? Name + (InDraftCopies > 0 ? $"  ·  In deck ×{InDraftCopies}" : "  ·  Not in deck") +
              (Owned > 0 ? $"  ·  {Owned:N0} recorded" : "  ·  not owned") +
              (Proxied > 0 ? $"  ·  Proxy ×{Proxied} (not valued)" : "") +
              (MechanicMatch ? "  ·  Mechanic match" : "") +
              (RoleHints.Length > 0 ? "  ·  " + RoleHints : "")
            : $"{Name}  ·  {Owned:N0} owned";
    }

    // TabControl may measure a TabItem's content at its desired full height.
    // Without an explicit viewport, expanding two tall sections can put
    // actions outside the visible editor instead of enabling scrolling.
    // Keep both analysis tabs bounded to the real center-pane height, and
    // recompute after window sizing, splitter moves and DPI changes.
    private void OnEdhWorkspaceLoaded(object sender, RoutedEventArgs e) =>
        ConstrainEdhWorkspaceScrollers();

    private void OnEdhWorkspaceSizeChanged(object sender,
        SizeChangedEventArgs e) => ConstrainEdhWorkspaceScrollers();

    private void ConstrainEdhWorkspaceScrollers()
    {
        if (EdhWorkspaceTabs is null ||
            EdhManaScroll is null || EdhImproveScroll is null)
            return;
        double available = EdhWorkspaceTabs.ActualHeight;
        if (!double.IsFinite(available) || available <= 0) return;

        // The selected TabItem's ActualHeight is its tab-strip header in the
        // stock WPF template. Reserve a little more room for border/padding,
        // and use the same viewport for both separately scrollable workspaces.
        double header = EdhCardsTab?.ActualHeight ?? 0;
        if (!double.IsFinite(header) || header is < 22 or > 72)
            header = 38;
        double viewport = Math.Max(72, available - header - 20);
        EdhManaScroll.MaxHeight = viewport;
        EdhImproveScroll.MaxHeight = viewport;
    }

    private EdhLabCardPresenceFilter SelectedEdhPresence =>
        (EdhCardPresenceBox?.SelectedItem as
            System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() switch
        {
            "InDeck" => EdhLabCardPresenceFilter.InDeck,
            "All" => EdhLabCardPresenceFilter.All,
            _ => EdhLabCardPresenceFilter.Available
        };

    private void SetOwnedBrowserRows(OwnedDisplay[] cards, string? status = null)
    {
        if (OwnedCardsList is null) return;
        string? previousName = (OwnedCardsList.SelectedItem as OwnedDisplay)?.Name;
        updatingOwnedBrowser = true;
        try
        {
            OwnedCardsList.ItemsSource = cards;
            if (previousName is not null)
                OwnedCardsList.SelectedItem = cards.FirstOrDefault(x =>
                    x.Name.Equals(previousName, StringComparison.OrdinalIgnoreCase));
        }
        finally { updatingOwnedBrowser = false; }
        if (edhLabCommander is not null && EdhBrowserStatus is not null)
        {
            EdhBrowserStatus.Visibility = Visibility.Visible;
            EdhBrowserStatus.Text = status ?? "";
        }
    }

    private void RefreshOwnedCards()
    {
        if (OwnedCardsList is null || OwnedPoolBox is null || OwnedSearchBox is null)
            return;
        if (edhLabCommander is not null)
        {
            if (edhCatalog is null)
            {
                SetOwnedBrowserRows([], "Waiting for the locally verified card catalog…");
                return;
            }

            // Build the unsaved mainboard quantity index once per refresh.
            // An unfinished/invalid deck edit must never display misleading
            // in-deck labels, permit stale suggestions, or rewrite draft text.
            Dictionary<string, int> counts;
            try
            {
                counts = NativeDeckEditing.ParseMainboard(MainboardBox.Text)
                    .ToDictionary(x => x.Name, x => x.Quantity,
                        StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is InvalidDataException or
                ArgumentException or OverflowException)
            {
                SetOwnedBrowserRows([], "Correct the mainboard to refresh deck status: " +
                    ex.Message);
                return;
            }
            var presence = SelectedEdhPresence;
            int InDeck(string name) => counts.GetValueOrDefault(name);
            bool MatchesPresence(string name, long owned) => presence switch
            {
                EdhLabCardPresenceFilter.Available =>
                    InDeck(name) == 0 && (owned > 0 || CountProxy(name) > 0),
                EdhLabCardPresenceFilter.InDeck => InDeck(name) > 0,
                _ => true
            };
            OwnedDisplay[] results;
            if (browsingOptimizationCandidates && lastOptimization is { CanBrowse: true })
            {
                results = lastOptimization.OwnedCandidates
                    .Where(candidate =>
                    {
                        var recorded = edhCatalog.Find(candidate.Name);
                        return MatchesPresence(candidate.Name,
                            recorded?.RecordedCopies ?? 0);
                    })
                    .Select(candidate =>
                    {
                        var recorded = edhCatalog.Find(candidate.Name);
                        var fit = edhCatalog.SynergyFor(candidate.Name);
                        return new OwnedDisplay(candidate.Name,
                            recorded?.RecordedCopies ?? 0,
                            candidate.RoleHints, LabSuggestion: true,
                            SynergyExplanation: fit?.Why ?? "",
                            MechanicMatch: fit?.Matches == true,
                            InDraftCopies: InDeck(candidate.Name),
                            Proxied: CountProxy(candidate.Name));
                    }).ToArray();
            }
            else
            {
                results = edhCatalog.Search(
                    OwnedSearchBox.Text, EdhRecordedOnly.IsChecked == true,
                    EdhRoleBox.SelectedItem is System.Windows.Controls.ComboBoxItem role
                        ? role.Tag?.ToString() : "", limit: 200,
                    synergyOnly: EdhSynergyOnly?.IsChecked == true,
                    prioritizeSynergy: EdhPrioritizeSynergy?.IsChecked == true,
                    mainboardCounts: counts, presence: presence,
                    proxyCounts: OwnedPoolBox.SelectedIndex == 1 ? null : proxyCounts)
                    .Select(x =>
                    {
                        var fit = edhCatalog.SynergyFor(x.Name);
                        return new OwnedDisplay(x.Name, x.RecordedCopies,
                            x.RoleHints, LabSuggestion: true,
                            SynergyExplanation: fit?.Why ?? "",
                            MechanicMatch: fit?.Matches == true,
                            InDraftCopies: InDeck(x.Name),
                            Proxied: CountProxy(x.Name));
                    }).ToArray();
            }

            string viewLabel = presence switch
            {
                EdhLabCardPresenceFilter.Available =>
                    "Available (owned or Proxy, not in deck)",
                EdhLabCardPresenceFilter.InDeck => "In deck",
                _ => "All eligible"
            };
            SetOwnedBrowserRows(results,
                $"{viewLabel} · {results.Length:N0} shown" +
                (results.Length == 200 ? " (first 200 matches)" : "") +
                $" · {counts.Count:N0} distinct mainboard names. " +
                "Counts reflect the unsaved draft; no inventory changes.");
            return;
        }
        var pool = OwnedPoolBox.SelectedIndex == 1
            ? NativeDeckOwnedPicker.Pool.Arena : NativeDeckOwnedPicker.Pool.Physical;
        SetOwnedBrowserRows(NativeDeckOwnedPicker.Search(
            ownership, pool, OwnedSearchBox.Text)
            .Select(x => new OwnedDisplay(x.Name, x.Owned,
                Proxied: CountProxy(x.Name))).ToArray());
    }

    private async void OnOwnedCardSelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Refreshing filters clears the ListBox selection. That should not
        // blank a deliberate mainboard card preview (or start an art fetch).
        if (updatingOwnedBrowser) return;
        if (OwnedCardsList?.SelectedItem is OwnedDisplay card)
            await ShowCardPreviewAsync(card);
    }

    private void OnMainboardSelectionChanged(object sender,
        RoutedEventArgs e) => PreviewMainboardSelection();

    private void OnMainboardGotKeyboardFocus(object sender,
        System.Windows.Input.KeyboardFocusChangedEventArgs e) =>
        PreviewMainboardSelection();

    private void OnMainboardMouseReleased(object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        // Mouse-up can precede the TextBox's final caret update. Resolve
        // the active line once the mouse selection has been committed.
        Dispatcher.BeginInvoke(new Action(PreviewMainboardSelection),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void PreviewMainboardSelection()
    {
        if (busy || MainboardBox is null || !MainboardBox.IsKeyboardFocusWithin ||
            OwnedPoolBox is null) return;
        string? name = NativeDeckEditing.PreviewNameAtCaret(
            MainboardBox.Text, MainboardBox.CaretIndex);
        // A blank/malformed line is not a card. Keep the previous preview
        // rather than showing unrelated art or discarding in-progress text.
        if (name is null) return;

        // Switching from browser selection to a deck line must not leave the
        // Add (+1) action targeting the previously selected browser card.
        OwnedCardsList?.UnselectAll();
        if (previewedCard?.Name.Equals(name,
                StringComparison.OrdinalIgnoreCase) == true &&
            previewedPool == OwnedPoolBox.SelectedIndex)
            return;

        var pool = OwnedPoolBox.SelectedIndex == 1
            ? NativeDeckOwnedPicker.Pool.Arena
            : NativeDeckOwnedPicker.Pool.Physical;
        long owned = NativeDeckOwnedPicker.CountOwned(ownership, pool, name);
        var catalogCard = edhCatalog?.Find(name);
        var fit = edhCatalog?.SynergyFor(name);
        var selected = new OwnedDisplay(name, owned,
            catalogCard?.RoleHints ?? "",
            LabSuggestion: edhLabCommander is not null && catalogCard is not null,
            SynergyExplanation: fit?.Why ?? "No verified commander mechanic match.",
            MechanicMatch: fit?.Matches == true,
            Proxied: CountProxy(name));
        _ = ShowCardPreviewAsync(selected);
    }

    private async Task ShowCardPreviewAsync(OwnedDisplay card)
    {
        // Any new preview cancels earlier in-flight artwork requests, whether
        // triggered by Cards-tab browsing or mainboard caret navigation.
        previewRequest?.Cancel();
        int version = ++previewVersion;
        if (PreviewCardName is null || PreviewArtwork is null ||
            PreviewStatus is null || PreviewMetadata is null ||
            PreviewOracleText is null || PreviewArtFallback is null ||
            PreviewOwnership is null || OwnedPoolBox is null)
            return;

        previewedCard = card;
        previewedPool = OwnedPoolBox.SelectedIndex;
        PreviewArtwork.Source = null;
        PreviewArtwork.Visibility = Visibility.Collapsed;
        PreviewArtFallback.Visibility = Visibility.Visible;
        PreviewOracleText.Document = NativeOracleReading.Build("");
        PreviewMetadata.Text = "";
        PreviewManaCost.Value = "";
        PreviewColorIdentity.Value = "";
        PreviewColorIdentity.Visibility = Visibility.Collapsed;
        PreviewSynergyText.Text = "";
        PreviewCardName.Text = card.Name;
        PreviewSynergyText.Text = card.LabSuggestion
            ? "Commander mechanic hint: " + card.SynergyExplanation
            : "";
        RefreshPreviewOwnership();
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            PreviewMetadata.Text = "No offline Scryfall catalog available.";
            PreviewStatus.Text = "Use Full Sync to load card metadata and artwork.";
            return;
        }

        bool arena = OwnedPoolBox.SelectedIndex == 1;
        var request = new CancellationTokenSource();
        previewRequest = request;
        var token = request.Token;
        PreviewStatus.Text = "Loading local card details…";
        try
        {
            // Rapid keyboard navigation should not generate one network
            // request per transient selection.
            await Task.Delay(220, token);
            var preview = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var info = CollectionCatalogReview.ReadDetails(catalogPath!, card.Name);
                string imageUrl = arena ? "" : info?.ArtworkUrl ?? "";
                if (!arena && deckPrintingChoices.TryGetValue(card.Name, out var chosenId) &&
                    Guid.TryParse(chosenId, out var exactPrintId))
                {
                    // Per-deck artwork is presentation only. Invalid or
                    // unavailable printings fall back safely to catalog art.
                    try
                    {
                        var choice = NativePrintingArtwork.Require(catalogPath!,
                            card.Name, exactPrintId);
                        imageUrl = choice.ImageUrl;
                    }
                    catch (Exception ex) when (ex is InvalidDataException or
                        Microsoft.Data.Sqlite.SqliteException)
                    {
                        // The user's selection remains saved for later sync.
                    }
                }
                if (arena && info is not null)
                {
                    // Never use a potentially paper-only card image for Arena.
                    // Printing gallery returns one representative Arena image,
                    // not a claimed owned digital cosmetic style.
                    try
                    {
                        imageUrl = NativePrintingArtwork.Read(catalogPath!,
                            card.Name, limit: 1, cancellation: token,
                            arenaOnly: true).Printings.FirstOrDefault()?.ImageUrl ?? "";
                    }
                    catch (Exception ex) when (ex is InvalidDataException or
                        Microsoft.Data.Sqlite.SqliteException)
                    {
                        imageUrl = "";
                    }
                }
                return (Info: info, ImageUrl: imageUrl);
            }, token);
            if (token.IsCancellationRequested || version != previewVersion) return;
            if (preview.Info is null)
            {
                PreviewMetadata.Text = "This card is not present in the offline catalog.";
                PreviewStatus.Text = "Ownership remains available; refresh card metadata to preview.";
                return;
            }

            PreviewMetadata.Text = preview.Info.TypeLine +
                $"  ·  Mana value {preview.Info.ManaValue:G}";
            PreviewManaCost.Value = preview.Info.ManaCost;
            PreviewColorIdentity.Value = preview.Info.ColorIdentity;
            PreviewColorIdentity.Visibility = Visibility.Visible;
            PreviewOracleText.Document = NativeOracleReading.Build(preview.Info.OracleText);
            if (!NativeCardArtwork.IsTrustedArtworkUrl(preview.ImageUrl))
            {
                PreviewStatus.Text = "No suitable official artwork in the current catalog. " +
                    "This preview does not identify an owned printing.";
                return;
            }

            PreviewStatus.Text = "Loading illustrative official artwork…";
            var bytes = await NativeCardArtwork.LoadAsync(preview.ImageUrl, token);
            if (token.IsCancellationRequested || version != previewVersion) return;
            if (bytes is null)
            {
                PreviewStatus.Text = "Artwork unavailable; offline card text is still shown.";
                return;
            }
            using var stream = new MemoryStream(bytes);
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 320;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            PreviewArtwork.Source = bitmap;
            PreviewArtwork.Visibility = Visibility.Visible;
            PreviewArtFallback.Visibility = Visibility.Collapsed;
            PreviewStatus.Text = "Illustrative catalog artwork · not verification of an owned printing.";
        }
        catch (OperationCanceledException) { /* Selection changed or editor closed. */ }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidDataException or Microsoft.Data.Sqlite.SqliteException or
            ArgumentException or System.Net.Http.HttpRequestException or System.NotSupportedException)
        {
            if (!token.IsCancellationRequested && version == previewVersion)
                PreviewStatus.Text = "Card preview unavailable: " + ex.Message;
        }
        finally
        {
            if (ReferenceEquals(previewRequest, request))
                previewRequest = null;
            request.Dispose();
        }
    }

    private void RefreshPreviewOwnership()
    {
        if (PreviewOwnership is null || MainboardBox is null ||
            previewedCard is not OwnedDisplay card)
            return;
        try
        {
            var inDeck = NativeDeckEditing.ParseMainboard(MainboardBox.Text)
                .FirstOrDefault(x => x.Name.Equals(card.Name,
                    StringComparison.OrdinalIgnoreCase))?.Quantity ?? 0;
            PreviewOwnership.Text = $"{card.Owned:N0} real owned" +
                (card.Proxied > 0 ? $" · {card.Proxied:N0} Proxy (not valued)" : "") +
                $" · {inDeck:N0} in mainboard";
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException)
        {
            PreviewOwnership.Text = $"{card.Owned:N0} real owned" +
                (card.Proxied > 0 ? $" · {card.Proxied:N0} Proxy (not valued)" : "") +
                " · draft needs review";
        }
    }

    private void InvalidateAnalytics()
    {
        draftVersion++;
        if (AnalyticsCharts is null || AnalyticsText is null) return;
        AnalyticsCharts.Visibility = Visibility.Collapsed;
        AnalyticsText.Text = "Deck changed. Select Analyze deck to refresh the charts.";
    }

    private void OnDraftChanged(object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        if (applyingManaBalance) return;
        if (edhLabCommander is not null && edhCatalog is not null &&
            EdhAutoBalanceLands?.IsChecked == true)
            ApplyManaBalance(automatic: true);
        InvalidateAnalytics();
        if (edhLabCommander is not null && edhCatalog is not null)
            ScheduleEdhAnalysis();
        else
            RefreshDraftSummary();
        RefreshPreviewOwnership();
    }

    private void ScheduleEdhAnalysis()
    {
        if (edhCatalog is null || edhLabCommander is null || MainboardBox is null)
            return;
        // A former suggestion must not remain clickable while the current
        // draft has not yet been analyzed. The ordinary preflight on Save
        // remains fully authoritative and is never delayed.
        lastOptimization = null;
        lastSwapAdvice = null;
        lastHealthReport = null;
        lastPackageReport = null;
        lastChainReport = null;
        lastEngineReport = null;
        EdhBrowseImprovementsButton.IsEnabled = false;
        EdhApplySwapButton.IsEnabled = false;
        EdhFindGapButton.IsEnabled = false;
        EdhHealthBrowseButton.IsEnabled = false;
        EdhChainJumpButton.IsEnabled = false;
        EdhEngineFindMissingButton.IsEnabled = false;
        EdhBalanceNowButton.IsEnabled = false;
        EdhHealthHeadline.Text = "Deck health: updating after mainboard edit…";
        EdhChainsStatus.Text = "Synergy chains: updating after mainboard edit…";
        EdhEnginesStatus.Text = "Three-card engines: updating after mainboard edit…";
        if (edhAnalysisTimer is null)
        {
            edhAnalysisTimer = new System.Windows.Threading.DispatcherTimer(
                System.Windows.Threading.DispatcherPriority.Background,
                Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(EdhAnalysisQuietMilliseconds)
            };
            edhAnalysisTimer.Tick += (_, _) =>
            {
                edhAnalysisTimer.Stop();
                if (IsLoaded && !busy)
                    RefreshDraftSummary();
            };
        }
        edhAnalysisTimer.Stop();
        edhAnalysisTimer.Start();
    }

    private void OnFormatChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(() =>
        {
            InvalidateAnalytics();
            RefreshDraftSummary();
        }));

    private bool TryLandTarget(out int target)
    {
        target = 36;
        return EdhLandTargetBox is not null &&
            int.TryParse(EdhLandTargetBox.Text, out target) &&
            target is >= 20 and <= 60;
    }

    private void OnEdhAutoBalanceChanged(object sender, RoutedEventArgs e)
    {
        if (edhLabCommander is null || EdhManaHeadline is null) return;
        if (EdhAutoBalanceLands.IsChecked == true)
            ApplyManaBalance(automatic: true);
        RefreshDraftSummary();
    }

    private void OnEdhLandTargetChanged(object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        if (edhLabCommander is null || EdhManaHeadline is null) return;
        if (EdhAutoBalanceLands?.IsChecked == true)
            ApplyManaBalance(automatic: true);
        RefreshDraftSummary();
    }

    private void OnEdhPipModeChanged(object sender, RoutedEventArgs e)
    {
        if (edhLabCommander is null || EdhManaHeadline is null) return;
        if (EdhAutoBalanceLands?.IsChecked == true)
            ApplyManaBalance(automatic: true);
        RefreshDraftSummary();
    }

    private void OnEdhBalanceNowClicked(object sender, RoutedEventArgs e)
    {
        if (edhLabCommander is null || busy) return;
        ApplyManaBalance(automatic: false);
        RefreshDraftSummary();
    }

    private bool ApplyManaBalance(bool automatic)
    {
        if (applyingManaBalance || busy || edhLabCommander is null ||
            edhCatalog is null || MainboardBox is null) return false;
        if (!TryLandTarget(out int target))
        {
            if (EdhManaDetails is not null)
                EdhManaDetails.Text = "Enter a land target between 20 and 60.";
            return false;
        }
        try
        {
            var entries = NativeDeckEditing.ParseMainboard(MainboardBox.Text);
            var plan = EdhLabManaPlanner.Analyze(edhCatalog, entries.Select(x => (x.Name, x.Quantity)),
                edhLabColors ?? "", target,
                weightSpellPips: EdhWeightSpellPips?.IsChecked == true);
            if (!plan.CanApply)
            {
                EdhManaDetails.Text = plan.Summary;
                return false;
            }
            string updated = EdhLabManaPlanner.Apply(
                MainboardBox.Text, edhLabColors ?? "", plan);
            string previous = MainboardBox.Text.Replace("\r\n", "\n")
                .Replace('\r', '\n');
            if (updated.Equals(previous, StringComparison.Ordinal)) return false;

            // Only the editable mainboard text changes. Preserve text entry
            // position and never write to native storage or catalog here.
            int caret = MainboardBox.CaretIndex;
            applyingManaBalance = true;
            try
            {
                MainboardBox.Text = updated;
                MainboardBox.CaretIndex = Math.Min(caret, MainboardBox.Text.Length);
            }
            finally { applyingManaBalance = false; }
            StatusText.Text = (automatic ? "Auto-balanced" : "Balanced") +
                $" the unsaved basic lands toward {target} total lands. " +
                "Existing nonbasic and snow lands were not changed. " +
                "Nothing is saved until Review and save deck.";
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException or RegexMatchTimeoutException)
        {
            // Users may still be typing an incomplete card. Never rewrite or
            // delete lines when parser or local metadata is uncertain.
            if (!automatic && EdhManaDetails is not null)
                EdhManaDetails.Text = "Balance unavailable: " + ex.Message;
            return false;
        }
    }

    private void RefreshLandAdvice(
        IReadOnlyList<NativeDeckEditing.Entry> entries)
    {
        if (edhLabCommander is null || EdhManaHeadline is null ||
            EdhManaDetails is null || EdhBalanceNowButton is null) return;
        if (edhCatalog is null)
        {
            EdhManaHeadline.Text = "Mana base: waiting for local catalog…";
            EdhManaDetails.Text = "Complete local Scryfall sync to calculate land sources.";
            EdhCurveHeadline.Text = "Curve: waiting for local catalog";
            EdhCurveDetails.Text = "Open an EDH Lab draft with synced Scryfall metadata.";
            EdhBalanceNowButton.IsEnabled = false;
            return;
        }
        if (!TryLandTarget(out int target))
        {
            EdhManaHeadline.Text = "Mana base: invalid target";
            EdhManaDetails.Text = "Choose 20–60 lands (36 is the initial recommendation).";
            EdhCurveHeadline.Text = "Curve: land target needs correction";
            EdhCurveDetails.Text = "Choose a valid land target to compute the 99-card projection.";
            // A malformed custom target invalidates advice derived from
            // that parameter. Do not leave old Apply/Browse actions enabled.
            lastOptimization = null;
            lastSwapAdvice = null;
            lastHealthReport = null;
            EdhApplySwapButton.IsEnabled = false;
            EdhBrowseImprovementsButton.IsEnabled = false;
            EdhHealthBrowseButton.IsEnabled = false;
            EdhFindGapButton.IsEnabled = false;
            EdhHealthHeadline.Text = "Deck health: choose a valid land target";
            EdhHealthOverview.Text = "Enter 20–60 lands before reviewing the current deck.";
            EdhHealthNextStep.Text = "No changes made.";
            EdhBalanceNowButton.IsEnabled = false;
            return;
        }
        var plan = EdhLabManaPlanner.Analyze(edhCatalog, entries.Select(x => (x.Name, x.Quantity)),
            edhLabColors ?? "", target,
            weightSpellPips: EdhWeightSpellPips?.IsChecked == true);
        string mode = EdhAutoBalanceLands?.IsChecked == true ? "Auto on" : "Manual";
        EdhManaHeadline.Text =
            $"Mana base: {plan.CurrentLands}/{target} current · " +
            $"{plan.TotalAfter}/{target} planned · {mode}";
        EdhManaDetails.Text = plan.Summary;
        EdhBalanceNowButton.IsEnabled = !busy && plan.CanApply;
        RefreshCurveAdvice(entries, plan);
    }

    private EdhLabStrategyFocus SelectedStrategyFocus
    {
        get
        {
            if (EdhStrategyFocusBox?.SelectedItem is
                    System.Windows.Controls.ComboBoxItem item &&
                Enum.TryParse<EdhLabStrategyFocus>(item.Tag?.ToString(),
                    ignoreCase: false, out var focus) &&
                Enum.IsDefined(focus))
                return focus;
            return EdhLabStrategyFocus.Balanced;
        }
    }

    private void OnEdhStrategyFocusChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (EdhStrategyExplanation is null) return;
        EdhStrategyExplanation.Text =
            EdhLabStrategyGoals.Description(SelectedStrategyFocus);
        // An intent change only recomputes read-only suggestions. Never
        // invoke ApplyManaBalance or change saved deck/collection state.
        if (edhLabCommander is not null && edhCatalog is not null)
            RefreshDraftSummary();
    }

    private void RefreshCurveAdvice(
        IReadOnlyList<NativeDeckEditing.Entry> entries, EdhLabManaPlanner.Plan plan)
    {
        if (edhCatalog is null || EdhCurveHeadline is null || EdhCurveDetails is null)
            return;
        var report = EdhLabCurveAnalysis.Analyze(
            edhCatalog, entries.Select(x => (x.Name, x.Quantity)), plan);
        EdhCurveHeadline.Text = report.Headline;
        EdhCurveDetails.Text = report.Display;
        lastOptimization = EdhLabDeckOptimizer.Analyze(
            edhCatalog, entries.Select(x => (x.Name, x.Quantity)),
            focus: SelectedStrategyFocus);
        EdhOptimizationHeadline.Text = lastOptimization.Headline;
        EdhOptimizationDetails.Text = lastOptimization.Display;
        EdhBrowseImprovementsButton.IsEnabled = !busy && lastOptimization.CanBrowse;
        RefreshHealthReview(entries);
        RefreshSwapAdvice(entries);
        RefreshSynergyPackages(entries);
        RefreshSynergyChains(entries);
        RefreshThreeCardEngines(entries);
        if (browsingOptimizationCandidates)
        {
            if (!lastOptimization.CanBrowse)
                ResetOptimizationBrowsing();
            RefreshOwnedCards();
        }
    }

    private void RefreshHealthReview(
        IReadOnlyList<NativeDeckEditing.Entry> entries)
    {
        if (edhCatalog is null || EdhHealthAreasList is null ||
            EdhHealthHeadline is null) return;
        var selected = EdhHealthAreasList.SelectedItem as EdhLabDeckHealth.Area;
        int landTarget = TryLandTarget(out var custom) ? custom : 36;
        lastHealthReport = EdhLabDeckHealth.Analyze(edhCatalog,
            entries.Select(x => (x.Name, x.Quantity)),
            landTarget, SelectedStrategyFocus, lastOptimization);
        EdhHealthHeadline.Text = lastHealthReport.Headline;
        EdhHealthOverview.Text = lastHealthReport.Explanation;
        EdhHealthNextStep.Text = "Suggested next review: " +
            lastHealthReport.ReviewNext;
        EdhHealthAreasList.ItemsSource = lastHealthReport.Areas;
        EdhHealthAreasList.SelectedItem = selected is null ? null :
            lastHealthReport.Areas.FirstOrDefault(x => x.Key == selected.Key);
        if (EdhHealthAreasList.SelectedItem is null)
        {
            EdhHealthBrowseButton.IsEnabled = false;
            EdhHealthAreaDetails.Text =
                "Select a coverage area. Targets are starting points, not a power ranking.";
        }
    }

    private void OnEdhHealthSelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (EdhHealthAreasList is null || EdhHealthBrowseButton is null ||
            EdhHealthAreaDetails is null) return;
        if (EdhHealthAreasList.SelectedItem is not EdhLabDeckHealth.Area area)
        {
            EdhHealthBrowseButton.IsEnabled = false;
            return;
        }
        EdhHealthAreaDetails.Text = area.Title + ": " + area.Explanation +
            (area.CanBrowse
                ? $"\nEligible owned example: {area.CandidateName} " +
                  $"({area.CandidateUnused} unused recorded). Check the full card text."
                : "\nNo unused eligible recorded suggestion is offered for this area.");
        EdhHealthBrowseButton.IsEnabled = !busy &&
            lastHealthReport?.SafeToSuggest == true && area.CanBrowse;
        EdhHealthBrowseButton.Content = area.CanBrowse
            ? "Inspect owned " + area.CandidateName + " in Cards"
            : "No owned candidate to inspect";
    }

    private void OnEdhHealthBrowseClicked(object sender, RoutedEventArgs e)
    {
        if (busy || edhCatalog is null || edhLabCommander is null ||
            EdhHealthAreasList?.SelectedItem is not EdhLabDeckHealth.Area selected ||
            !selected.CanBrowse) return;
        try
        {
            // Validate fresh against the current unsaved draft, format-filtered
            // local catalog and ownership. A stale suggestion cannot be used.
            var current = NativeDeckEditing.ParseMainboard(MainboardBox.Text);
            if (!TryLandTarget(out int landTarget)) return;
            var fresh = EdhLabDeckHealth.Analyze(edhCatalog,
                current.Select(x => (x.Name, x.Quantity)),
                landTarget, SelectedStrategyFocus);
            var match = fresh.Areas.FirstOrDefault(x => x.Key == selected.Key);
            if (!fresh.SafeToSuggest || match is null || !match.CanBrowse ||
                !match.CandidateName.Equals(selected.CandidateName,
                    StringComparison.OrdinalIgnoreCase))
            {
                RefreshHealthReview(current);
                StatusText.Text = "That recommendation changed. Review the refreshed deck health analysis.";
                return;
            }
            ResetOptimizationBrowsing();
            EdhRoleBox.SelectedIndex = 0;
            EdhSynergyOnly.IsChecked = false;
            EdhRecordedOnly.IsChecked = true;
            OwnedSearchBox.Text = match.CandidateName;
            EdhWorkspaceTabs.SelectedItem = EdhCardsTab;
            EdhCardsFilters.IsExpanded = true;
            RefreshOwnedCards();
            StatusText.Text = "Inspecting eligible recorded " + match.CandidateName +
                " in Cards. Adding and saving remain separate manual actions.";
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException)
        {
            StatusText.Text = "Correct the draft before following deck health advice: " +
                ex.Message;
        }
    }

    private void RefreshSynergyPackages(
        IReadOnlyList<NativeDeckEditing.Entry> entries)
    {
        if (edhCatalog is null || EdhPackagesList is null ||
            EdhPackagesStatus is null || EdhPackageThemeBox is null) return;
        lastPackageReport = EdhLabSynergyPackages.Analyze(edhCatalog,
            entries.Select(x => (x.Name, x.Quantity)));
        // Preserve the mechanic selection between edits, but reset it if
        // switching commander/format removes that mechanic from the catalog.
        string previousTheme = EdhPackageThemeBox.SelectedItem as string ?? "All mechanics";
        var names = new[] { "All mechanics" }.Concat(
            lastPackageReport.Packages.Select(p => p.Theme)).ToArray();
        if (!names.SequenceEqual(EdhPackageThemeBox.ItemsSource as IEnumerable<string>
                ?? Array.Empty<string>(), StringComparer.Ordinal))
        {
            updatingPackageTheme = true;
            try
            {
                EdhPackageThemeBox.ItemsSource = names;
                EdhPackageThemeBox.SelectedItem = names.Contains(previousTheme,
                    StringComparer.Ordinal) ? previousTheme : "All mechanics";
            }
            finally { updatingPackageTheme = false; }
        }
        RenderSynergyPackages();
    }

    private void OnEdhPackageThemeChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!updatingPackageTheme) RenderSynergyPackages();
    }

    private void RenderSynergyPackages()
    {
        if (EdhPackagesList is null || EdhPackagesStatus is null ||
            EdhPackageThemeBox is null || lastPackageReport is null) return;
        var previous = EdhPackagesList.SelectedItem as PackageDisplay;
        var report = lastPackageReport;
        if (!report.HasRecognizedThemes)
        {
            EdhPackagesStatus.Text = report.Explanation;
            EdhPackagesList.ItemsSource = null;
            EdhPackageAddButton.IsEnabled = false;
            EdhPackagePreviewText.Text = "No recognized mechanic package is available.";
            return;
        }
        string theme = EdhPackageThemeBox.SelectedItem as string ?? "All mechanics";
        var visible = theme == "All mechanics"
            ? report.Packages
            : report.Packages.Where(p => p.Theme == theme).ToArray();
        var display = visible.SelectMany(package =>
            package.InDeck.Select(x => new PackageDisplay(
                x.Name, package.Theme, "In deck", x.Kind, x.Why,
                x.RecordedCopies, x.RoleHints))
            .Concat(package.OwnedOptions.Select(x => new PackageDisplay(
                x.Name, package.Theme, "Owned option", x.Kind, x.Why,
                x.RecordedCopies, x.RoleHints)))
            .Concat(package.MissingOptions.Select(x => new PackageDisplay(
                x.Name, package.Theme, "Missing", x.Kind, x.Why,
                x.RecordedCopies, x.RoleHints)))).ToArray();
        EdhPackagesStatus.Text = string.Join("\n\n",
            visible.Select(p => p.Theme + ": " +
                p.InDeckCount + " matching copies in deck; " +
                p.OwnedOptions.Count + " owned options; " +
                p.MissingOptions.Count + " missing examples.\n" +
                p.Coverage + "\nNext review: " + p.NextStep)) +
            (report.UnknownDeckCopies > 0
                ? $"\n{report.UnknownDeckCopies} unverified deck copies."
                : "") +
            "\nRead-only cached-text hints; lists capped at five per category.";
        EdhPackagesList.ItemsSource = display;
        if (previous is not null)
            EdhPackagesList.SelectedItem = display.FirstOrDefault(x =>
                x.Name.Equals(previous.Name, StringComparison.OrdinalIgnoreCase) &&
                x.Theme == previous.Theme && x.Group == previous.Group);
        UpdatePackageAddButton();
    }

    private bool CanAddPackageCard(PackageDisplay? candidate)
    {
        if (busy || edhLabCommander is null || edhCatalog is null ||
            candidate is null || candidate.Group == "In deck" ||
            EdhPackagesList?.Items.Contains(candidate) != true ||
            MainboardBox is null) return false;
        var catalogCard = edhCatalog.Find(candidate.Name);
        if (catalogCard is null) return false;
        try
        {
            int copies = NativeDeckEditing.ParseMainboard(MainboardBox.Text)
                .FirstOrDefault(x => x.Name.Equals(candidate.Name,
                    StringComparison.OrdinalIgnoreCase))?.Quantity ?? 0;
            // Package quick-add is a one-copy intentional gesture. Basics
            // already present use the normal +1 buttons for extra copies.
            return copies == 0 && catalogCard.SuggestedCopyLimit > 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException) { return false; }
    }

    private void UpdatePackageAddButton()
    {
        if (EdhPackageAddButton is null || EdhPackagesList is null) return;
        var candidate = EdhPackagesList.SelectedItem as PackageDisplay;
        bool allowed = CanAddPackageCard(candidate);
        EdhPackageAddButton.IsEnabled = allowed;
        EdhPackageAddButton.Content = candidate is null
            ? "Add selected package card" : "Add " + candidate.Name;
        EdhPackageAddButton.ToolTip = candidate is null
            ? "Select a package card to inspect before intentionally adding it."
            : allowed ? "Add one locally eligible copy to the UNSAVED mainboard. " +
                (candidate.Recorded > 0 ? "Recorded in inventory." :
                    "Not recorded in inventory; saving will still review missing copies.")
            : "Already in the deck, not locally eligible, or the draft requires review.";
    }

    private void OnEdhPackagePreviewMouseDown(object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left ||
            e.ClickCount != 2 || EdhPackagesList is null ||
            e.OriginalSource is not DependencyObject source ||
            System.Windows.Controls.ItemsControl.ContainerFromElement(
                EdhPackagesList, source) is not System.Windows.Controls.ListBoxItem row ||
            row.DataContext is not PackageDisplay clicked)
            return;

        // Unlike the Cards tab this is a distinct list; its normal
        // SelectionChanged is preview-only and clears Cards selection.
        if (!ReferenceEquals(clicked, EdhPackagesList.SelectedItem))
            EdhPackagesList.SelectedItem = clicked;
        TryAddPackageSelection(clicked, "double-click");
        e.Handled = true;
    }

    private void OnEdhPackageAddClicked(object sender, RoutedEventArgs e)
    {
        if (EdhPackagesList?.SelectedItem is PackageDisplay selected)
            TryAddPackageSelection(selected, "Add button");
    }

    private void TryAddPackageSelection(PackageDisplay selected, string action)
    {
        if (!CanAddPackageCard(selected))
        {
            StatusText.Text = "That package card is already in the draft or " +
                "is not currently eligible. Inspect the current package and deck.";
            UpdatePackageAddButton();
            return;
        }
        // Reuse the same authoritative catalog + copy-limit gate as Cards.
        // A Missing option may be drafted, but never invents ownership.
        AddEligibleCardToMainboard(selected.Name,
            singletonDoubleClick: true, source: "package " + action);
        UpdatePackageAddButton();
    }

    private async void OnEdhPackageSelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdatePackageAddButton();
        if (EdhPackagesList?.SelectedItem is not PackageDisplay selected ||
            busy || OwnedPoolBox is null) return;
        EdhPackagePreviewText.Text = selected.Theme + " · " +
            selected.Group + " · " + selected.Kind + "\n" +
            selected.Explanation + "\n" +
            (CanAddPackageCard(selected)
                ? "Single-click only previews. Double-click or use Add to place one " +
                  "copy in the UNSAVED mainboard. Inventory is unchanged." +
                  (selected.Recorded == 0
                      ? " This card is not recorded as owned." : "")
                : "This card is already drafted, locally ineligible, or the " +
                  "draft needs correction. Selection only previews; no edits.");
        // A suggestion preview must never leave the Cards-tab Add/Remove
        // controls targeting a different highlighted card.
        OwnedCardsList?.UnselectAll();
        await ShowCardPreviewAsync(new OwnedDisplay(selected.Name,
            selected.Recorded, selected.Roles, LabSuggestion: true,
            SynergyExplanation: selected.Explanation,
            MechanicMatch: true, Proxied: CountProxy(selected.Name)));
    }

    private void RefreshSynergyChains(
        IReadOnlyList<NativeDeckEditing.Entry> entries)
    {
        if (edhCatalog is null || EdhChainsList is null ||
            EdhChainsStatus is null) return;
        var previous = EdhChainsList.SelectedItem as EdhLabSynergyChains.Chain;
        lastChainReport = EdhLabSynergyChains.Analyze(edhCatalog,
            entries.Select(x => (x.Name, x.Quantity)));
        EdhChainsStatus.Text = lastChainReport.Headline + "\n" +
            lastChainReport.Disclaimer +
            (lastChainReport.UnknownCopies > 0
                ? $"\n{lastChainReport.UnknownCopies} unknown/ineligible deck copies excluded."
                : "");
        EdhChainsList.ItemsSource = lastChainReport.Chains;
        if (lastChainReport.Chains.Count == 0)
            ClearChainExplorer();
        if (previous is not null)
            EdhChainsList.SelectedItem = lastChainReport.Chains.FirstOrDefault(x =>
                x.Theme == previous.Theme &&
                x.Enabler.Equals(previous.Enabler, StringComparison.OrdinalIgnoreCase) &&
                x.Payoff.Equals(previous.Payoff, StringComparison.OrdinalIgnoreCase));
        else
            EdhChainDetails.Text =
                "Select a chain to inspect its suggested card and read both Oracle texts.";
    }

    private void ClearChainExplorer()
    {
        chainArtworkRequest?.Cancel();
        ++chainArtworkVersion;
        if (EdhChainEnablerArt is null || EdhChainPayoffArt is null) return;
        EdhChainEnablerArt.Source = null;
        EdhChainPayoffArt.Source = null;
        EdhChainEnablerArt.Visibility = Visibility.Collapsed;
        EdhChainPayoffArt.Visibility = Visibility.Collapsed;
        EdhChainEnablerFallback.Visibility = Visibility.Visible;
        EdhChainPayoffFallback.Visibility = Visibility.Visible;
        EdhChainEnablerFallback.Text = "Choose a chain";
        EdhChainPayoffFallback.Text = "Choose a chain";
        EdhChainEnablerButton.IsEnabled = false;
        EdhChainPayoffButton.IsEnabled = false;
        EdhChainJumpButton.IsEnabled = false;
    }

    private async Task LoadChainArtworkAsync(EdhLabSynergyChains.Chain chain)
    {
        chainArtworkRequest?.Cancel();
        var request = new CancellationTokenSource();
        chainArtworkRequest = request;
        int version = ++chainArtworkVersion;
        EdhChainEnablerArt.Source = null;
        EdhChainPayoffArt.Source = null;
        EdhChainEnablerArt.Visibility = Visibility.Collapsed;
        EdhChainPayoffArt.Visibility = Visibility.Collapsed;
        EdhChainEnablerFallback.Visibility = Visibility.Visible;
        EdhChainPayoffFallback.Visibility = Visibility.Visible;
        EdhChainEnablerFallback.Text = chain.Enabler;
        EdhChainPayoffFallback.Text = chain.Payoff;
        if (string.IsNullOrWhiteSpace(catalogPath)) return;
        try
        {
            bool arena = edhLabFormat == EdhLabFormat.ArenaBrawl;
            // Use the same trusted-artwork and Arena-printing restrictions as
            // the existing preview. All blocking catalog reads are off the UI
            // thread; requests are canceled when the selection changes.
            async Task<System.Windows.Media.Imaging.BitmapImage?> Fetch(string name)
            {
                string url = await Task.Run(() =>
                {
                    request.Token.ThrowIfCancellationRequested();
                    var details = CollectionCatalogReview.ReadDetails(catalogPath!, name);
                    if (!arena) return details?.ArtworkUrl ?? "";
                    if (details is null) return "";
                    return NativePrintingArtwork.Read(catalogPath!, name,
                        limit: 1, cancellation: request.Token, arenaOnly: true)
                        .Printings.FirstOrDefault()?.ImageUrl ?? "";
                }, request.Token);
                if (!NativeCardArtwork.IsTrustedArtworkUrl(url)) return null;
                var bytes = await NativeCardArtwork.LoadAsync(url, request.Token);
                if (bytes is null || request.Token.IsCancellationRequested) return null;
                using var stream = new MemoryStream(bytes);
                var image = new System.Windows.Media.Imaging.BitmapImage();
                image.BeginInit();
                image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 160;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            var left = await Fetch(chain.Enabler);
            if (request.Token.IsCancellationRequested || version != chainArtworkVersion) return;
            if (left is not null)
            {
                EdhChainEnablerArt.Source = left;
                EdhChainEnablerArt.Visibility = Visibility.Visible;
                EdhChainEnablerFallback.Visibility = Visibility.Collapsed;
            }
            var right = await Fetch(chain.Payoff);
            if (request.Token.IsCancellationRequested || version != chainArtworkVersion) return;
            if (right is not null)
            {
                EdhChainPayoffArt.Source = right;
                EdhChainPayoffArt.Visibility = Visibility.Visible;
                EdhChainPayoffFallback.Visibility = Visibility.Collapsed;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidDataException or Microsoft.Data.Sqlite.SqliteException or
            ArgumentException or System.Net.Http.HttpRequestException or
            System.NotSupportedException)
        {
            // Text fallbacks and inspection controls continue to work offline.
        }
        finally
        {
            if (ReferenceEquals(chainArtworkRequest, request))
                chainArtworkRequest = null;
            request.Dispose();
        }
    }

    private async void OnEdhChainSelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (EdhChainsList?.SelectedItem is not EdhLabSynergyChains.Chain chain ||
            busy || OwnedPoolBox is null) return;
        EdhChainDetails.Text = chain.Explanation + "\n" +
            chain.Status + ": " + chain.Enabler + " → " + chain.Payoff +
            "\nEnabler text: " + chain.EnablerEvidence +
            "\nPayoff text: " + chain.PayoffEvidence +
            "\nInspect either card for complete Oracle text. Artwork is illustrative, " +
            "not verified ownership. Nothing is modified.";
        EdhChainEnablerButton.Content = "Inspect " + chain.Enabler;
        EdhChainPayoffButton.Content = "Inspect " + chain.Payoff;
        EdhChainEnablerButton.IsEnabled = true;
        EdhChainPayoffButton.IsEnabled = true;
        EdhChainJumpButton.IsEnabled = chain.Status == "Owned missing link";
        // Start the scoped art request immediately. A delayed shared card
        // preview must not let an older selection cancel a newer art request.
        var artwork = LoadChainArtworkAsync(chain);
        await PreviewChainCardAsync(chain.SuggestedCard, chain.Explanation);
        await artwork;
    }

    private Task PreviewChainCardAsync(string name, string why)
    {
        if (busy || edhCatalog is null || OwnedPoolBox is null)
            return Task.CompletedTask;
        OwnedCardsList?.UnselectAll();
        var record = edhCatalog.Find(name);
        return ShowCardPreviewAsync(new OwnedDisplay(name,
            record?.RecordedCopies ?? 0, record?.RoleHints ?? "",
            LabSuggestion: true, SynergyExplanation: why,
            MechanicMatch: true, Proxied: CountProxy(name)));
    }

    private async void OnEdhInspectEnablerClicked(object sender, RoutedEventArgs e)
    {
        if (EdhChainsList?.SelectedItem is EdhLabSynergyChains.Chain chain)
            await PreviewChainCardAsync(chain.Enabler, chain.Explanation);
    }

    private async void OnEdhInspectPayoffClicked(object sender, RoutedEventArgs e)
    {
        if (EdhChainsList?.SelectedItem is EdhLabSynergyChains.Chain chain)
            await PreviewChainCardAsync(chain.Payoff, chain.Explanation);
    }

    private void OnEdhChainJumpClicked(object sender, RoutedEventArgs e)
    {
        if (busy || EdhChainsList?.SelectedItem is not EdhLabSynergyChains.Chain chain ||
            chain.Status != "Owned missing link" || edhCatalog is null)
            return;
        // Browse through the existing manually-operated Cards picker. Never
        // call add/edit/save from an analyzer suggestion.
        ResetOptimizationBrowsing();
        EdhRecordedOnly.IsChecked = true;
        EdhRoleBox.SelectedIndex = 0;
        EdhSynergyOnly.IsChecked = false;
        OwnedSearchBox.Text = chain.SuggestedCard;
        EdhWorkspaceTabs.SelectedItem = EdhCardsTab;
        RefreshOwnedCards();
        StatusText.Text = "Showing owned eligible missing link " + chain.SuggestedCard +
            " in Cards. Inspect it before making any manual deck change.";
    }

    private void RefreshThreeCardEngines(
        IReadOnlyList<NativeDeckEditing.Entry> entries)
    {
        if (edhCatalog is null || EdhEnginesList is null ||
            EdhEnginesStatus is null) return;
        var old = EdhEnginesList.SelectedItem as EdhLabThreeCardEngines.Engine;
        lastEngineReport = EdhLabThreeCardEngines.Analyze(edhCatalog,
            entries.Select(x => (x.Name, x.Quantity)));
        EdhEnginesStatus.Text = lastEngineReport.Headline + "\n" +
            lastEngineReport.Disclaimer +
            (lastEngineReport.UnknownCopies > 0
                ? $"\n{lastEngineReport.UnknownCopies} unknown or ineligible copies excluded."
                : "");
        EdhEnginesList.ItemsSource = lastEngineReport.Engines;
        EdhEnginesList.SelectedItem = old is null ? null :
            lastEngineReport.Engines.FirstOrDefault(x =>
                x.Theme == old.Theme &&
                x.Producer.Equals(old.Producer, StringComparison.OrdinalIgnoreCase) &&
                x.Bridge.Equals(old.Bridge, StringComparison.OrdinalIgnoreCase) &&
                x.Payoff.Equals(old.Payoff, StringComparison.OrdinalIgnoreCase));
        if (EdhEnginesList.SelectedItem is null)
            ClearThreeCardEngineSelection();
    }

    private void ClearThreeCardEngineSelection()
    {
        if (EdhEngineProducerButton is null) return;
        EdhEngineProducerButton.IsEnabled = false;
        EdhEngineBridgeButton.IsEnabled = false;
        EdhEnginePayoffButton.IsEnabled = false;
        EdhEngineFindMissingButton.IsEnabled = false;
        EdhEngineDetails.Text = "Select a possible pathway to inspect each card. No cards have changed.";
    }

    private void OnEdhEngineSelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (EdhEnginesList?.SelectedItem is not EdhLabThreeCardEngines.Engine engine ||
            busy) return;
        EdhEngineProducerButton.Content = "Producer: " + engine.Producer;
        EdhEngineBridgeButton.Content = "Bridge: " + engine.Bridge;
        EdhEnginePayoffButton.Content = "Payoff: " + engine.Payoff;
        EdhEngineProducerButton.IsEnabled = true;
        EdhEngineBridgeButton.IsEnabled = true;
        EdhEnginePayoffButton.IsEnabled = true;
        EdhEngineFindMissingButton.IsEnabled = engine.Status == "Owned missing link";
        EdhEngineDetails.Text = engine.Explanation + "\n" +
            engine.Status + ": " + engine.Producer + " → " +
            engine.Bridge + " → " + engine.Payoff +
            (engine.Status == "Owned missing link"
                ? $"\nOwned missing piece: {engine.MissingName} ({engine.UnusedRecordedCopies} unused recorded)."
                : engine.Status == "Not recorded"
                    ? "\nNot recorded: " + engine.MissingName
                    : "\nAll three names are in the current mainboard.") +
            "\nEach control opens the full cached card preview. No combo or win is guaranteed.";
    }

    private async void OnEdhInspectProducerClicked(object sender, RoutedEventArgs e)
    {
        if (EdhEnginesList?.SelectedItem is EdhLabThreeCardEngines.Engine engine)
            await PreviewChainCardAsync(engine.Producer, engine.Explanation);
    }

    private async void OnEdhInspectBridgeClicked(object sender, RoutedEventArgs e)
    {
        if (EdhEnginesList?.SelectedItem is EdhLabThreeCardEngines.Engine engine)
            await PreviewChainCardAsync(engine.Bridge, engine.Explanation);
    }

    private async void OnEdhInspectEnginePayoffClicked(object sender, RoutedEventArgs e)
    {
        if (EdhEnginesList?.SelectedItem is EdhLabThreeCardEngines.Engine engine)
            await PreviewChainCardAsync(engine.Payoff, engine.Explanation);
    }

    private void OnEdhEngineFindMissingClicked(object sender, RoutedEventArgs e)
    {
        if (busy || EdhEnginesList?.SelectedItem is not
            EdhLabThreeCardEngines.Engine engine ||
            engine.Status != "Owned missing link" || edhCatalog is null ||
            edhCatalog.Find(engine.MissingName) is not { } card ||
            card.RecordedCopies < engine.UnusedRecordedCopies ||
            engine.UnusedRecordedCopies < 1) return;
        // A navigation-only entry point into the existing manual picker.
        ResetOptimizationBrowsing();
        EdhRecordedOnly.IsChecked = true;
        EdhRoleBox.SelectedIndex = 0;
        EdhSynergyOnly.IsChecked = false;
        OwnedSearchBox.Text = engine.MissingName;
        EdhWorkspaceTabs.SelectedItem = EdhCardsTab;
        RefreshOwnedCards();
        StatusText.Text = "Inspecting owned three-card engine candidate " +
            engine.MissingName + ". No card was added or saved.";
    }

    private void RefreshSwapAdvice(
        IReadOnlyList<NativeDeckEditing.Entry> entries)
    {
        if (edhCatalog is null || EdhSwapHeadline is null ||
            EdhSwapProposalsList is null || EdhApplySwapButton is null)
            return;
        var previous = EdhSwapProposalsList.SelectedItem as
            EdhLabSwapAdvisor.Proposal;
        int landTarget = TryLandTarget(out int userTarget)
            ? userTarget : 36;
        lastSwapAdvice = EdhLabSwapAdvisor.Analyze(edhCatalog,
            entries.Select(x => (x.Name, x.Quantity)), landTarget,
            focus: SelectedStrategyFocus);
        EdhSwapHeadline.Text = lastSwapAdvice.Headline;
        EdhSwapStatus.Text = lastSwapAdvice.Display;
        EdhSwapProposalsList.ItemsSource = lastSwapAdvice.Proposals;
        if (previous is not null)
            EdhSwapProposalsList.SelectedItem = lastSwapAdvice.Proposals
                .FirstOrDefault(x =>
                    x.OutName.Equals(previous.OutName,
                        StringComparison.OrdinalIgnoreCase) &&
                    x.InName.Equals(previous.InName,
                        StringComparison.OrdinalIgnoreCase));
        UpdateSwapSelection();
    }

    private void OnEdhSwapSelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e) =>
        UpdateSwapSelection();

    private void UpdateSwapSelection()
    {
        if (EdhSwapPreviewText is null || EdhApplySwapButton is null ||
            EdhSwapProposalsList is null) return;
        var selected = EdhSwapProposalsList.SelectedItem as
            EdhLabSwapAdvisor.Proposal;
        EdhSwapPreviewText.Text = selected?.Explanation ??
            "Select a proposed cut and replacement to inspect the tradeoff.";
        EdhApplySwapButton.IsEnabled = !busy && selected is not null &&
            lastSwapAdvice is not null &&
            EdhLabSwapAdvisor.IsCurrent(lastSwapAdvice, selected);
    }

    private void OnEdhApplySwapClicked(object sender, RoutedEventArgs e)
    {
        if (busy || edhCatalog is null || edhLabCommander is null ||
            EdhSwapProposalsList.SelectedItem is not
                EdhLabSwapAdvisor.Proposal selected)
            return;
        // Revalidate immediately before applying. The in-memory catalog is
        // already format/color filtered; refresh the selected proposal
        // against the latest draft, role counts and recorded quantities.
        try
        {
            var entries = NativeDeckEditing.ParseMainboard(MainboardBox.Text);
            int landTarget = TryLandTarget(out int userTarget)
                ? userTarget : 36;
            var fresh = EdhLabSwapAdvisor.Analyze(edhCatalog,
                entries.Select(x => (x.Name, x.Quantity)), landTarget,
                focus: SelectedStrategyFocus);
            if (!EdhLabSwapAdvisor.IsCurrent(fresh, selected))
            {
                RefreshSwapAdvice(entries);
                StatusText.Text = "That swap is no longer eligible. Review the " +
                    "refreshed proposals before choosing again.";
                return;
            }
            int before = entries.Sum(x => x.Quantity);
            string message = selected.Explanation + "\n\n" +
                "Apply this ONE-FOR-ONE change to the UNSAVED draft? " +
                "If Auto-balance basics is enabled, its suggested basics " +
                "may also update. No collection or saved deck is modified. " +
                "Review and save deck remains a separate action.";
            if (NativeInAppDialog.Show(this, message, "Review proposed EDH Lab swap",
                    MessageBoxButton.YesNo, MessageBoxImage.Question,
                    MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
            // A single assignment makes both requested changes atomic in the
            // editor; no intermediate removal event can auto-balance the
            // partially modified list or touch the native save path.
            string removed = NativeDeckOwnedPicker.RemoveOne(
                MainboardBox.Text, selected.OutName);
            string swapped = NativeDeckOwnedPicker.AddOne(
                removed, selected.InName);
            if (NativeDeckEditing.ParseMainboard(swapped)
                    .Sum(x => x.Quantity) != before)
                throw new InvalidDataException(
                    "Proposed swap did not preserve deck copy count.");
            MainboardBox.Text = swapped;
            StatusText.Text = $"Applied unsaved swap: -1 {selected.OutName}, " +
                $"+1 {selected.InName}. Collection, deck library and revision " +
                "history are unchanged until you explicitly review and save.";
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException)
        {
            StatusText.Text = "Swap was not applied: " + ex.Message;
        }
    }

    private void ResetOptimizationBrowsing()
    {
        browsingOptimizationCandidates = false;
        if (EdhBrowseImprovementsButton is not null)
            EdhBrowseImprovementsButton.Content = "Browse owned early-play candidates";
    }

    private void OnEdhBrowseImprovementsClicked(object sender, RoutedEventArgs e)
    {
        if (busy || edhCatalog is null || lastOptimization is null)
            return;
        if (browsingOptimizationCandidates)
        {
            ResetOptimizationBrowsing();
            RefreshOwnedCards();
            StatusText.Text = "Showing normal EDH Lab catalog search again.";
            return;
        }
        if (!lastOptimization.CanBrowse)
            return;
        // Prepare the standard picker, then override only its visible items.
        // Existing add/remove and protected native save paths are unchanged.
        EdhRecordedOnly.IsChecked = true;
        EdhRoleBox.SelectedIndex = 0;
        EdhSynergyOnly.IsChecked = false;
        OwnedSearchBox.Text = "";
        browsingOptimizationCandidates = true;
        EdhWorkspaceTabs.SelectedItem = EdhCardsTab;
        EdhBrowseImprovementsButton.Content = "Return to full card catalog";
        RefreshOwnedCards();
        StatusText.Text = "Browsing unused, recorded, locally legal MV 0–2 cards. " +
            "Select a card for its preview; adding or replacing cards remains manual.";
    }

    private void RefreshDraftSummary()
    {
        // Explicit refreshes (strategy/format/land-target changes) replace
        // the queued draft-only pass, rather than rendering it a second time.
        edhAnalysisTimer?.Stop();
        // Shares the existing 220ms draft-analysis debounce. Card counts
        // and presence filters update without a full catalog reload.
        if (edhLabCommander is not null && edhCatalog is not null)
            RefreshOwnedCards();
        if (DeckSummaryText is null || DeckShortageText is null ||
            MainboardBox is null || CommanderBox is null || FormatBox is null ||
            OwnedPoolBox is null)
            return;
        if (edhLabCommander is not null) RefreshMainboardManager();
        var pool = OwnedPoolBox.SelectedIndex == 1
            ? NativeDeckOwnedPicker.Pool.Arena : NativeDeckOwnedPicker.Pool.Physical;
        try
        {
            var summary = NativeDeckOwnedPicker.Review(ownership, pool,
                MainboardBox.Text, CommanderBox.Text, FormatBox.Text, proxyLedger);
            DeckSummaryText.Text = summary.Header;
            string poolName = pool == NativeDeckOwnedPicker.Pool.Arena ? "Arena" : "Physical";
            var lines = summary.Shortages.Take(6).Select(x =>
                $"{x.Name}: need {x.Required}, owned {x.Owned}" +
                (pool == NativeDeckOwnedPicker.Pool.Physical && x.Proxy > 0
                    ? $", Proxy {x.Proxy}" : "") +
                $" (still missing {x.Missing})");
            DeckShortageText.Text = summary.Shortages.Count == 0
                ? $"All draft copies covered by {poolName} inventory" +
                  (summary.ProxyCoveredCopies > 0
                      ? $" + {summary.ProxyCoveredCopies:N0} Proxy copies (not owned or valued)."
                      : ".")
                : $"{poolName} shortages ({summary.Shortages.Count} cards):\n" +
                  string.Join("\n", lines) +
                  (summary.Shortages.Count > 6
                      ? $"\n… plus {summary.Shortages.Count - 6} more card names."
                      : "");
            if (edhLabCommander is not null)
            {
                RefreshMissingCardReview(summary);
                var entries = NativeDeckEditing.ParseMainboard(MainboardBox.Text);
                if (edhCatalog is not null)
                {
                    var ineligible = entries
                        .Where(x => !edhCatalog.Contains(x.Name))
                        .Select(x => x.Name).Take(5).ToArray();
                    EdhLegalityText.Text = ineligible.Length == 0
                        ? "All drafted names are locally eligible by format and commander identity. Save runs deeper checks."
                        : "Review ineligible / missing catalog metadata: " +
                          string.Join(", ", ineligible) +
                          ". Save will show warnings; no card is removed automatically.";
                }
                RefreshEdhBlueprint(entries);
                RefreshLandAdvice(entries);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException)
        {
            DeckSummaryText.Text = "Deck draft needs review";
            DeckShortageText.Text = ex.Message;
            if (edhLabCommander is not null)
            {
                ClearMissingCardReview("Correct the decklist to review recorded-copy shortages.");
                EdhBlueprintText.Text = "Correct the decklist to update role coverage.";
                EdhFindGapButton.IsEnabled = false;
                EdhFindGapButton.Content = "Find next role";
                EdhManaHeadline.Text = "Mana base: correct the decklist to continue";
                EdhManaDetails.Text = ex.Message;
                EdhCurveHeadline.Text = "Curve: correct the decklist to continue";
                EdhCurveDetails.Text = "No draw estimates are available for invalid draft text.";
                ResetOptimizationBrowsing();
                lastOptimization = null;
                lastHealthReport = null;
                EdhHealthAreasList.ItemsSource = null;
                EdhHealthHeadline.Text = "Deck health: correct the decklist";
                EdhHealthOverview.Text = "Role review and suggestions are paused until all draft lines are valid.";
                EdhHealthNextStep.Text = "No deck or collection changes were made.";
                EdhHealthAreaDetails.Text = "Fix the decklist first.";
                EdhHealthBrowseButton.IsEnabled = false;
                EdhOptimizationHeadline.Text = "Optimization: correct the decklist";
                EdhOptimizationDetails.Text = "Recommendations are paused until every draft line is valid.";
                EdhBrowseImprovementsButton.IsEnabled = false;
                lastSwapAdvice = null;
                EdhSwapProposalsList.ItemsSource = null;
                lastPackageReport = null;
                EdhPackagesList.ItemsSource = null;
                EdhPackagesStatus.Text = "Packages paused until the decklist is valid.";
                lastChainReport = null;
                EdhChainsList.ItemsSource = null;
                lastEngineReport = null;
                EdhEnginesList.ItemsSource = null;
                EdhEnginesStatus.Text = "Three-card pathways paused until the draft is valid.";
                ClearThreeCardEngineSelection();
                ClearChainExplorer();
                EdhChainsStatus.Text = "Chains paused until the decklist is valid.";
                EdhChainDetails.Text = "No changes made.";
                EdhPackagePreviewText.Text = "No draft changes were made.";
                EdhSwapHeadline.Text = "Swap Lab: correct the decklist first";
                EdhSwapStatus.Text = "Proposals paused until every draft line is valid.";
                EdhSwapPreviewText.Text = "No draft changes were made.";
                EdhApplySwapButton.IsEnabled = false;
                EdhBalanceNowButton.IsEnabled = false;
            }
        }
    }

    private void RefreshEdhBlueprint(
        IReadOnlyList<NativeDeckEditing.Entry> entries)
    {
        if (edhLabCommander is null || EdhBlueprintText is null ||
            EdhFindGapButton is null) return;
        if (edhCatalog is null)
        {
            EdhBlueprintText.Text = "Waiting for locally eligible card metadata…";
            EdhFindGapButton.IsEnabled = false;
            return;
        }

        int landTarget = TryLandTarget(out var customLandTarget)
            ? customLandTarget : 36;
        var blueprint = EdhLabDeckBlueprint.Analyze(edhCatalog,
            entries.Select(x => (x.Name, x.Quantity)), landTarget);
        EdhBlueprintText.Text = blueprint.Display;
        var gap = blueprint.NextGap;
        EdhFindGapButton.IsEnabled = !busy && gap is not null;
        EdhFindGapButton.Content = gap is null
            ? "Suggested role targets met"
            : $"Browse {gap.Role} (suggest {gap.Shortfall} more)";
    }

    private void OnEdhFindGapClicked(object sender, RoutedEventArgs e)
    {
        if (busy || edhCatalog is null || edhLabCommander is null) return;
        try
        {
            int landTarget = TryLandTarget(out var target) ? target : 36;
            var blueprint = EdhLabDeckBlueprint.Analyze(edhCatalog,
                NativeDeckEditing.ParseMainboard(MainboardBox.Text)
                    .Select(x => (x.Name, x.Quantity)), landTarget);
            var gap = blueprint.NextGap;
            if (gap is null) return;
            var choice = EdhRoleBox.Items.OfType<System.Windows.Controls.ComboBoxItem>()
                .FirstOrDefault(x => string.Equals(x.Tag?.ToString(), gap.Role,
                    StringComparison.OrdinalIgnoreCase));
            if (choice is null) return;
            EdhRoleBox.SelectedItem = choice;
            EdhWorkspaceTabs.SelectedItem = EdhCardsTab;
            EdhCardsFilters.IsExpanded = true;
            OwnedSearchBox.Text = "";
            EdhRecordedOnly.IsChecked = true;
            RefreshOwnedCards();
            StatusText.Text = $"Browsing recorded cards with a {gap.Role} hint. " +
                "Uncheck 'Recorded in my inventory only' to include missing cards. " +
                "Role targets are suggestions; no cards are added automatically.";
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException)
        {
            StatusText.Text = "Correct the decklist before browsing a role gap: " + ex.Message;
        }
    }

    private void OnEdhSuggestionsChanged(object sender, RoutedEventArgs e)
    {
        if (edhLabCommander is not null)
        {
            ResetOptimizationBrowsing();
            RefreshOwnedCards();
        }
    }

    private void OnEdhCardPresenceChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (edhLabCommander is null || OwnedCardsList is null) return;
        // Presence is a visual lens on the unsaved mainboard, not a deck edit.
        ResetOptimizationBrowsing();
        RefreshOwnedCards();
    }

    private async Task LoadEdhSuggestionsAsync()
    {
        if (edhLabCommander is null || edhLabFormat is null ||
            string.IsNullOrWhiteSpace(catalogPath)) return;
        EdhSuggestionsStatus.Text = "Loading verified offline EDH cards (reused on later opens)…";
        try
        {
            // Warm commander/ownership/format snapshots are immediately
            // available; a cold first read runs off the UI thread.
            EdhLabDraftCatalog suggestions;
            if (EdhLabReadOnlyCache.TryGetDraft(catalogPath!, ownership,
                    edhLabCommander, edhLabColors ?? "", edhLabFormat.Value,
                    out var cached) && cached is not null)
                suggestions = cached;
            else
                suggestions = await Task.Run(() => EdhLabReadOnlyCache.GetDraft(
                    catalogPath!, ownership, edhLabCommander,
                    edhLabColors ?? "", edhLabFormat.Value));
            if (!IsLoaded) return;
            edhCatalog = suggestions;
            EdhSuggestionsStatus.Text =
                $"{suggestions.RecordedEligibleCount:N0} eligible recorded names · " +
                $"{suggestions.EligibleCount:N0} locally legal card names. " +
                "Only card-name holdings are counted, not verified printings.";
            EdhSynergyStatus.Text = suggestions.Synergy.ThemeSummary +
                $" {suggestions.SynergyEligibleCount:N0} legal candidate names share recognized signals.";
            bool hasSignals = suggestions.Synergy.ThemeCount > 0;
            EdhSynergyOnly.IsEnabled = hasSignals;
            EdhPrioritizeSynergy.IsEnabled = hasSignals;
            if (!hasSignals)
                EdhSynergyOnly.IsChecked = false;
            RefreshOwnedCards();
            if (EdhAutoBalanceLands.IsChecked == true)
                ApplyManaBalance(automatic: true);
            RefreshDraftSummary();
            if (focusShortageReview)
            {
                EdhWorkspaceTabs.SelectedItem = EdhImproveTab;
                EdhMissingExpander.IsExpanded = true;
                StatusText.Text = "Reviewing saved-deck shortages with recorded, " +
                    "locally eligible alternatives. Nothing has been swapped or saved.";
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or
            Microsoft.Data.Sqlite.SqliteException or ArgumentException)
        {
            EdhSuggestionsStatus.Text = "Suggestions unavailable: " + ex.Message +
                ". You can still edit a deck manually; saving runs preflight.";
            ClearMissingCardReview("A synced local catalog is required for replacement suggestions.");
            EdhBlueprintText.Text = "Blueprint unavailable without a local catalog.";
            EdhSynergyStatus.Text = "Mechanic recommendations unavailable without a synced catalog.";
            EdhSynergyOnly.IsEnabled = false;
            EdhPrioritizeSynergy.IsEnabled = false;
            EdhFindGapButton.IsEnabled = false;
            EdhManaHeadline.Text = "Mana base: catalog unavailable";
            EdhManaDetails.Text = "No automatic land changes were made.";
            EdhCurveHeadline.Text = "Curve: catalog unavailable";
            EdhCurveDetails.Text = "Refresh Scryfall metadata to evaluate your draft.";
            ResetOptimizationBrowsing();
            lastOptimization = null;
            EdhOptimizationHeadline.Text = "Optimization: catalog unavailable";
            EdhOptimizationDetails.Text = "Refresh local Scryfall metadata; no deck changes were made.";
            EdhBrowseImprovementsButton.IsEnabled = false;
            lastSwapAdvice = null;
            EdhSwapProposalsList.ItemsSource = null;
            EdhSwapHeadline.Text = "Swap Lab: catalog unavailable";
            EdhSwapStatus.Text = "Synced Scryfall metadata is needed for safe swaps.";
            EdhSwapPreviewText.Text = "No draft changes were made.";
            EdhApplySwapButton.IsEnabled = false;
            EdhBalanceNowButton.IsEnabled = false;
        }
    }

    private void OnOwnedSearchChanged(object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        if (edhLabCommander is not null && browsingOptimizationCandidates)
            ResetOptimizationBrowsing();
        if (OwnedSearchBox?.Text.Length > 255)
        {
            StatusText.Text = "Owned-card search must be 255 characters or fewer.";
            return;
        }
        RefreshOwnedCards();
    }

    private void OnOwnedPoolChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshOwnedCards();
        RefreshDraftSummary();
        // Mainboard previews are available even when a card is unowned.
        // Switching physical/Arena pools must never leave a stale owned
        // quantity or an image for the previous pool in the preview.
        if (previewedCard is { } previous && OwnedPoolBox is not null &&
            previewedPool != OwnedPoolBox.SelectedIndex)
        {
            var pool = OwnedPoolBox.SelectedIndex == 1
                ? NativeDeckOwnedPicker.Pool.Arena
                : NativeDeckOwnedPicker.Pool.Physical;
            var updated = previous with
            {
                Owned = NativeDeckOwnedPicker.CountOwned(ownership, pool, previous.Name),
                Proxied = CountProxy(previous.Name)
            };
            _ = ShowCardPreviewAsync(updated);
        }
    }

    private async void OnAnalyzeDeckClicked(object sender, RoutedEventArgs e)
    {
        if (busy || !AnalyzeDeckButton.IsEnabled) return;
        AnalyticsPanel.IsExpanded = true;
        IReadOnlyList<NativeDeckEditing.Entry> entries;
        try
        {
            entries = NativeDeckEditing.ParseMainboard(MainboardBox.Text);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            AnalyticsCharts.Visibility = Visibility.Collapsed;
            AnalyticsText.Text = "Deck analytics unavailable: " + ex.Message;
            return;
        }

        // Snapshot the draft before crossing the async boundary. Late results
        // must never be shown for a different deck or format.
        int version = draftVersion;
        string mainboard = MainboardBox.Text;
        string commander = CommanderBox.Text;
        string format = FormatBox.Text;
        AnalyzeDeckButton.IsEnabled = false;
        AnalyticsCharts.Visibility = Visibility.Collapsed;
        AnalyticsText.Text = "Analyzing offline Scryfall metadata…";
        try
        {
            var report = await Task.Run(() => NativeDeckAnalytics.Analyze(
                entries, commander, format, catalogPath));
            if (version != draftVersion ||
                MainboardBox.Text != mainboard ||
                CommanderBox.Text != commander ||
                FormatBox.Text != format)
            {
                AnalyticsText.Text = "Deck changed during analysis. Select Analyze deck again.";
                return;
            }
            var charts = NativeDeckAnalyticsCharts.FromReport(report);
            ManaCurveBars.ItemsSource = charts.Curve;
            ColorIdentityBars.ItemsSource = charts.Identity;
            CardTypeBars.ItemsSource = charts.Types;
            AverageManaText.Text = report.AverageNonlandManaValue.HasValue
                ? $"Average nonland mana value: {report.AverageNonlandManaValue.Value:F2}"
                : "Average nonland mana value: unavailable";
            AnalyticsText.Text = report.CoverageSummary +
                (report.UnknownCards > 0
                    ? " · Unknown copies excluded from the charts."
                    : " · All deck copies matched offline metadata.");
            AnalyticsCharts.Visibility = Visibility.Visible;
        }
        catch (Exception ex) when (ex is InvalidDataException or
            Microsoft.Data.Sqlite.SqliteException or IOException or
            UnauthorizedAccessException or ArgumentException or OverflowException)
        {
            AnalyticsCharts.Visibility = Visibility.Collapsed;
            AnalyticsText.Text = "Analytics unavailable: " + ex.Message +
                " (the deck and collection were not modified).";
        }
        finally
        {
            AnalyzeDeckButton.IsEnabled = true;
        }
    }

    private void OnOwnedCardPreviewMouseDown(object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        // First click is deliberately selection/preview ONLY. A valid second
        // left click on an actual card row is the sole quick-add gesture.
        // PreviewMouseLeftButtonDown also avoids bubbling MouseDoubleClick
        // from unrelated child controls, empty space and the scrollbar.
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left ||
            e.ClickCount != 2 || OwnedCardsList is null ||
            e.OriginalSource is not DependencyObject source ||
            System.Windows.Controls.ItemsControl.ContainerFromElement(
                OwnedCardsList, source) is not System.Windows.Controls.ListBoxItem row ||
            row.DataContext is not OwnedDisplay clicked)
            return;

        // SelectionChanged remains preview-only. Resolve exactly the row
        // the second click hit, even if a fast filter changed selection.
        if (!ReferenceEquals(clicked, OwnedCardsList.SelectedItem))
            OwnedCardsList.SelectedItem = clicked;
        AddSelectedOwnedCard(singletonDoubleClick: edhLabCommander is not null);
        e.Handled = true;
    }

    private void OnAddOwnedClicked(object sender, RoutedEventArgs e) => AddSelectedOwnedCard();

    private void OnRemoveOwnedClicked(object sender, RoutedEventArgs e)
    {
        if (busy || OwnedCardsList.SelectedItem is not OwnedDisplay card) return;
        try
        {
            MainboardBox.Text = NativeDeckOwnedPicker.RemoveOne(MainboardBox.Text, card.Name);
            if (edhLabCommander is not null)
            {
                RefreshMainboardManager();
                RefreshOwnedCards();
            }
            StatusText.Text = $"Removed 1 {card.Name} from the unsaved draft. " +
                "Collection quantities are unchanged.";
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or OverflowException)
        {
            StatusText.Text = "Could not remove card: " + ex.Message;
        }
    }

    private void OnSortMainboardClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        try
        {
            string sorted = NativeDeckOwnedPicker.SortAlphabetically(MainboardBox.Text);
            if (sorted != MainboardBox.Text)
                MainboardBox.Text = sorted;
            StatusText.Text = "Sorted the unsaved mainboard alphabetically. " +
                "Commander, ownership and saved decks are unchanged.";
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or OverflowException)
        {
            StatusText.Text = "Could not sort draft: " + ex.Message;
        }
    }


    private void AddSelectedOwnedCard(bool singletonDoubleClick = false)
    {
        if (busy || OwnedCardsList.SelectedItem is not OwnedDisplay card) return;
        AddEligibleCardToMainboard(card.Name, singletonDoubleClick,
            singletonDoubleClick ? "Cards double-click" : "Cards Add button");
    }

    private void AddEligibleCardToMainboard(string cardName,
        bool singletonDoubleClick = false, string source = "manual")
    {
        if (busy || string.IsNullOrWhiteSpace(cardName)) return;
        try
        {
            // A still-loading EDH catalog must never be treated as proof of
            // card legality, availability or commander color identity.
            if (edhLabCommander is not null && edhCatalog is null)
            {
                StatusText.Text = "Verified EDH card catalog is still loading. " +
                    "Choose a card after eligibility checks finish.";
                return;
            }
            if (edhCatalog is not null)
            {
                var suggested = edhCatalog.Find(cardName);
                if (suggested is null)
                    throw new InvalidDataException(
                        "This card is not eligible for the selected EDH Lab draft.");
                int inDraft = NativeDeckEditing.ParseMainboard(MainboardBox.Text)
                    .FirstOrDefault(x => x.Name.Equals(cardName,
                        StringComparison.OrdinalIgnoreCase))?.Quantity ?? 0;
                if (singletonDoubleClick && inDraft > 0)
                {
                    StatusText.Text = $"{cardName} is already in the mainboard. " +
                        "Double-click adds at most one copy of each card; " +
                        "use Add (+1) for basic lands or allowed multi-copy cards.";
                    return;
                }
                if (inDraft >= suggested.SuggestedCopyLimit)
                    throw new InvalidDataException(
                        $"{cardName} has reached the locally supported " +
                        $"copy limit ({suggested.SuggestedCopyLimit}).");
            }
            string updated = singletonDoubleClick
                ? NativeDeckOwnedPicker.AddIfAbsent(MainboardBox.Text, cardName)
                : NativeDeckOwnedPicker.AddOne(MainboardBox.Text, cardName);
            if (updated == MainboardBox.Text) return;
            MainboardBox.Text = updated;
            if (edhLabCommander is not null)
            {
                RefreshMainboardManager();
                RefreshOwnedCards();
            }
            UpdatePackageAddButton();
            StatusText.Text = $"Added 1 {cardName} to the unsaved draft via {source}. " +
                "Collection quantities are unchanged; save still requires preflight review.";
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or OverflowException)
        {
            StatusText.Text = "Could not add card: " + ex.Message;
        }
    }

    public NativeDeckEditorWindow(string nativePath, string expectedSha,
        NativeBackupView ownership, string? catalogPath,
        NativeDeckEditing.DeckRow? selected = null,
        string? draftCommander = null,
        EdhLabFormat? draftFormat = null,
        string? draftColors = null,
        EdhLabFormat? savedLabFormat = null,
        string? savedLabColors = null,
        bool focusShortageReview = false,
        string? preferredShortageName = null)
    {
        this.nativePath = nativePath;
        this.expectedSha = expectedSha;
        this.ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
        this.catalogPath = catalogPath;
        this.focusShortageReview = focusShortageReview;
        this.preferredShortageName = preferredShortageName;
        original = selected;
        if (selected is not null && savedLabFormat is not null &&
            (EdhLabSavedDeckAccess.FormatFor(selected.Format) != savedLabFormat ||
             string.IsNullOrWhiteSpace(selected.Commander) ||
             savedLabColors is null))
            throw new ArgumentException(
                "A saved EDH Lab deck needs its verified commander, format, and identity.");
        edhLabCommander = selected is null
            ? draftCommander : savedLabFormat is not null ? selected.Commander : null;
        edhLabFormat = edhLabCommander is null ? null :
            selected is null ? draftFormat : savedLabFormat;
        edhLabColors = selected is null ? draftColors : savedLabColors;
        InitializeComponent();
        InitializeDeckPrintingChoices();
        // Load independent proxy coverage asynchronously. It does not
        // rewrite the deck, own an official printing or fetch any price.
        Loaded += async (_, _) => await LoadProxyCoverageAsync();
        MainboardModes.SelectedItem = MainboardTextTab;
        Closed += (_, _) =>
        {
            edhAnalysisTimer?.Stop();
            previewVersion++;
            previewRequest?.Cancel();
            chainArtworkRequest?.Cancel();
            proxyCoverageClosed = true;
            // Cancel all pending/visible EDH thumbnail fetches on editor close.
            mainboardArtLifetime.Cancel();
        };
        OwnedPoolBox.SelectedIndex = edhLabFormat == EdhLabFormat.ArenaBrawl ||
            selected?.Format.Equals("Arena Brawl",
                StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
        RefreshOwnedCards();
        FormatBox.Text = selected?.Format ?? (edhLabFormat is null
            ? "60-card" : edhLabFormat == EdhLabFormat.ArenaBrawl
                ? "Arena Brawl" : "Commander");
        FolderBox.Text = selected?.Folder ?? "Unfiled";
        FavoriteDeckBox.IsChecked = selected?.Favorite ?? false;
        if (selected is not null)
        {
            HeaderText.Text = "Edit saved deck";
            DeckNameBox.Text = selected.Name;
            CommanderBox.Text = selected.Commander;
            MainboardBox.Text = selected.MainboardText;
            StatusText.Text = $"Editing saved deck: {selected.EntryCount:N0} entries, " +
                $"{selected.TotalCopies:N0} mainboard copies and {selected.Revisions:N0} revisions. " +
                "The prior deck is appended to its revision history before changes are committed.";
        }
        if (edhLabCommander is not null)
        {
            MainboardManageTab.Visibility = Visibility.Visible;
            MainboardModes.SelectedItem = MainboardManageTab;
            HeaderText.Text = selected is null
                ? "EDH Lab — Construct a deck" : "EDH Lab — Edit saved deck";
            OwnedCardsHeading.Text = "Eligible cards · click to inspect, double-click to add";
            EdhClickInstructions.Visibility = Visibility.Visible;
            OwnedCardsList.ToolTip = "Single-click a card to preview its official details. " +
                "Only a double-click adds one copy to the unsaved mainboard. " +
                "Use Add (+1) for additional basic lands or allowed multi-copy cards.";
            Title = "EDH Lab — " + edhLabCommander;
            if (selected is null)
            {
                // A new draft gets defaults; a saved deck keeps its actual name,
                // folder, favorite, commander and mainboard unchanged.
                DeckNameBox.Text = edhLabCommander + " · New Deck";
                CommanderBox.Text = edhLabCommander;
                FolderBox.Text = "EDH Lab";
            }
            FormatBox.IsEnabled = false;
            CommanderBox.IsReadOnly = true;
            OwnedPoolBox.IsEnabled = false;
            EdhSuggestionTools.Visibility = Visibility.Visible;
            EdhManaTab.Visibility = Visibility.Visible;
            EdhImproveTab.Visibility = Visibility.Visible;
            EdhWorkspaceTabs.SelectedItem = EdhCardsTab;
            EdhRecordedOnly.IsChecked = true;
            EdhRoleBox.SelectedIndex = 0;
            EdhStrategyFocusBox.SelectedIndex = 0; // Per-session default, never stored.
            // Fresh drafts get Arena-style baseline basics. Existing decks
            // never opt into edits without a deliberate user choice.
            EdhAutoBalanceLands.IsChecked = selected is null;
            StatusText.Text = selected is null
                ? "Draft is unsaved. Eligible suggestions use the synced local catalog; " +
                  "all saved cards still receive native preflight review."
                : $"Editing saved EDH Lab deck with {selected.Revisions:N0} revisions. " +
                  "Changes remain unsaved until Review and save; prior versions are retained.";
            Loaded += async (_, _) => await LoadEdhSuggestionsAsync();
        }
        RefreshDraftSummary();
    }

    private void ApplyDeckImport(string text)
    {
        if (busy) return;
        try
        {
            var imported = NativeDeckTextExchange.Parse(text);
            string printWarning = imported.RemovedPrintingAnnotations
                ? "This text lists Arena set/collector annotations. The native deck editor " +
                  "stores card names and quantities, NOT those exact printing choices.\n\n"
                : "";
            string commanderWarning = imported.Commander.Length == 0
                ? "No Commander section detected; the current Commander field will be kept.\n\n"
                : $"Commander: {imported.Commander}\n\n";
            string prompt =
                $"Import {imported.MainboardCopies:N0} mainboard copies across " +
                $"{imported.CardNames:N0} card names?\n\n" +
                commanderWarning + printWarning +
                "This replaces the TEXT currently visible in the deck editor; " +
                "it does not save or modify your collection until you press Review and save deck.\n\n" +
                "Continue?";
            if (NativeInAppDialog.Show(this, prompt, "Review imported decklist",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            MainboardBox.Text = imported.MainboardText;
            if (imported.Commander.Length > 0)
                CommanderBox.Text = imported.Commander;
            StatusText.Text = $"Loaded {imported.SourceKind}: " +
                $"{imported.CardNames:N0} card names, {imported.MainboardCopies:N0} copies. " +
                "Not saved yet; review the selected format and press Review and save deck.";
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            StatusText.Text = "Deck import rejected: " + ex.Message;
            NativeInAppDialog.Show(this, ex.Message,
                "Decklist needs review", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnPasteDeckClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        try
        {
            if (!Clipboard.ContainsText())
                throw new InvalidDataException("Clipboard has no plain-text decklist.");
            ApplyDeckImport(Clipboard.GetText());
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Runtime.InteropServices.COMException)
        {
            StatusText.Text = "Unable to paste decklist: " + ex.Message;
        }
    }

    private void OnImportTextClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var picker = new OpenFileDialog
        {
            Title = "Import a plain-text or MTG Arena decklist",
            Filter = "Decklist text (*.txt)|*.txt|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var info = new FileInfo(picker.FileName);
            if (!info.Exists || info.Length is < 1 or > 256_000)
                throw new InvalidDataException("Decklist files must be smaller than 256 KB.");
            ApplyDeckImport(File.ReadAllText(info.FullName, Encoding.UTF8));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            StatusText.Text = "Unable to read decklist: " + ex.Message;
        }
    }

    private void OnCopyDeckClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        try
        {
            string exported = NativeDeckTextExchange.Export(MainboardBox.Text, CommanderBox.Text);
            Clipboard.SetText(exported);
            StatusText.Text = "Copied a name/quantity Arena-style decklist. " +
                "This export does not include printings, sideboards, deck history or ownership.";
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or
            System.Runtime.InteropServices.COMException)
        {
            StatusText.Text = "Could not copy decklist: " + ex.Message;
        }
    }

    private void OnExportTextClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        string exported;
        try { exported = NativeDeckTextExchange.Export(MainboardBox.Text, CommanderBox.Text); }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            StatusText.Text = "Cannot export decklist: " + ex.Message;
            return;
        }
        string filename = new string(
            (DeckNameBox.Text.Trim().Length == 0 ? "ArcaneVaultDeck" : DeckNameBox.Text.Trim())
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());
        var picker = new SaveFileDialog
        {
            Title = "Export current deck as Arena-style text",
            FileName = filename + ".txt",
            Filter = "Decklist text (*.txt)|*.txt",
            AddExtension = true,
            DefaultExt = ".txt",
            OverwritePrompt = true
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            // Export only; never touch the authoritative native SQLite deck library.
            File.WriteAllText(picker.FileName, exported, new UTF8Encoding(false));
            StatusText.Text = "Exported decklist text. Exact card printings, deck history " +
                "and organization remain only in your full native backup.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = "Could not write decklist: " + ex.Message;
        }
    }

    private async void OnSaveDeckClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        string name = DeckNameBox.Text.Trim();
        string commander = CommanderBox.Text.Trim();
        string format = FormatBox.Text.Trim();
        string folder = FolderBox.Text.Trim();
        bool favorite = FavoriteDeckBox.IsChecked == true;
        string mainboard = MainboardBox.Text;
        IReadOnlyList<NativeDeckEditing.Entry> entries;
        try
        {
            entries = NativeDeckEditing.ParseMainboard(mainboard);
            if (name.Length is < 1 or > 120 || name.Any(char.IsControl) ||
                commander.Length > 255 || commander.Any(char.IsControl) ||
                format.Length is < 1 or > 60 || format.Any(char.IsControl) ||
                folder.Length is < 1 or > 64 || folder.Any(char.IsControl))
                throw new InvalidDataException("Invalid deck or commander name.");
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            StatusText.Text = ex.Message;
            return;
        }

        busy = true;
        SaveDeckButton.IsEnabled = false;
        StatusText.Text = "Reviewing the saved cards against native ownership and local Scryfall metadata…";
        try
        {
            // Runs without changing the native database. Reads local catalog in
            // read-only mode and treats unavailable metadata as unverified.
            var report = await Task.Run(() => NativeDeckPreflight.Check(
                ownership, entries, commander, format, catalogPath, proxyLedger));

            int copies = entries.Sum(entry => entry.Quantity);
            string action = original is null ? "CREATE" : "UPDATE";
            var details = report.Findings.Take(18)
                .Select(f => "• " + f.Severity + ": " + f.Message);
            int omitted = Math.Max(0, report.Findings.Count - 18);
            string reportText = string.Join("\n", details);
            if (omitted > 0) reportText += $"\n…plus {omitted} additional review notes.";
            string prompt =
                $"{action} \"{name}\" ({format}) with {copies:N0} mainboard copies across " +
                $"{entries.Count:N0} distinct card names · Folder: {folder}" +
                (favorite ? " · ★ Favorite" : "") +
                (commander.Length > 0 ? $", Commander: {commander}" : ", no commander") +
                $"?\n\nRead-only deck preflight: {report.Summary}\n" +
                (report.CatalogChecked
                    ? "Metadata from the offline Scryfall catalog.\n"
                    : "Catalog legality checks unavailable.\n") +
                (reportText.Length > 0 ? "\n" + reportText + "\n" : "") +
                "\nReview is advisory, not a guarantee of Arena or tournament legality. " +
                "Save anyway? Your physical/Arena inventory will NOT change. " +
                "The complete previous native database will be retained for recovery.";

            // Explicit consent is required even when warnings are present.
            if (NativeInAppDialog.Show(this, prompt, "Review native deck and save",
                MessageBoxButton.YesNo,
                report.Warnings > 0 ? MessageBoxImage.Warning :
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                StatusText.Text = "Save canceled after review. " + report.Summary +
                    ". No collection data changed.";
                return;
            }

            StatusText.Text = "Verifying and saving the native deck with automatic recovery…";
            var savedPrintingChoices = deckPrintingChoices
                .Where(x => entries.Any(e => e.Name.Equals(x.Key,
                    StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(x => x.Key, x => x.Value,
                    StringComparer.OrdinalIgnoreCase);
            var saved = await Task.Run(() =>
                NativeWorkspaceSafety.SaveDeck(nativePath, name, commander,
                    mainboard, original, expectedSha, format: format,
                    folder: folder, favorite: favorite,
                    displayPrintIds: savedPrintingChoices));
            NativeInAppDialog.Show(this,
                $"Deck saved. Library now contains {saved.Current.Collection.Decks:N0} decks " +
                $"and {saved.Current.Collection.DeckRevisions:N0} revisions.\n\n" +
                "The complete prior native collection is preserved at:\n" +
                saved.RecoveryPath,
                "Saved deck verified", MessageBoxButton.OK, MessageBoxImage.Information);
            SavedReplacement = saved;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Review or save did not finish. No automatic reset was performed. " +
                ex.Message;
            NativeInAppDialog.Show(this, StatusText.Text,
                "Deck action stopped", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            busy = false;
            SaveDeckButton.IsEnabled = true;
        }
    }
}
