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
        while (!stopped.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stopped.Token);
            }
            catch (OperationCanceledException) when (stopped.IsCancellationRequested) { break; }
            catch (Exception ex) { Log("Snapshot refresh", ex); }
            try { await Task.Delay(TimeSpan.FromSeconds(15), stopped.Token); }
            catch (OperationCanceledException) { break; }
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
        // Native match history takes precedence. The legacy match-history.sqlite3
        // is used ONLY when present as an optional read-only source of the same
        // MTG Arena match IDs. The legacy program is not run or referenced.
        string native = Path.Combine(NativeWorkspace.RootDirectory, "match-history.sqlite3");
        string legacy = Path.Combine(exportDirectory, "match-history.sqlite3");
        string? file = File.Exists(native) ? native : File.Exists(legacy) ? legacy : null;
        if (file is null) return (null, null, null);
        try
        {
            using var db = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = file, Mode = SqliteOpenMode.ReadOnly, Pooling = false
            }.ToString());
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT result,deck_name FROM matches WHERE result IN ('win','loss') ORDER BY played_utc DESC LIMIT 25000";
            using var reader = cmd.ExecuteReader();
            var unique = decks.GroupBy(x => x.name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            RecentMatch? recent = null;
            string? current = null;
            while (reader.Read())
            {
                string result = reader.GetString(0), name = reader.GetString(1).Trim();
                if (!unique.TryGetValue(name, out var deck)) continue;
                if (result == "win") deck.wins++; else deck.losses++;
                if (recent is null)
                {
                    recent = new RecentMatch(result, deck.name);
                    // Recent match is not necessarily the currently selected deck.
                    current = null;
                }
            }
            return (recent, file == native ? "native" : "legacy-read-only", current);
        }
        catch (Exception ex) when (ex is IOException or SqliteException or
            InvalidOperationException or UnauthorizedAccessException)
        {
            Log("Optional Arena match history unavailable", ex);
            return (null, null, null);
        }
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
        stopped.Dispose();
    }
}
