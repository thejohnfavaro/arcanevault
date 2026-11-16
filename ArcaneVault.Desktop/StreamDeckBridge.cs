using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArcaneVault.Desktop;

/// <summary>
/// Read-only Stream Deck export. The WebView owns the deck library and physical valuation;
/// MatchHistory owns wins and losses. No duplicate match counter is maintained here.
/// </summary>
internal sealed class StreamDeckBridge : IDisposable
{
    private readonly WebView2 browser;
    private readonly LocalAppServer server;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 5000 };
    private readonly HttpClient artworkClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly string outputFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcaneVault");
    private bool busy;
    private bool disposed;
    private const string Script = """
        (() => {
          try {
            const read = (k, fallback) => {
              try { return JSON.parse(localStorage.getItem('vault_' + k)) ?? fallback; }
              catch { return fallback; }
            };
            const decks = read('libraryV3', []);
            const saved = Array.isArray(decks) ? decks : [];
            const cards = read('physicalCollectionV1', []);
            const prices = read('physicalValuesV1', null);
            let total = 0;
            if (prices?.at && prices.quotes && Array.isArray(cards)) {
              for (const card of cards) {
                const key = [
                  card.name,card.set,card.number,card.finish || 'nonfoil',
                  card.language,card.condition,card.location,card.scryfallId
                ].map(x => String(x || '').trim().toLowerCase()).join('|');
                const price = Number(prices.quotes[key]?.usd);
                const qty = Number(card.qty);
                if (Number.isFinite(price) && price > 0 && Number.isFinite(qty) && qty > 0)
                  total += price * qty;
              }
            }
            return {
              decks: saved.filter(d => d && d.id && d.name).map(d => ({
                id: String(d.id), name: String(d.name), commander: String(d.commander || ''),
                arenaDeckId: String(d.arenaDeckId || ''),
                artUrl: typeof libraryArtUrl === 'function'
                  ? libraryArtUrl(d.coverCard || d.commander || d.entries?.[0]?.name || '')
                  : '',
              })),
              activeDeckId: typeof activeDeckId !== 'undefined' ? activeDeckId : null,
              collection: { value: prices?.at ? total : null, currency: 'USD' },
              sync: { status: 'App connected', lastSync: prices?.at || null }
            };
          } catch (error) { return { error: String(error), decks: [] }; }
        })()
        """;
    private sealed class Input
    {
        public List<Deck> decks { get; set; } = new();
        public string? activeDeckId { get; set; }
        public ValueInfo? collection { get; set; }
        public SyncInfo? sync { get; set; }
        public string? error { get; set; }
    }
    private sealed class Deck
    {
        public string id { get; set; } = "";
        public string name { get; set; } = "";
        public string commander { get; set; } = "";
        public string arenaDeckId { get; set; } = "";
        public string artUrl { get; set; } = "";
        public string imagePath { get; set; } = "";
        public int wins { get; set; }
        public int losses { get; set; }
    }
    private sealed class ValueInfo { public decimal? value { get; set; } public string currency { get; set; } = "USD"; }
    private sealed class SyncInfo { public string status { get; set; } = ""; public string? lastSync { get; set; } }
    private sealed class Recent { public string result { get; set; } = ""; public string deckName { get; set; } = ""; }
    private sealed class Output
    {
        public List<Deck> decks { get; set; } = new();
        public string? activeDeckId { get; set; }
        public ValueInfo? collection { get; set; }
        public SyncInfo? sync { get; set; }
        public Recent? recentMatch { get; set; }
        public string appPath { get; set; } = Application.ExecutablePath;
    }

    public StreamDeckBridge(WebView2 browser, LocalAppServer server)
    {
        this.browser = browser;
        this.server = server;
        artworkClient.DefaultRequestHeaders.UserAgent.ParseAdd("ArcaneVault/5.21 StreamDeck-Companion");
        timer.Tick += async (_, _) => await ExportAsync();
    }
    public void Start()
    {
        timer.Start();
        _ = ExportAsync();
    }
    private async Task ExportAsync()
    {
        if (disposed || busy || browser.CoreWebView2 is null) return;
        busy = true;
        try
        {
            var raw = await browser.ExecuteScriptAsync(Script);
            var input = JsonSerializer.Deserialize<Input>(raw);
            if (input is null || input.error is not null || input.decks is null)
            {
                WriteDiagnostic("WebView snapshot unavailable: " + (input?.error ?? raw[..Math.Min(raw.Length, 300)]));
                return;
            }
            var output = new Output
            {
                decks = input.decks.Take(1000).ToList(),
                activeDeckId = input.activeDeckId,
                collection = input.collection,
                sync = input.sync
            };
            // The Arena log's explicitly selected deck takes precedence over Studio's
            // active editor. Only map the name if it identifies one saved deck.
            var arenaName = server.CurrentArenaDeckName;
            if (!string.IsNullOrWhiteSpace(arenaName))
            {
                var candidates = output.decks.Where(d => string.Equals(
                    d.name.Trim(), arenaName.Trim(), StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
                if (candidates.Length == 1) output.activeDeckId = candidates[0].id;
            }
            // Write a usable baseline immediately. Match history and art are optional:
            // a corrupt/locked database must not hide the entire deck selector.
            await WriteSnapshotAsync(output);
            var matchDb = Path.Combine(outputFolder, "match-history.sqlite3");
            try
            {
              if (File.Exists(matchDb))
              {
                using var db = new SqliteConnection("Data Source=" + matchDb + ";Mode=ReadOnly");
                db.Open();
                using var command = db.CreateCommand();
                command.CommandText = @"SELECT result, deck_id, deck_name FROM matches
                    WHERE result IN ('win', 'loss') ORDER BY played_utc DESC";
                using var rows = command.ExecuteReader();
                var byId = output.decks.Where(d => d.arenaDeckId.Length > 0)
                    .GroupBy(d => d.arenaDeckId, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() == 1)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var byName = output.decks.GroupBy(d => d.name.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() == 1)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                while (rows.Read())
                {
                    var result = rows.GetString(0);
                    var arenaId = rows.GetString(1);
                    var deckName = rows.GetString(2).Trim();
                    Deck? match = null;
                    if (arenaId.Length > 0) byId.TryGetValue(arenaId, out match);
                    if (match is null && deckName.Length > 0) byName.TryGetValue(deckName, out match);
                    if (match is null) continue; // Never assign ambiguous/unidentified matches.
                    if (result == "win") match.wins++;
                    else match.losses++;
                    output.recentMatch ??= new Recent { result = result, deckName = match.name };
                }
            }
              }
            catch (Exception ex) when (ex is SqliteException or IOException or InvalidOperationException)
            {
                WriteDiagnostic("Match history unavailable: " + ex.Message);
            }
            await WriteSnapshotAsync(output);
            // Resolve previously cached artwork immediately; download only two missing
            // images per cycle so records and deck selection remain responsive.
            int uncached = 0;
            foreach (var deck in output.decks)
            {
                var file = CachedArtPath(deck);
                if (file.Length == 0) continue;
                if (!File.Exists(file) && !File.Exists(Path.ChangeExtension(file, ".jpg")) && uncached++ < 2)
                    await CacheArtAsync(deck, file);
                if (File.Exists(file)) deck.imagePath = file;
                else if (File.Exists(Path.ChangeExtension(file, ".jpg")))
                    deck.imagePath = Path.ChangeExtension(file, ".jpg");
            }
            await WriteSnapshotAsync(output);
        }
        catch (Exception ex) when (ex is IOException or SqliteException or JsonException or HttpRequestException
            or InvalidOperationException or UnauthorizedAccessException or TaskCanceledException)
        {
            WriteDiagnostic("Stream Deck snapshot: " + ex);
        }
        finally { busy = false; }
    }

    private async Task WriteSnapshotAsync(Output output)
    {
        Directory.CreateDirectory(outputFolder);
        var target = Path.Combine(outputFolder, "streamdeck.json");
        var temp = Path.Combine(outputFolder, "streamdeck." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(output));
            File.Move(temp, target, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private void WriteDiagnostic(string message)
    {
        Debug.WriteLine(message);
        try
        {
            var folder = Path.Combine(outputFolder, "Diagnostics");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "streamdeck-export.log"),
                DateTimeOffset.Now.ToString("O") + " " + message + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private string CachedArtPath(Deck deck)
    {
        if (string.IsNullOrWhiteSpace(deck.artUrl) ||
            !Uri.TryCreate(deck.artUrl, UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps ||
            !(url.Host.Equals("api.scryfall.com", StringComparison.OrdinalIgnoreCase)
              || url.Host.Equals("cards.scryfall.io", StringComparison.OrdinalIgnoreCase)))
            return "";
        var folder = Path.Combine(outputFolder, "StreamDeckArtwork");
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(deck.artUrl)));
        return Path.Combine(folder, hash + ".png");
    }

    private async Task CacheArtAsync(Deck deck, string file)
    {
        try
        {
            using var response = await artworkClient.GetAsync(deck.artUrl);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentType?.MediaType?.StartsWith("image/") != true ||
                response.Content.Headers.ContentLength > 2_000_000) return;
            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length > 2_000_000 || bytes.Length < 8) return;
            // Scryfall artwork is JPEG or PNG. Preserve the real MIME format
            // by writing a PNG only when it actually contains PNG bytes.
            var png = bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71;
            var jpg = bytes[0] == 255 && bytes[1] == 216;
            if (!png && !jpg) return;
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var actualFile = png ? file : Path.ChangeExtension(file, ".jpg");
            await File.WriteAllBytesAsync(actualFile, bytes);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        { Debug.WriteLine("Stream Deck artwork: " + ex.Message); }
    }
    public void Dispose()
    {
        disposed = true;
        timer.Stop();
        timer.Dispose();
        artworkClient.Dispose();
    }
}
