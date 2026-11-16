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

    public StreamDeckBridge(WebView2 browser)
    {
        this.browser = browser;
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
            if (input?.error is not null || input?.decks is null) return;
            var output = new Output
            {
                decks = input.decks.Take(1000).ToList(),
                activeDeckId = input.activeDeckId,
                collection = input.collection,
                sync = input.sync
            };
            var matchDb = Path.Combine(outputFolder, "match-history.sqlite3");
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
            foreach (var deck in output.decks.Take(60)) // artwork is lazy and capped
                deck.imagePath = await CacheArtAsync(deck);
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
        catch (Exception ex) when (ex is IOException or SqliteException or JsonException or HttpRequestException
            or InvalidOperationException or UnauthorizedAccessException or TaskCanceledException)
        {
            Debug.WriteLine("Stream Deck snapshot: " + ex.Message);
        }
        finally { busy = false; }
    }

    private async Task<string> CacheArtAsync(Deck deck)
    {
        if (string.IsNullOrWhiteSpace(deck.artUrl) ||
            !Uri.TryCreate(deck.artUrl, UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps ||
            !(url.Host.Equals("api.scryfall.com", StringComparison.OrdinalIgnoreCase)
              || url.Host.EndsWith(".scryfall.com", StringComparison.OrdinalIgnoreCase)))
            return "";
        var folder = Path.Combine(outputFolder, "StreamDeckArtwork");
        var file = Path.Combine(folder, Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(deck.artUrl))) + ".jpg");
        if (File.Exists(file)) return file;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await artworkClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentType?.MediaType?.StartsWith("image/") != true ||
                response.Content.Headers.ContentLength > 2_000_000) return "";
            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length > 2_000_000 || bytes.Length == 0) return "";
            Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(file, bytes);
            return file;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException) { return ""; }
    }
    public void Dispose()
    {
        disposed = true;
        timer.Stop();
        timer.Dispose();
        artworkClient.Dispose();
    }
}
