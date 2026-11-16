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
    private List<Deck> decks = new();
    private int artworkCursor;

    private sealed class Deck
    {
        public string id { get; set; } = "";
        public string name { get; set; } = "";
        public string commander { get; set; } = "";
        public string imagePath { get; set; } = "";
        public string artUrl { get; set; } = "";
        public int wins { get; set; }
        public int losses { get; set; }
    }
    private sealed record CollectionValue(decimal? value, string currency = "USD",
        string? asOf = null);
    private sealed record SyncInfo(string status, string? lastSync = null);
    private sealed record RecentMatch(string result, string deckName);
    private sealed record Snapshot(
        int schemaVersion, string appPath, string? activeDeckId,
        IReadOnlyList<Deck> decks, CollectionValue collection,
        SyncInfo sync, RecentMatch? recentMatch,
        string? matchSource = null);

    public NativeStreamDeckExporter()
    {
        diagnosticPath = Path.Combine(exportDirectory, "Diagnostics", "streamdeck-export.log");
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
                arenaTracker?.Tick();
                await RefreshAsync(stopped.Token);
            }
            catch (OperationCanceledException) when (stopped.IsCancellationRequested) { break; }
            catch (Exception ex) { Log("Snapshot refresh", ex); }
            try { await Task.Delay(TimeSpan.FromSeconds(15), stopped.Token); }
            catch (OperationCanceledException) { break; }
        }
        arenaTracker?.Dispose();
        stopped.Dispose();
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
                    id = d.Id, name = d.Name, commander = d.Commander
                }).ToList();

            // Don't let cover metadata failure suppress deck selection.
            if (File.Exists(NativeWorkspace.CatalogPath))
            {
                try
                {
                    var verified = NativeWorkspace.Open(workspace, cancel);
                    var covers = NativeDeckCoverCache.Get(
                        read.Select(d => d.commander).Where(x => x.Length > 0),
                        NativeWorkspace.CatalogPath, verified.BackupSha256,
                        cancellation: cancel);
                    foreach (var deck in read)
                        if (covers.TryGetValue(deck.commander, out string? url))
                            deck.artUrl = url;
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or
                    SqliteException or UnauthorizedAccessException or ArgumentException)
                { Log("Cover metadata unavailable", ex); }
            }
            decks = read;
            artworkCursor = 0;
            lastWorkspaceStamp = stamp;
        }

        var (recent, source, active) = ApplyMatches();
        Publish(recent, source, "Native app connected", active);

        // Artwork is optional; never delay the first populated deck-list snapshot.
        if (decks.Count == 0) return;
        int attempts = 0;
        int inspected = 0;
        while (inspected++ < decks.Count && attempts < 2)
        {
            cancel.ThrowIfCancellationRequested();
            var deck = decks[(artworkCursor++) % decks.Count];
            if (deck.imagePath.Length > 0 || !NativeCardArtwork.IsTrustedArtworkUrl(deck.artUrl))
                continue;
            attempts++;
            try
            {
                byte[]? bytes = await NativeCardArtwork.LoadAsync(deck.artUrl, cancel);
                if (bytes is null || bytes.Length < 8 || bytes.Length > 2_000_000) continue;
                string ext = bytes[0] == 137 && bytes[1] == 80 ? ".png" :
                    bytes[0] == 255 && bytes[1] == 216 ? ".jpg" : "";
                if (ext.Length == 0) continue;
                string folder = Path.Combine(exportDirectory, "StreamDeckArtwork");
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder,
                    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(deck.artUrl))) + ext);
                if (!File.Exists(file)) await File.WriteAllBytesAsync(file, bytes, cancel);
                deck.imagePath = file;
            }
            catch (Exception ex) when (ex is IOException or HttpRequestException or
                UnauthorizedAccessException or TaskCanceledException or InvalidOperationException)
            { Log("Deck artwork unavailable", ex); }
        }
        Publish(recent, source, "Native app connected", active);
    }

    private (RecentMatch? Recent, string? Source, string? ActiveDeck) ApplyMatches()
    {
        foreach (var deck in decks) { deck.wins = 0; deck.losses = 0; }
        string native = Path.Combine(NativeWorkspace.RootDirectory, "match-history.sqlite3");
        string legacy = Path.Combine(exportDirectory, "match-history.sqlite3");
        var files = new[] { native, legacy }.Where(File.Exists).ToArray();
        var unique = decks.GroupBy(x => x.name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
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
                cmd.CommandText = "SELECT match_id,result,deck_name,played_utc FROM matches " +
                    "WHERE result IN ('win','loss') ORDER BY played_utc DESC LIMIT 25000";
                using var reader = cmd.ExecuteReader();
                bool used = false;
                while (reader.Read())
                {
                    string id = reader.GetString(0);
                    string result = reader.GetString(1), name = reader.GetString(2).Trim();
                    if (id.Length == 0 || !unique.TryGetValue(name, out var deck) ||
                        !knownMatches.Add(id)) continue;
                    used = true;
                    if (result == "win") deck.wins++; else deck.losses++;
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
        string? chosen = arenaTracker?.CurrentDeckName;
        string? active = !string.IsNullOrWhiteSpace(chosen) &&
            unique.TryGetValue(chosen.Trim(), out var selected) ? selected.id : null;
        return (recent, sources.Count == 0 ? null : string.Join("+", sources), active);
    }

    private void Publish(RecentMatch? recent, string? source, string status,
        string? activeDeckId = null)
    {
        var last = NativeValueHistory.Read().LastOrDefault();
        decimal? value = last is not null ? last.Usd : null;
        string? asOf = last?.Day.ToString("yyyy-MM-dd");
        var snapshot = new Snapshot(1,
            Environment.ProcessPath ?? "",
            activeDeckId, decks.ToArray(),
            new CollectionValue(value, "USD", asOf),
            new SyncInfo(status, asOf), recent, source);
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
        // The worker reads immutable snapshots and is canceled cooperatively.
        // Do not block WPF shutdown waiting for a log file read.
        // Native match tracker is disposed by the background loop.
    }
}
