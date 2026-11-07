using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ArcaneVault.Desktop;

/// <summary>
/// Serves the embedded Arcane Vault interface and proxies authorized localhost card metadata
/// batches to Scryfall. No Python runtime or external API keys are required.
/// </summary>
internal sealed class LocalAppServer : IDisposable
{
    public const string BaseUrl = "http://127.0.0.1:8765/";
    private const int MaximumRequestBytes = 25_000;
    private static readonly Uri ScryfallEndpoint = new("https://api.scryfall.com/cards/collection");
    private readonly HttpListener listener = new();
    private readonly HttpClient upstream;
    private readonly ArtworkCache artwork;
    private readonly UpdateManager updater = new();
    public event Action? UpdateRestartRequested;
    private readonly ArenaLogConnector arena = new();
    private readonly ScryfallCatalog catalog;
    private readonly Dictionary<int,string> arenaCardNames = new();
    private readonly SemaphoreSlim arenaNameGate = new(1,1);
    private readonly ArenaMatchTracker tracker = new();
    private readonly PeriodicTimer matchTimer = new(TimeSpan.FromSeconds(4));
    private Task? matchLoop;
    private readonly CancellationTokenSource stopped = new();
    private readonly SemaphoreSlim upstreamGate = new(1, 1);
    private DateTimeOffset lastScryfallRequest = DateTimeOffset.MinValue;
    private readonly byte[] pageBytes;
    private Task? loop;

    public LocalAppServer()
    {
        using var resource = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("ArcaneVault.Desktop.App.html")
            ?? throw new InvalidOperationException("The embedded Arcane Vault interface is missing.");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        pageBytes = buffer.ToArray();

        upstream = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
        upstream.DefaultRequestHeaders.UserAgent.ParseAdd("ArcaneVault/4.7 (local collection manager)");
        upstream.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        catalog = new ScryfallCatalog(upstream);
        artwork = new ArtworkCache(upstream);
        listener.Prefixes.Add(BaseUrl);
    }

    public void Start()
    {
        try { listener.Start(); }
        catch (HttpListenerException ex)
        {
            throw new InvalidOperationException("Port 8765 is not available. Close the previous Arcane Vault server before running the Visual Studio app.", ex);
        }
        loop = Task.Run(LoopAsync);
        matchLoop = Task.Run(async () =>
        {
            try { while (await matchTimer.WaitForNextTickAsync(stopped.Token)) tracker.Tick(); }
            catch (OperationCanceledException) { }
        }, stopped.Token);
    }

    private async Task LoopAsync()
    {
        while (listener.IsListening && !stopped.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await listener.GetContextAsync().WaitAsync(stopped.Token); }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
            _ = Task.Run(() => HandleAsync(ctx), stopped.Token);
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            if (ctx.Request.HttpMethod == "GET")
            {
                if (path is "/" or "/index.html" or "/Arcane_Vault_MTGA_Collection_v4_2.html")
                {
                    await SendAsync(ctx.Response, 200, "text/html; charset=utf-8", pageBytes);
                    return;
                }
                if (path == "/api/app/version")
                {
                    if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response, 403, new { error = "Local only" }); return; }
                    var version = typeof(LocalAppServer).Assembly.GetName().Version ?? new Version(0, 0, 0);
                    await JsonAsync(ctx.Response, 200, new { version = $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}" }); return;
                }
                if (path == "/api/app/update/status")
                {
                    if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response, 403, new { error = "Local only" }); return; }
                    await JsonAsync(ctx.Response, 200, updater.Status()); return;
                }
                if (path == "/api/art/status") { await JsonAsync(ctx.Response, 200, artwork.Status()); return; }
                if (path == "/api/art")
                {
                    if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response, 403, new { error = "Local only" }); return; }
                    try { var (bytes, type) = await artwork.ReadAsync(ctx.Request.QueryString["url"] ?? "", stopped.Token); await SendAsync(ctx.Response, 200, type, bytes); }
                    catch (Exception ex) when (ex is ArgumentException or HttpRequestException or IOException)
                    { await JsonAsync(ctx.Response, 502, new { error = "Artwork unavailable: " + ex.Message }); }
                    return;
                }
                if (path == "/api/catalog/status")
                {
                    await JsonAsync(ctx.Response, 200, catalog.Status());
                    return;
                }
                if (path == "/api/catalog/search")
                {
                    if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response, 403, new { error = "Local access only" }); return; }
                    string Param(string key) => ctx.Request.QueryString[key] ?? "";
                    int.TryParse(Param("page"), out int pg); int.TryParse(Param("size"), out int sz);
                    await JsonAsync(ctx.Response, 200, catalog.Search(Param("scope"), Param("q"), Param("type"), Param("rarity"), Param("colors"), Param("role"), Param("sort"), pg, sz == 0 ? 36 : sz, Param("arena")));
                    return;
                }
                if (path == "/api/catalog/set-release-dates")
                {
                    if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response, 403, new { error = "Local access only" }); return; }
                    await JsonAsync(ctx.Response, 200, catalog.SetReleaseDates());
                    return;
                }
                if (path == "/api/catalog/sets")
                {
                    if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response,403,new {error="Local access only"});return; }
                    await JsonAsync(ctx.Response,200,catalog.SetProgress(ctx.Request.QueryString["arena"]??"any",ctx.Request.QueryString["q"]??""));return;
                }
                if (path == "/api/catalog/set-cards")
                {
                    if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response,403,new {error="Local access only"});return; }
                    int.TryParse(ctx.Request.QueryString["page"],out int pg);int.TryParse(ctx.Request.QueryString["size"],out int sz);
                    try { await JsonAsync(ctx.Response,200,catalog.SetCards(ctx.Request.QueryString["set"]??"",ctx.Request.QueryString["arena"]??"any",ctx.Request.QueryString["ownership"]??"all",pg,sz==0?40:sz)); }
                    catch(ArgumentException ex) { await JsonAsync(ctx.Response,400,new {error=ex.Message}); }return;
                }
                if (path == "/api/catalog/variants")
                {
                    if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response, 403, new { error = "Local access only" }); return; }
                    try { await JsonAsync(ctx.Response, 200, catalog.Variants(ctx.Request.QueryString["name"] ?? "", ctx.Request.QueryString["scope"] ?? "all")); }
                    catch (ArgumentException ex) { await JsonAsync(ctx.Response, 400, new { error = ex.Message }); }
                    return;
                }
                if (path == "/api/combos")
                {
                    await ProxyCombosAsync(ctx);
                    return;
                }
                if (path == "/api/matches")
                {
                    await JsonAsync(ctx.Response, 200, tracker.Snapshot());
                    return;
                }
                if (path == "/api/arena/resolve")
                {
                    if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response,403,new {error="Local only"});return; }
                    var raw = ctx.Request.QueryString["ids"] ?? "";
                    var tokens = raw.Split(',',StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if(tokens.Length is < 1 or > 25 || tokens.Any(t=>!int.TryParse(t,out var n)||n<=0 || n>int.MaxValue))
                    { await JsonAsync(ctx.Response,400,new {error="Specify 1–25 positive Arena card IDs."});return; }
                    var resolved = new Dictionary<int,string>();
                    var unresolved = new List<int>();
                    await arenaNameGate.WaitAsync(stopped.Token);
                    try
                    {
                        foreach(var token in tokens.Distinct())
                        {
                            int id = int.Parse(token,System.Globalization.CultureInfo.InvariantCulture);
                            if(arenaCardNames.TryGetValue(id,out var cached)){resolved[id]=cached;continue;}
                            try
                            {
                                using var response=await upstream.GetAsync("https://api.scryfall.com/cards/arena/"+id,stopped.Token);
                                if(!response.IsSuccessStatusCode){unresolved.Add(id);continue;}
                                using var card=JsonDocument.Parse(await response.Content.ReadAsStringAsync(stopped.Token));
                                if(card.RootElement.TryGetProperty("name",out var name) && name.ValueKind==JsonValueKind.String)
                                {
                                    var value=name.GetString() ?? "";
                                    if(value.Length is > 0 and <=200){arenaCardNames[id]=value;resolved[id]=value;}
                                    else unresolved.Add(id);
                                }
                                else unresolved.Add(id);
                                await Task.Delay(110,stopped.Token);
                            }
                            catch(HttpRequestException){unresolved.Add(id);}
                            catch(TaskCanceledException) when (!stopped.IsCancellationRequested){unresolved.Add(id);}
                            catch(JsonException){unresolved.Add(id);}
                        }
                    }
                    finally{arenaNameGate.Release();}
                    await JsonAsync(ctx.Response,200,new {resolved,unresolved});return;
                }
                if (path == "/api/arena/status")
                {
                    await JsonAsync(ctx.Response, 200, arena.Snapshot());
                    return;
                }
                if (path == "/health")
                {
                    await JsonAsync(ctx.Response, 200, new { ok = true, service = "Arcane Vault", version = "4.7-vs" });
                    return;
                }
            }
            else if (ctx.Request.HttpMethod == "POST" && path == "/api/app/update/prepare")
            {
                if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response, 403, new { error = "Local only" }); return; }
                if (ctx.Request.ContentLength64 is < 2 or > 256) { await JsonAsync(ctx.Response, 413, new { error = "Invalid update request." }); return; }
                try
                {
                    using var body = await JsonDocument.ParseAsync(ctx.Request.InputStream);
                    var version = body.RootElement.GetProperty("version").GetString() ?? "";
                    await JsonAsync(ctx.Response, 200, await updater.PrepareAsync(version, stopped.Token));
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or ArgumentException or InvalidOperationException or IOException or InvalidDataException)
                { await JsonAsync(ctx.Response, 400, new { error = ex.Message }); }
                return;
            }
            else if (ctx.Request.HttpMethod == "POST" && path == "/api/app/update/install")
            {
                if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response, 403, new { error = "Local only" }); return; }
                try
                {
                    updater.LaunchAndRestart();
                    await JsonAsync(ctx.Response, 202, new { restarting = true });
                    _ = Task.Run(async () => { await Task.Delay(450); UpdateRestartRequested?.Invoke(); });
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                { await JsonAsync(ctx.Response, 400, new { error = ex.Message }); }
                return;
            }
            else if (ctx.Request.HttpMethod == "POST" && path == "/api/art/clear")
            {
                if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response, 403, new { error = "Local only" }); return; }
                artwork.Clear(); await JsonAsync(ctx.Response, 200, artwork.Status()); return;
            }
            else if (ctx.Request.HttpMethod == "POST" && path == "/api/catalog/sync")
            {
                if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response, 403, new { error = "Local application only" }); return; }
                bool started = catalog.Start();
                await JsonAsync(ctx.Response, started ? 202 : 409, new { started, status = catalog.Status() });
                return;
            }
            else if (ctx.Request.HttpMethod == "POST" && path is "/api/catalog/sets/physical" or "/api/catalog/set-cards/physical")
            {
                if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response, 403, new { error = "Local application only" }); return; }
                if (ctx.Request.ContentLength64 is < 2 or > 3_000_000) { await JsonAsync(ctx.Response, 413, new { error = "Invalid physical ownership payload size" }); return; }
                try
                {
                    using var body = await JsonDocument.ParseAsync(ctx.Request.InputStream);
                    var inventory = body.RootElement.GetProperty("cards");
                    string Param(string key) => ctx.Request.QueryString[key] ?? "";
                    if (path == "/api/catalog/sets/physical")
                        await JsonAsync(ctx.Response, 200, catalog.SetProgress(Param("arena"), Param("q"), inventory));
                    else
                    {
                        int.TryParse(Param("page"), out int pg); int.TryParse(Param("size"), out int sz);
                        await JsonAsync(ctx.Response, 200, catalog.SetCards(Param("set"), Param("arena"), Param("ownership"), pg, sz == 0 ? 40 : sz, inventory));
                    }
                }
                catch (Exception ex) when (ex is JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException)
                { await JsonAsync(ctx.Response, 400, new { error = ex.Message }); }
                return;
            }
            else if (ctx.Request.HttpMethod == "POST" && path == "/api/catalog/ownership")
            {
                if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response, 403, new { error = "Local application only" }); return; }
                if (ctx.Request.ContentLength64 is < 2 or > 1_500_000) { await JsonAsync(ctx.Response, 413, new { error = "Invalid ownership payload size" }); return; }
                try
                {
                    using var json = await JsonDocument.ParseAsync(ctx.Request.InputStream);
                    catalog.UpdateOwnership(json.RootElement.GetProperty("cards"));
                    await JsonAsync(ctx.Response, 200, new { ok = true });
                }
                catch (Exception ex) when (ex is JsonException or ArgumentException or KeyNotFoundException)
                { await JsonAsync(ctx.Response, 400, new { error = ex.Message }); }
                return;
            }
            else if (ctx.Request.HttpMethod == "POST" && path == "/api/arena/config")
            {
                if (!AllowedLocalRequest(ctx.Request))
                {
                    await JsonAsync(ctx.Response, 403, new { error = "Local app requests only" });
                    return;
                }
                if (ctx.Request.ContentLength64 is < 2 or > 4096)
                {
                    await JsonAsync(ctx.Response, 413, new { error = "Invalid configuration size" });
                    return;
                }
                try
                {
                    using var body = await JsonDocument.ParseAsync(ctx.Request.InputStream);
                    bool enabled = body.RootElement.GetProperty("enabled").GetBoolean();
                    var pathValue = body.RootElement.TryGetProperty("path", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                    arena.Configure(enabled, pathValue);
                    tracker.Configure(enabled, pathValue);
                    if (enabled) tracker.Tick();
                    await JsonAsync(ctx.Response, 200, arena.Snapshot());
                }
                catch (Exception ex) when (ex is JsonException or ArgumentException or KeyNotFoundException)
                {
                    await JsonAsync(ctx.Response, 400, new { error = ex.Message });
                }
                return;
            }
            else if (ctx.Request.HttpMethod == "POST" && path == "/api/matches/active-deck")
            {
                if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response, 403, new { error = "Local application only" }); return; }
                if (ctx.Request.ContentLength64 is < 2 or > 4096) { await JsonAsync(ctx.Response, 413, new { error = "Invalid request size" }); return; }
                try
                {
                    using var body = await JsonDocument.ParseAsync(ctx.Request.InputStream);
                    var root = body.RootElement;
                    var name = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                    var version = root.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    tracker.SetCurrentDeck(name, version);
                    await JsonAsync(ctx.Response, 200, new { updated = true });
                }
                catch (Exception ex) when (ex is JsonException or ArgumentException)
                { await JsonAsync(ctx.Response, 400, new { error = ex.Message }); }
                return;
            }
            else if (ctx.Request.HttpMethod == "POST" && path == "/api/matches/correct")
            {
                if (!AllowedLocalRequest(ctx.Request)) { await JsonAsync(ctx.Response, 403, new { error = "Local application only" }); return; }
                if (ctx.Request.ContentLength64 is < 2 or > 4096) { await JsonAsync(ctx.Response, 413, new { error = "Invalid update size" }); return; }
                try
                {
                    using var body = await JsonDocument.ParseAsync(ctx.Request.InputStream);
                    var root = body.RootElement;
                    string id = root.GetProperty("id").GetString() ?? "";
                    if (id.Length is < 1 or > 180) throw new ArgumentException("Invalid match identifier");
                    string? Optional(string key) => root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    bool updated = tracker.Correct(id, Optional("result"), Optional("deck"), Optional("version"), Optional("note"));
                    await JsonAsync(ctx.Response, updated ? 200 : 404, new { updated });
                }
                catch (Exception ex) when (ex is JsonException or ArgumentException or KeyNotFoundException)
                { await JsonAsync(ctx.Response, 400, new { error = ex.Message }); }
                return;
            }
            else if (ctx.Request.HttpMethod == "POST" && path == "/api/scryfall/collection")
            {
                await ProxyCollectionAsync(ctx);
                return;
            }
            await JsonAsync(ctx.Response, 404, new { error = "Unknown endpoint" });
        }
        catch (Exception ex)
        {
            try { await JsonAsync(ctx.Response, 500, new { error = "Local service error: " + ex.Message }); }
            catch { /* connection closed */ }
        }
    }

    private static bool AllowedLocalRequest(HttpListenerRequest request)
    {
        var host = request.Headers["Host"] ?? "";
        var origin = request.Headers["Origin"];
        return string.Equals(host, "127.0.0.1:8765", StringComparison.OrdinalIgnoreCase) &&
            (origin is null || string.Equals(origin, "http://127.0.0.1:8765", StringComparison.OrdinalIgnoreCase));
    }

    private async Task ProxyCombosAsync(HttpListenerContext ctx)
    {
        if (!AllowedLocalRequest(ctx.Request))
        { await JsonAsync(ctx.Response, 403, new { error = "Local app requests only" }); return; }
        string? name = ctx.Request.QueryString["name"];
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || name.Any(char.IsControl))
        { await JsonAsync(ctx.Response, 400, new { error = "Supply a valid card name." }); return; }
        // Query by exact card name; no arbitrary URL or external request target is accepted.
        string query = "card:\"" + name.Replace("\"", "") + "\"";
        var url = "https://backend.commanderspellbook.com/variants/?limit=12&q=" + Uri.EscapeDataString(query);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await upstream.SendAsync(request, stopped.Token);
            string json = await response.Content.ReadAsStringAsync(stopped.Token);
            if (!response.IsSuccessStatusCode)
            { await JsonAsync(ctx.Response, 502, new { error = "Commander Spellbook returned HTTP " + (int)response.StatusCode }); return; }
            if (json.Length > 3_000_000)
            { await JsonAsync(ctx.Response, 502, new { error = "Combo response too large" }); return; }
            await SendAsync(ctx.Response, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { await JsonAsync(ctx.Response, 502, new { error = "Cannot reach Commander Spellbook; check your network connection." }); }
    }

    private async Task ProxyCollectionAsync(HttpListenerContext ctx)
    {
        if (!AllowedLocalRequest(ctx.Request))
        {
            await JsonAsync(ctx.Response, 403, new { error = "Requests must originate from the local Arcane Vault app." });
            return;
        }
        var length = ctx.Request.ContentLength64;
        if (length < 2 || length > MaximumRequestBytes)
        {
            await JsonAsync(ctx.Response, 413, new { error = "Batch size exceeds the supported limit." });
            return;
        }
        var payload = new byte[(int)length];
        await ctx.Request.InputStream.ReadExactlyAsync(payload.AsMemory(), stopped.Token);
        // Scryfall supports a name, a stable printing UUID, or exact set/collector-number lookup.
        // Only send allowlisted identifier fields upstream; never relay arbitrary URLs.
        var identifiers = new List<Dictionary<string, string>>();
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var items = doc.RootElement.GetProperty("identifiers");
            if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() is < 1 or > 75)
                throw new JsonException();
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) throw new JsonException();
                string Get(string property) => item.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String ? val.GetString()?.Trim() ?? "" : "";
                var id = Get("id");
                var set = Get("set");
                var number = Get("collector_number");
                var name = Get("name");
                if (Guid.TryParse(id, out var uuid))
                    identifiers.Add(new() { ["id"] = uuid.ToString() });
                else if (set.Length is >= 2 and <= 8 && System.Text.RegularExpressions.Regex.IsMatch(set, "^[a-zA-Z0-9]+$") &&
                         number.Length is >= 1 and <= 30 && System.Text.RegularExpressions.Regex.IsMatch(number, "^[a-zA-Z0-9★*+-]+$"))
                    identifiers.Add(new() { ["set"] = set.ToLowerInvariant(), ["collector_number"] = number });
                else if (name.Length is > 0 and <= 200)
                    identifiers.Add(new() { ["name"] = name });
                else throw new JsonException();
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            await JsonAsync(ctx.Response, 400, new { error = "Expected 1–75 identifiers with names, UUIDs, or set and collector number." });
            return;
        }

        var requestBody = JsonSerializer.Serialize(new { identifiers });
        await upstreamGate.WaitAsync(stopped.Token);
        try
        {
            // Scryfall asks consumers to keep request frequency modest (approximately 10/sec maximum).
            var remaining = TimeSpan.FromMilliseconds(125) - (DateTimeOffset.UtcNow - lastScryfallRequest);
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, stopped.Token);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                lastScryfallRequest = DateTimeOffset.UtcNow;
                using var message = new HttpRequestMessage(HttpMethod.Post, ScryfallEndpoint)
                {
                    Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
                };
                try
                {
                    using var response = await upstream.SendAsync(message, stopped.Token);
                    var responseText = await response.Content.ReadAsStringAsync(stopped.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        await SendAsync(ctx.Response, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(responseText));
                        return;
                    }
                    if ((response.StatusCode == (HttpStatusCode)429 || response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable) && attempt < 2)
                    {
                        var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2 * (attempt + 1));
                        await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(wait.TotalSeconds, 1, 12)), stopped.Token);
                        continue;
                    }
                    await JsonAsync(ctx.Response, 502, new { error = $"Scryfall returned {(int)response.StatusCode}: {responseText[..Math.Min(responseText.Length, 350)]}" });
                    return;
                }
                catch (HttpRequestException ex)
                {
                    if (attempt < 2) { await Task.Delay(1000 * (attempt + 1), stopped.Token); continue; }
                    await JsonAsync(ctx.Response, 502, new { error = "Cannot reach Scryfall. Check connectivity or firewall: " + ex.Message });
                    return;
                }
                catch (TaskCanceledException) when (!stopped.IsCancellationRequested)
                {
                    if (attempt < 2) continue;
                    await JsonAsync(ctx.Response, 504, new { error = "Scryfall connection timed out." });
                    return;
                }
            }
        }
        finally { upstreamGate.Release(); }
    }

    private static Task JsonAsync(HttpListenerResponse response, int statusCode, object result) =>
        SendAsync(response, statusCode, "application/json; charset=utf-8", JsonSerializer.SerializeToUtf8Bytes(result));

    private static async Task SendAsync(HttpListenerResponse response, int statusCode, string contentType, byte[] bytes)
    {
        response.StatusCode = statusCode;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        response.Headers["Cache-Control"] = "no-store";
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    public void Dispose()
    {
        stopped.Cancel();
        listener.Close();
        upstream.Dispose();
        matchTimer.Dispose();
        tracker.Dispose();
        upstreamGate.Dispose();
        stopped.Dispose();
    }
}
