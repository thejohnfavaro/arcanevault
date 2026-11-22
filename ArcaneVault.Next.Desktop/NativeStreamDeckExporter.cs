using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Read-only adapter for the existing Arcane Vault Stream Deck v0.1 plugin.
/// Native data remains authoritative under ArcaneVaultNext; only a presentation
/// snapshot is written to the original plugin's compatibility path.
/// </summary>
internal sealed class NativeStreamDeckExporter : IDisposable
{
    private readonly CancellationTokenSource stopped = new();
    private readonly string exportDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcaneVault");
    private readonly string diagnosticPath;
    private Task? worker;
    private ArenaMatchTracker? arenaTracker;
    private string? lastWorkspaceStamp;
    private string? lastKnownArenaDeckStamp;
    private DateTimeOffset nextDeckMappingRefresh = DateTimeOffset.MinValue;
    private List<Deck> decks = new();
    private int artworkCursor;
    private DateTimeOffset nextCoverRetry = DateTimeOffset.MinValue;
    private string? lastRefreshNonce;
    private string? selectedRefreshDeckId;
    private DateTimeOffset nextScheduledRefresh = DateTimeOffset.MinValue;
    private bool firstSnapshotPublished;
    private DateTimeOffset nextSetProgressRetry = DateTimeOffset.MinValue;
    private string? setProgressStamp;
    private SetProgressSummary? setProgress;
    private readonly HttpClient seasonClient = new() { Timeout = TimeSpan.FromSeconds(12) };
    private ArenaRankSeason.Schedule? seasonSchedule;
    private DateTimeOffset nextSeasonCheck = DateTimeOffset.MinValue;
    private Task? ongoingSeasonCheck;

    private sealed class Deck
    {
        public string id { get; set; } = "";
        public string name { get; set; } = "";
        public string commander { get; set; } = "";
        public string format { get; set; } = "";
        public string arenaDeckId { get; set; } = "";
        public string commanderDisplayPrintId { get; set; } = "";
        public string coverCardName { get; set; } = "";
        public string coverMode { get; set; } = "Auto";
        public string coverPrintingId { get; set; } = "";
        public string keyImagePath { get; set; } = "";
        public string imagePath { get; set; } = "";
        public string artUrl { get; set; } = "";
        public string artStatus { get; set; } = "Pending artwork lookup";
        public int wins { get; set; }
        public int losses { get; set; }
        public IReadOnlyList<MatchEvent> matchHistory { get; set; } = Array.Empty<MatchEvent>();
    }
    private sealed record MatchEvent(string result, string playedUtc);
    private sealed record SetProgressSummary(int ownedSlots, int totalSlots,
        IReadOnlyList<SetProgressItem> topSets, string? asOf = null,
        IReadOnlyList<SetProgressItem>? sets = null);
    private sealed record SetProgressItem(string code, string name,
        int owned, int total, double percent);
    private sealed record CollectionValue(decimal? value, string currency = "USD",
        string? asOf = null);
    private sealed record SyncInfo(string status, string? lastSync = null);
    private sealed record RecentMatch(string result, string deckName);
    private sealed record RankDisplay(string constructedClass, int constructedLevel,
        string limitedClass, int limitedLevel, string asOfUtc,
        int? constructedStep = null, int? limitedStep = null,
        double? constructedPercentile = null, double? limitedPercentile = null,
        int? constructedLeaderboardPlace = null, int? limitedLeaderboardPlace = null);
    private sealed record Snapshot(
        int schemaVersion, string appPath, string? activeDeckId,
        IReadOnlyList<Deck> decks, CollectionValue collection,
        SyncInfo sync, RecentMatch? recentMatch,
        string? matchSource = null,
        SetProgressSummary? setProgress = null,
        RankDisplay? rank = null,
        string? publishedUtc = null,
        ArenaRankSeason.Schedule? season = null,
        string? predictedConstructed = null,
        string? predictedLimited = null);

    public NativeStreamDeckExporter()
    {
        diagnosticPath = Path.Combine(exportDirectory, "Diagnostics", "streamdeck-export.log");
        seasonSchedule = ArenaRankSeason.Read(ArenaRankSeason.CachePath, DateTimeOffset.UtcNow);
    }
    public void Start() => worker ??= Task.Run(RunAsync);
    private async Task RunAsync()
    {
        // Export a baseline even without a collection, catalog, network or Arena.
        try { Publish(null, null, "Native app connected"); }
        catch (Exception ex) { Log("Initial snapshot", ex); }
        try
        {
            arenaTracker = new ArenaMatchTracker();
            arenaTracker.Configure(enable: true, logFile: null);
        }
        catch (Exception ex) when (ex is IOException or SqliteException or
            InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        { Log("Native Arena match tracker unavailable", ex); }
        while (!stopped.IsCancellationRequested)
        {
            try
            {
                // A Stream Deck key requests a new snapshot immediately.
                // Automatic refresh still occurs every fifteen seconds.
                // Schedule checks are independent of deck/match exporting, run
                // on startup and every 12 hours while the application is open.
                // Network errors are nonfatal and retried in one hour.
                if (DateTimeOffset.UtcNow >= nextSeasonCheck &&
                    (ongoingSeasonCheck is null || ongoingSeasonCheck.IsCompleted))
                {
                    nextSeasonCheck = DateTimeOffset.UtcNow.AddHours(12);
                    ongoingSeasonCheck = RefreshSeasonAsync(stopped.Token);
                }
                bool pressed = ConsumeRefreshRequest();
                if (pressed || DateTimeOffset.UtcNow >= nextScheduledRefresh)
                {
                    // Initial workspace state should reach EDGE before scanning
                    // potentially huge Arena Player.log files. The tracker is
                    // updated after the first publish, then before later refreshes.
                    if (!firstSnapshotPublished)
                    {
                        await RefreshAsync(stopped.Token);
                        firstSnapshotPublished = true;
                        arenaTracker?.Tick();
                    }
                    else
                    {
                        arenaTracker?.Tick();
                        await RefreshAsync(stopped.Token);
                    }
                    nextScheduledRefresh = DateTimeOffset.UtcNow.AddSeconds(15);
                }
            }
            catch (OperationCanceledException) when (stopped.IsCancellationRequested) { break; }
            catch (Exception ex) { Log("Snapshot refresh", ex); }
            try { await Task.Delay(TimeSpan.FromSeconds(1), stopped.Token); }
            catch (OperationCanceledException) { break; }
        }
        arenaTracker?.Dispose();
        stopped.Dispose();
    }

    private async Task RefreshSeasonAsync(CancellationToken cancel)
    {
        try
        {
            var newSchedule = await ArenaRankSeason.CheckAsync(
                ArenaRankSeason.CachePath, DateTimeOffset.UtcNow, seasonClient, cancel);
            seasonSchedule = newSchedule;
            if(newSchedule is null)
                nextSeasonCheck = DateTimeOffset.UtcNow.AddHours(1);
        }
        catch(OperationCanceledException) when(cancel.IsCancellationRequested) { }
        catch(Exception error) when(error is IOException or HttpRequestException or
            TaskCanceledException or InvalidDataException or UnauthorizedAccessException)
        {
            nextSeasonCheck = DateTimeOffset.UtcNow.AddHours(1);
            Log("Official Arena ranked season refresh unavailable",error);
        }
    }

    private async Task RefreshAsync(CancellationToken cancel)
    {
        string workspace = NativeWorkspace.DefaultPath;
        if (!File.Exists(workspace))
        {
            decks = new();
            lastWorkspaceStamp = null;
            Publish(null, null, "Native collection not initialized");
            return;
        }

        // Native workspace verification may be expensive. Don't rescan the
        // full collection unless its SQLite file or the preserved backup changes.
        var info = new FileInfo(workspace);
        string stamp = info.Length + ":" + info.LastWriteTimeUtc.Ticks;
        if (stamp != lastWorkspaceStamp)
        {
            var read = NativeWorkspaceSafety.ListDecks(workspace, cancel)
                .Where(d => !string.IsNullOrWhiteSpace(d.Id) && !string.IsNullOrWhiteSpace(d.Name))
                .Take(1000)
                .Select(d => new Deck
                {
                    id = d.Id, name = d.Name, commander = d.Commander,
                    format = d.Format, arenaDeckId = d.ArenaDeckId,
                    commanderDisplayPrintId = d.CommanderPrintingId,
                    // A custom cover outranks commander and automatic
                    // non-Commander fallback; never changes printing ownership.
                    coverMode = d.DeckCoverMode,
                    coverPrintingId = d.EffectiveCoverPrintingId,
                    coverCardName = d.EffectiveCoverCard
                }).ToList();

            decks = read;
            artworkCursor = 0;
            lastWorkspaceStamp = stamp;
            lastKnownArenaDeckStamp = null;
            nextCoverRetry = DateTimeOffset.MinValue;
        }

        // Prefer exact Arena IDs already saved with native decks. This also
        // safely backfills older unassigned results that carry the exact ID.
        // Never guess a deck from a result's timestamp or deck similarity.
        // Manual match mapping is stored in the separate match-history
        // database. Refresh it periodically even if the collection's deck
        // library has not changed, so the EDGE updates without restart.
        if (arenaTracker is not null &&
            (lastKnownArenaDeckStamp != stamp ||
             DateTimeOffset.UtcNow >= nextDeckMappingRefresh))
        {
            arenaTracker.SetKnownSavedDecks(decks
                .Select(d => (ArenaId: d.arenaDeckId, Name: d.name,
                    NativeId: d.id)));
            lastKnownArenaDeckStamp = stamp;
            nextDeckMappingRefresh = DateTimeOffset.UtcNow.AddSeconds(15);
        }

        // Publish deck and match information before optional cover lookup,
        // image rendering, or set progress queries delay the EDGE refresh.
        // Subsequent publishes enrich this snapshot with artwork and sets.
        var (initialRecent, initialSource, initialActive) = ApplyMatches();
        Publish(initialRecent, initialSource, "Native app connected", initialActive);

        // Card metadata can become available after a catalog sync, without
        // any deck library change. Retry missing artwork independently.
        if (DateTimeOffset.UtcNow >= nextCoverRetry ||
            !string.IsNullOrWhiteSpace(selectedRefreshDeckId))
        {
            RefreshCommanderArtwork(cancel);
            nextCoverRetry = DateTimeOffset.UtcNow.AddSeconds(45);
        }

        var (recent, source, active) =
            (initialRecent, initialSource, initialActive);

        // Render at most three buttons per tick. The selected deck is first
        // when a key or Property Inspector requests a refresh, so a long
        // library doesn't delay the user's chosen tile.
        if (decks.Count == 0)
        {
            RefreshSetSummary(cancel);
            Publish(recent, source, "Native app connected", active);
            return;
        }
        var candidates = new List<Deck>();
        if (!string.IsNullOrWhiteSpace(selectedRefreshDeckId))
        {
            var selected = decks.FirstOrDefault(x => x.id == selectedRefreshDeckId);
            if (selected is not null) candidates.Add(selected);
        }
        selectedRefreshDeckId = null;
        for (int scan = 0; scan < decks.Count && candidates.Count < 3; scan++)
        {
            var candidate = decks[(artworkCursor++) % decks.Count];
            if (!candidates.Contains(candidate)) candidates.Add(candidate);
        }
        foreach (var deck in candidates)
        {
            cancel.ThrowIfCancellationRequested();
            await RenderDeckAsync(deck, cancel);
        }
        // Optional set summary work follows the populated snapshot, so a slow
        // catalog read cannot delay the deck list or first rendered key.
        RefreshSetSummary(cancel);
        Publish(recent, source, "Native app connected", active);
    }

    private void RefreshSetSummary(CancellationToken cancel)
    {
        string catalog = NativeWorkspace.CatalogPath;
        if (!File.Exists(catalog))
        {
            setProgress = null;
            setProgressStamp = null;
            return;
        }
        try
        {
            var workspace = new FileInfo(NativeWorkspace.DefaultPath);
            var info = new FileInfo(catalog);
            string stamp = workspace.Length + ":" + workspace.LastWriteTimeUtc.Ticks
                + ":" + info.Length + ":" + info.LastWriteTimeUtc.Ticks;
            if (stamp == setProgressStamp && setProgress is not null) return;
            if (DateTimeOffset.UtcNow < nextSetProgressRetry) return;
            nextSetProgressRetry = DateTimeOffset.UtcNow.AddMinutes(5);
            var verified = NativeWorkspace.Open(NativeWorkspace.DefaultPath, cancel);
            var reviewed = CollectionCatalogReview.Examine(
                verified.Collection, catalog, cancel);
            var summary = NativeSetProgressCache.Physical(
                reviewed, catalog, verified.BackupSha256, cancellation: cancel);
            var top = summary.Sets.Where(x => x.OwnedSlots > 0)
                .OrderByDescending(x => x.OwnedSlots)
                .ThenBy(x => x.SetName, StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .Select(x => new SetProgressItem(x.SetCode, x.SetName,
                    x.OwnedSlots, x.TotalSlots,
                    Math.Round(x.Percent, 1)))
                .ToArray();
            // All verified set summaries are exported for EDGE touch selection.
            // Only counts and set identifiers are exposed, never the
            // underlying individual owned cards or database records.
            var allSets = summary.Sets
                .Where(x => x.TotalSlots > 0)
                .OrderBy(x => x.SetName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.SetCode, StringComparer.OrdinalIgnoreCase)
                .Take(2000)
                .Select(x => new SetProgressItem(x.SetCode, x.SetName,
                    x.OwnedSlots, x.TotalSlots, Math.Round(x.Percent, 1)))
                .ToArray();
            setProgress = new SetProgressSummary(
                summary.ConfirmedOwnedSlots, summary.CatalogPaperSlots,
                top, DateTimeOffset.UtcNow.ToString("O"), allSets);
            setProgressStamp = stamp;
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        { throw; }
        catch (Exception ex) when (ex is IOException or SqliteException or
            InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { Log("Optional Stream Deck set progress unavailable", ex); }
    }

    private void RefreshCommanderArtwork(CancellationToken cancel)
    {
        var missing = decks.Where(d => string.IsNullOrWhiteSpace(d.artUrl)
                && !string.IsNullOrWhiteSpace(d.coverCardName))
            .OrderByDescending(d => d.id == selectedRefreshDeckId)
            .Take(12).ToArray();
        foreach (var empty in decks.Where(d => string.IsNullOrWhiteSpace(d.coverCardName)))
            empty.artStatus = "No eligible deck cover card in saved mainboard";
        if (missing.Length == 0) return;

        string catalog = NativeWorkspace.CatalogPath;
        if (!File.Exists(catalog))
        {
            foreach (var deck in missing)
                deck.artStatus = "Offline Scryfall catalog not installed";
            return;
        }
        try
        {
            var verified = NativeWorkspace.Open(NativeWorkspace.DefaultPath, cancel);
            var covers = NativeDeckCoverCache.Get(
                missing.Select(d => d.coverCardName), catalog, verified.BackupSha256,
                cancellation: cancel);
            foreach (var deck in missing)
            {
                cancel.ThrowIfCancellationRequested();
                string? url = covers.GetValueOrDefault(deck.coverCardName);
                // The saved deck's custom cover printing outranks its default
                // card art; fallback uses commander printing for Auto mode.
                if (Guid.TryParse(deck.coverPrintingId, out var printId))
                {
                    try
                    {
                        url = NativePrintingArtwork.Require(catalog,
                            deck.coverCardName, printId).ImageUrl;
                    }
                    catch (Exception ex) when (ex is SqliteException or
                        InvalidDataException or IOException or ArgumentException)
                    {
                        Log("Deck-selected commander artwork unavailable", ex);
                    }
                }
                // The cards table may have no canonical image for a special
                // multi-face name. Fall back to the official printings table.
                if (!NativeCardArtwork.IsTrustedArtworkUrl(url))
                {
                    try
                    {
                        url = NativePrintingArtwork.Read(catalog,
                            deck.coverCardName, limit: 20, cancellation: cancel)
                            .Printings.Select(p => p.ImageUrl)
                            .FirstOrDefault(NativeCardArtwork.IsTrustedArtworkUrl);
                    }
                    catch (Exception ex) when (ex is SqliteException or
                        InvalidDataException or IOException or ArgumentException)
                    { Log("Commander printing-art lookup unavailable", ex); }
                }
                if (NativeCardArtwork.IsTrustedArtworkUrl(url))
                {
                    deck.artUrl = url!;
                    deck.artStatus = "Artwork URL found; preparing key image";
                }
                else
                    deck.artStatus = "No official artwork for saved deck card: " +
                        deck.coverCardName;
            }
        }
        catch (Exception ex) when (ex is SqliteException or
            InvalidDataException or IOException or UnauthorizedAccessException
            or ArgumentException)
        {
            foreach (var deck in missing)
                deck.artStatus = "Artwork catalog read failed (see diagnostics)";
            Log("Commander artwork lookup failed", ex);
        }
    }

    private bool ConsumeRefreshRequest()
    {
        var path = Path.Combine(exportDirectory, "streamdeck-refresh.request");
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4096) return false;
            using var request = JsonDocument.Parse(File.ReadAllText(path));
            var data = request.RootElement;
            if (!data.TryGetProperty("nonce", out var nonceElement)) return false;
            string? nonce = nonceElement.GetString();
            if (string.IsNullOrWhiteSpace(nonce) || nonce == lastRefreshNonce) return false;
            lastRefreshNonce = nonce;
            selectedRefreshDeckId =
                data.TryGetProperty("deckId", out var deckElement)
                    && deckElement.ValueKind == JsonValueKind.String
                    ? deckElement.GetString() : null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or
            InvalidOperationException or UnauthorizedAccessException)
        { Log("Refresh request unavailable", ex); return false; }
    }

    /// <summary>
    /// Official Scryfall card images have a corresponding art_crop CDN size.
    /// Use only the trusted Scryfall host and known image-size path segments.
    /// Other image URLs remain unchanged and use the framed-card fallback.
    /// </summary>
    private static string? ArtCropUrl(string original)
    {
        if (!NativeCardArtwork.IsTrustedArtworkUrl(original)) return null;
        var uri = new Uri(original);
        var segments = uri.AbsolutePath.Split('/');
        if (segments.Length < 4) return null;
        var known = new[] { "normal", "large", "small", "png", "border_crop" };
        if (!known.Contains(segments[1], StringComparer.OrdinalIgnoreCase))
            return null;
        bool wasPng = segments[1].Equals("png", StringComparison.OrdinalIgnoreCase);
        segments[1] = "art_crop";
        if (wasPng && segments[^1].EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            segments[^1] = segments[^1][..^4] + ".jpg";
        var builder = new UriBuilder(uri) { Path = string.Join("/", segments) };
        string candidate = builder.Uri.AbsoluteUri;
        return NativeCardArtwork.IsTrustedArtworkUrl(candidate) ? candidate : null;
    }

    private async Task RenderDeckAsync(Deck deck, CancellationToken cancel)
    {
        byte[]? art = null;
        bool fullCardFallback = false;
        string? selectedArtworkUrl = null;
        if (NativeCardArtwork.IsTrustedArtworkUrl(deck.artUrl))
        {
            string? crop = ArtCropUrl(deck.artUrl);
            if (crop is not null)
            {
                try
                {
                    art = await NativeCardArtwork.LoadAsync(crop, cancel);
                    if (art is not null) selectedArtworkUrl = crop;
                }
                catch (Exception ex) when (ex is IOException or HttpRequestException or
                    UnauthorizedAccessException or TaskCanceledException or InvalidOperationException)
                { Log("Scryfall art crop unavailable; trying full card", ex); }
            }
            if (art is null)
            {
                try
                {
                    art = await NativeCardArtwork.LoadAsync(deck.artUrl, cancel);
                    fullCardFallback = art is not null;
                    if (fullCardFallback) selectedArtworkUrl = deck.artUrl;
                }
                catch (Exception ex) when (ex is IOException or HttpRequestException or
                    UnauthorizedAccessException or TaskCanceledException or InvalidOperationException)
                { Log("Deck fallback artwork unavailable", ex); }
            }
            deck.artStatus = art is null
                ? "Scryfall art crop and fallback image unavailable"
                : fullCardFallback
                    ? "Art crop unavailable; using cropped card illustration"
                    : "Commander art crop loaded";
        }
        var folder = Path.Combine(exportDirectory, "StreamDeckArtwork");
        Directory.CreateDirectory(folder);
        string signature = deck.id + "|" + deck.name + "|" + deck.wins + "|" +
            deck.losses + "|art-crop-v1|" +
            (selectedArtworkUrl ?? "no-art");
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(signature)));
        string keyFile = Path.Combine(folder, "key-" + key + ".png");
        if (!File.Exists(keyFile))
        {
            try
            {
                var png = await NativeStreamDeckTileRenderer.DrawAsync(
                    deck.name, deck.wins, deck.losses, art, cancel,
                    fullCardFallback);
                if (png.Length is > 0 and < 2_000_000)
                {
                    string stage = keyFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        await File.WriteAllBytesAsync(stage, png, cancel);
                        File.Move(stage, keyFile, overwrite: true);
                    }
                    finally { if (File.Exists(stage)) File.Delete(stage); }
                }
            }
            catch (Exception ex) when (ex is IOException or
                UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
            { Log("Deck tile rendering unavailable", ex); }
        }
        if (File.Exists(keyFile))
        {
            deck.keyImagePath = keyFile;
            if (art is not null) deck.artStatus = fullCardFallback
                ? "Illustration extracted from full card; key tile ready"
                : "Commander art crop tile ready";
            else if (string.IsNullOrWhiteSpace(deck.artUrl))
                deck.artStatus = "Text-only tile ready; commander art unavailable";
        }
    }

    private (RecentMatch? Recent, string? Source, string? ActiveDeck) ApplyMatches()
    {
        foreach (var deck in decks)
        {
            deck.wins = 0;
            deck.losses = 0;
            deck.matchHistory = Array.Empty<MatchEvent>();
        }
        var histories = new Dictionary<string, List<MatchEvent>>(StringComparer.Ordinal);
        string native = Path.Combine(NativeWorkspace.RootDirectory, "match-history.sqlite3");
        string legacy = Path.Combine(exportDirectory, "match-history.sqlite3");
        var files = new[] { native, legacy }.Where(File.Exists).ToArray();
        var unique = decks.GroupBy(x => x.name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var byNativeId = decks.ToDictionary(d => d.id, StringComparer.Ordinal);
        // Build exactly the same verified Arena-ID relationships used by
        // Match History. Saved mappings are persisted in the native history DB,
        // not necessarily embedded in the saved-deck workspace.
        var arenaGroups = decks.Where(d => !string.IsNullOrWhiteSpace(d.arenaDeckId))
            .GroupBy(d => d.arenaDeckId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var blockedArenaIds = arenaGroups.Where(g => g.Count() != 1)
            .Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uniqueArenaIds = arenaGroups.Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        if (File.Exists(native))
        {
            try
            {
                using var mappingDb = new SqliteConnection(new SqliteConnectionStringBuilder {
                    DataSource = native, Mode = SqliteOpenMode.ReadOnly, Pooling = false
                }.ToString());
                mappingDb.Open();
                using var query = mappingDb.CreateCommand();
                query.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='deck_mappings'";
                if (query.ExecuteScalar() is not null)
                {
                    query.CommandText = "SELECT arena_deck_id,native_deck_id FROM deck_mappings";
                    using var mappingRows = query.ExecuteReader();
                    while (mappingRows.Read())
                    {
                        string arenaId = mappingRows.GetString(0).Trim();
                        string nativeId = mappingRows.GetString(1).Trim();
                        if (arenaId.Length == 0 || blockedArenaIds.Contains(arenaId) ||
                            !byNativeId.TryGetValue(nativeId, out var mapped)) continue;
                        if (uniqueArenaIds.TryGetValue(arenaId, out var existing) &&
                            existing.id != mapped.id)
                        {
                            // A conflict is never resolved by choosing whichever
                            // source happened to be read last.
                            blockedArenaIds.Add(arenaId);
                            uniqueArenaIds.Remove(arenaId);
                            continue;
                        }
                        uniqueArenaIds[arenaId] = mapped;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or SqliteException or
                InvalidOperationException or UnauthorizedAccessException)
            { Log("Verified Stream Deck deck mappings unavailable", ex); }
        }
        var resolvedDecks = decks.Select(d =>
            new MatchDeckAttribution.Deck(d.id, d.name, d.arenaDeckId)).ToArray();
        var arenaMap = uniqueArenaIds.ToDictionary(x => x.Key, x => x.Value.id,
            StringComparer.OrdinalIgnoreCase);
        var knownMatches = new HashSet<string>(StringComparer.Ordinal);
        RecentMatch? recent = null;
        DateTimeOffset? latest = null;
        var sources = new List<string>();
        foreach (var file in files)
        {
            try
            {
                using var db = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = file, Mode = SqliteOpenMode.ReadOnly, Pooling = false
                }.ToString());
                db.Open();
                using var cmd = db.CreateCommand();
                // Legacy match-history databases may not yet include the
                // optional stable Arcane Vault deck identity column.
                using var schema = db.CreateCommand();
                schema.CommandText = "PRAGMA table_info(matches)";
                bool hasNativeDeckId = false;
                using (var cols = schema.ExecuteReader())
                    while (cols.Read())
                        if (cols.GetString(1).Equals("native_deck_id",
                            StringComparison.OrdinalIgnoreCase))
                            hasNativeDeckId = true;
                cmd.CommandText = "SELECT match_id,result,deck_name,played_utc,deck_id," +
                    (hasNativeDeckId ? "native_deck_id" : "''") +
                    ",note,format FROM matches WHERE result IN ('win','loss') " +
                    "ORDER BY played_utc DESC LIMIT 25000";
                using var reader = cmd.ExecuteReader();
                bool used = false;
                while (reader.Read())
                {
                    string id = reader.GetString(0);
                    string result = reader.GetString(1);
                    string name = reader.IsDBNull(2) ? "" : reader.GetString(2).Trim();
                    string arenaId = reader.IsDBNull(4) ? "" : reader.GetString(4).Trim();
                    string nativeId = reader.IsDBNull(5) ? "" : reader.GetString(5).Trim();
                    // Match History displays the native database first.
                    // Deduplicate BEFORE attribution so a stale legacy copy
                    // cannot replace the same match with different evidence.
                    if (id.Length == 0 || !knownMatches.Add(id)) continue;
                    string note = reader.IsDBNull(6) ? "" : reader.GetString(6);
                    string rawQueue = reader.IsDBNull(7) ? "" : reader.GetString(7);
                    string queue = ArenaQueueInfo.Label(rawQueue);
                    string? resolvedId = MatchDeckAttribution.Resolve(
                        new MatchDeckAttribution.Match(id, name, arenaId, nativeId, note),
                        resolvedDecks, arenaMap, blockedArenaIds);
                    if (resolvedId is null || !byNativeId.TryGetValue(resolvedId, out var deck))
                        continue;
                    // The generic Arena "Ladder" queue does not identify its
                    // Constructed format. Accept it only with a stable, uniquely
                    // mapped Arena deck ID or already-confirmed native deck ID;
                    // never assume format from a deck name alone.
                    bool exactNative = nativeId.Equals(deck.id, StringComparison.Ordinal);
                    bool exactArena = arenaId.Length > 0 &&
                        !blockedArenaIds.Contains(arenaId) &&
                        arenaMap.TryGetValue(arenaId, out string? mappedId) &&
                        mappedId.Equals(deck.id, StringComparison.Ordinal);
                    // Historical Arena submissions and explicit Match History
                    // corrections can retain their verified assignment in
                    // the note even when their native deck ID was not stored.
                    // Resolve already requires a unique matching saved deck;
                    // never allow an unknown or conflicting Arena ID to count.
                    bool verifiedHistoricalAssignment =
                        name.Equals(deck.name, StringComparison.OrdinalIgnoreCase) &&
                        !blockedArenaIds.Contains(arenaId) &&
                        MatchDeckAttribution.HasVerifiedAssignmentNote(note);
                    if (deck.format.Equals("Arena Standard", StringComparison.OrdinalIgnoreCase) &&
                        !ArenaQueueInfo.IsRankedStandardMatch(rawQueue,
                            exactNative || exactArena || verifiedHistoricalAssignment))
                        continue;
                    if (deck.format.Equals("Arena Brawl", StringComparison.OrdinalIgnoreCase) &&
                        (queue.Equals("Standard", StringComparison.OrdinalIgnoreCase) ||
                         queue.Equals("Standard Ranked", StringComparison.OrdinalIgnoreCase)))
                        continue;
                    used = true;
                    if (result == "win") deck.wins++; else deck.losses++;
                    if (!histories.TryGetValue(deck.id, out var history))
                    {
                        history = new List<MatchEvent>();
                        histories[deck.id] = history;
                    }
                    // Date and outcome only. No match identifiers, names,
                    // opponents, tokens or Arena logs are exported.
                    if (DateTimeOffset.TryParse(reader.GetString(3), out var played))
                        history.Add(new MatchEvent(result, played.ToUniversalTime().ToString("O")));
                    if (DateTimeOffset.TryParse(reader.GetString(3), out var when) &&
                        (!latest.HasValue || when > latest.Value))
                    {
                        latest = when;
                        recent = new RecentMatch(result, deck.name);
                    }
                }
                if (used) sources.Add(file == native ? "native" : "legacy-read-only");
            }
            catch (Exception ex) when (ex is IOException or SqliteException or
                InvalidOperationException or UnauthorizedAccessException)
            { Log("Optional Arena match history unavailable", ex); }
        }
        foreach (var deck in decks)
            if (histories.TryGetValue(deck.id, out var history))
                deck.matchHistory = history
                    .OrderByDescending(x => x.playedUtc, StringComparer.Ordinal)
                    .Take(20).ToArray();
        string? chosen = arenaTracker?.CurrentDeckName;
        string? chosenId = arenaTracker?.CurrentDeckId;
        Deck? selected = null;
        if (!string.IsNullOrWhiteSpace(chosenId))
            uniqueArenaIds.TryGetValue(chosenId.Trim(), out selected);
        if (selected is null && string.IsNullOrWhiteSpace(chosenId) &&
            !string.IsNullOrWhiteSpace(chosen))
            unique.TryGetValue(chosen.Trim(), out selected);
        string? active = selected?.id;
        return (recent, sources.Count == 0 ? null : string.Join("+", sources), active);
    }

    private void Publish(RecentMatch? recent, string? source, string status,
        string? activeDeckId = null)
    {
        var last = NativeValueHistory.Read().LastOrDefault();
        decimal? value = last is not null ? last.Usd : null;
        string? asOf = last?.Day.ToString("yyyy-MM-dd");
        RankDisplay? rank = null;
        try
        {
            // Separate account-level rank snapshot, never inferred from the
            // selected deck's results or a match's format.
            using var history = new MatchHistory();
            var recentRank = history.LatestRank();
            if (recentRank is not null)
                rank = new RankDisplay(recentRank.ConstructedClass,
                    recentRank.ConstructedLevel, recentRank.LimitedClass,
                    recentRank.LimitedLevel, recentRank.WhenUtc,
                    recentRank.ConstructedStep, recentRank.LimitedStep,
                    recentRank.ConstructedPercentile, recentRank.LimitedPercentile,
                    recentRank.ConstructedLeaderboardPlace,
                    recentRank.LimitedLeaderboardPlace);
        }
        catch (Exception ex) when (ex is IOException or SqliteException or
            InvalidOperationException or UnauthorizedAccessException)
        { Log("Arena rank snapshot unavailable", ex); }
        var schedule = seasonSchedule;
        if(schedule is not null &&
            (!DateTimeOffset.TryParse(schedule.endsUtc,out var end) ||
             end <= DateTimeOffset.UtcNow))
            schedule = null;
        var predictedConstructed = ArenaRankSeason.Predicted(schedule,
            rank?.constructedClass,rank?.constructedLevel ?? 0,DateTimeOffset.UtcNow);
        var predictedLimited = ArenaRankSeason.Predicted(schedule,
            rank?.limitedClass,rank?.limitedLevel ?? 0,DateTimeOffset.UtcNow);
        var snapshot = new Snapshot(1,
            Environment.ProcessPath ?? "",
            activeDeckId, decks.ToArray(),
            new CollectionValue(value, "USD", asOf),
            new SyncInfo(status, asOf), recent, source, setProgress, rank,
            DateTimeOffset.UtcNow.ToString("O"),schedule,
            predictedConstructed,predictedLimited);
        Directory.CreateDirectory(exportDirectory);
        string destination = Path.Combine(exportDirectory, "streamdeck.json");
        string stage = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(stage, JsonSerializer.Serialize(snapshot));
            File.Move(stage, destination, overwrite: true);
        }
        finally { if (File.Exists(stage)) File.Delete(stage); }
    }

    private void Log(string context, Exception error)
    {
        Debug.WriteLine("Native Stream Deck: " + context + ": " + error);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(diagnosticPath)!);
            File.AppendAllText(diagnosticPath,
                DateTimeOffset.Now.ToString("O") + " " + context + ": " + error + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        stopped.Cancel();
        seasonClient.Dispose();
        // The worker reads immutable snapshots and is canceled cooperatively.
        // Do not block WPF shutdown waiting for a log file read.
        // Native match tracker is disposed by the background loop.
    }
}
