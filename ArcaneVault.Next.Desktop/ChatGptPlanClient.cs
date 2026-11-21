using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// ChatGPT OAuth client with Windows user-protected renewable session storage.
/// </summary>
internal sealed class ChatGptPlanClient
{
    private const string AuthBase = "https://auth.openai.com";
    private const string Resource = "https://api.openai.com/v1";
    private const string RequiredScope = "chatgpt.tokens.use.direct";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };
    private string? accessToken;
    private DateTimeOffset expiresAt;
    private string? subject;
    private string? issuedClientId;
    private string? hostId;
    private string? refreshToken;
    private string? retainedIdToken;
    private readonly SemaphoreSlim refreshGate = new(1, 1);

    public bool IsConnected => refreshToken is not null ||
        (accessToken is not null && expiresAt > DateTimeOffset.UtcNow.AddSeconds(30));
    public string Status => refreshToken is not null
        ? "Connected to ChatGPT — signed in across restarts"
        : IsConnected ? "Connected for this session"
        : "Not connected — use Options to connect";


    private static string CredentialPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ArcaneVaultNext", "AI", "chatgpt-session.dpapi");

    private sealed record SavedSession(string ClientId, string Subject,
        string Access, string Refresh, string IdToken, DateTimeOffset Expires, string HostId);

    public ChatGptPlanClient() => LoadSavedSession();

    private void LoadSavedSession()
    {
        try
        {
            if (!File.Exists(CredentialPath)) return;
            byte[] encrypted = File.ReadAllBytes(CredentialPath);
            byte[] clear = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            try
            {
                var session = JsonSerializer.Deserialize<SavedSession>(clear);
                if (session is null || string.IsNullOrWhiteSpace(session.ClientId) ||
                    string.IsNullOrWhiteSpace(session.Refresh) ||
                    string.IsNullOrWhiteSpace(session.Subject)) return;
                issuedClientId = session.ClientId;
                subject = session.Subject;
                accessToken = session.Access;
                refreshToken = session.Refresh;
                retainedIdToken = session.IdToken;
                expiresAt = session.Expires;
                hostId = session.HostId;
            }
            finally { CryptographicOperations.ZeroMemory(clear); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            CryptographicException or JsonException)
        {
            accessToken = null; refreshToken = null; subject = null;
            retainedIdToken = null; issuedClientId = null;
            expiresAt = default;
        }
    }

    private void SaveSession()
    {
        if (accessToken is null || refreshToken is null || retainedIdToken is null ||
            subject is null || issuedClientId is null || hostId is null)
            throw new InvalidOperationException("OAuth session is incomplete.");
        var record = new SavedSession(issuedClientId, subject, accessToken,
            refreshToken, retainedIdToken, expiresAt, hostId);
        byte[] clear = JsonSerializer.SerializeToUtf8Bytes(record);
        byte[] encrypted;
        try { encrypted = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(clear); }
        Directory.CreateDirectory(Path.GetDirectoryName(CredentialPath)!);
        var temporary = CredentialPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, encrypted);
            File.Move(temporary, CredentialPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private void ClearSession()
    {
        accessToken = null;
        refreshToken = null;
        retainedIdToken = null;
        subject = null;
        expiresAt = default;
        if (File.Exists(CredentialPath)) File.Delete(CredentialPath);
    }

    public async Task EnsureConnectedAsync(CancellationToken ct = default)
    {
        if (accessToken is not null && expiresAt > DateTimeOffset.UtcNow.AddMinutes(2)) return;
        if (refreshToken is null || issuedClientId is null)
            throw new InvalidOperationException("Connect ChatGPT in Options before reviewing.");
        await refreshGate.WaitAsync(ct);
        try
        {
            if (accessToken is not null && expiresAt > DateTimeOffset.UtcNow.AddMinutes(2)) return;
            using var response = await Http.PostAsync(AuthBase + "/api/accounts/oauth/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = issuedClientId,
                    ["refresh_token"] = refreshToken,
                    ["resource"] = Resource
                }), ct);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode is 400 or 401 or 403)
                {
                    ClearSession();
                    throw new InvalidOperationException("ChatGPT authorization expired or was revoked. Reconnect in Options.");
                }
                throw new HttpRequestException("ChatGPT renewal unavailable (HTTP " +
                    (int)response.StatusCode + ").");
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;
            string newAccess = root.GetProperty("access_token").GetString() ??
                throw new InvalidOperationException("Missing access token in renewal.");
            string newRefresh = root.GetProperty("refresh_token").GetString() ??
                throw new InvalidOperationException("Missing rotated refresh token.");
            int expires = root.GetProperty("expires_in").GetInt32();
            if (expires < 60) throw new InvalidOperationException("Invalid renewed token lifetime.");
            if (root.TryGetProperty("scope", out var scopes) &&
                !(scopes.GetString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Contains(RequiredScope, StringComparer.Ordinal))
            {
                ClearSession();
                throw new InvalidOperationException("ChatGPT plan usage permission was withdrawn.");
            }
            accessToken = newAccess;
            refreshToken = newRefresh;
            expiresAt = DateTimeOffset.UtcNow.AddSeconds(expires);
            // Retain the original verified ID token as a hint, never as proof
            // of the newly refreshed grant.
            SaveSession();
        }
        finally { refreshGate.Release(); }
    }

    public async Task<bool> DisconnectAsync(CancellationToken ct = default)
    {
        await refreshGate.WaitAsync(ct);
        try
        {
            bool revoked = refreshToken is null;
            if (refreshToken is not null && issuedClientId is not null)
            {
                try
                {
                    using var discovery = await Http.GetAsync(
                        AuthBase + "/.well-known/openid-configuration", ct);
                    discovery.EnsureSuccessStatusCode();
                    using var json = JsonDocument.Parse(
                        await discovery.Content.ReadAsStringAsync(ct));
                    var endpoint = json.RootElement.GetProperty("revocation_endpoint").GetString();
                    if (endpoint is null || !endpoint.StartsWith(AuthBase + "/", StringComparison.Ordinal))
                        throw new InvalidOperationException("Unexpected revocation endpoint.");
                    using var response = await Http.PostAsync(endpoint,
                        new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            ["token"] = refreshToken,
                            ["token_type_hint"] = "refresh_token",
                            ["client_id"] = issuedClientId
                        }), ct);
                    revoked = response.IsSuccessStatusCode;
                }
                catch (Exception ex) when (ex is HttpRequestException or
                    TaskCanceledException or InvalidOperationException or JsonException)
                { revoked = false; }
            }
            ClearSession();
            return revoked;
        }
        finally { refreshGate.Release(); }
    }

    private static string UrlSafe(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromUrlSafe(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight((s.Length + 3) / 4 * 4, '='));
    }

    private static string RandomUrl(int bytes = 32) => UrlSafe(RandomNumberGenerator.GetBytes(bytes));

    private static Dictionary<string, string> Query(string? q) =>
        (q ?? "").TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .ToDictionary(a => Uri.UnescapeDataString(a[0].Replace("+", " ")),
                      a => a.Length == 2 ? Uri.UnescapeDataString(a[1].Replace("+", " ")) : "",
                      StringComparer.Ordinal);

    private static string BuildUrl(string root, IEnumerable<KeyValuePair<string, string>> p) =>
        root + "?" + string.Join("&", p.Select(x =>
            Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));

    private static int PickLoopbackPort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    private static string StableHostId()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArcaneVaultNext", "AI");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "host-id.txt");
        if (File.Exists(path))
        {
            var saved = File.ReadAllText(path).Trim();
            if (Guid.TryParse(saved.Replace("urn:uuid:", ""), out _)) return saved;
        }
        var created = "urn:uuid:" + Guid.NewGuid().ToString("D");
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, created);
        File.Move(temp, path, true);
        return created;
    }

    public async Task SignInAsync(CancellationToken cancellationToken = default)
    {
        var state = RandomUrl();
        var nonce = RandomUrl();
        var verifier = RandomUrl(64);
        var challenge = UrlSafe(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        hostId ??= StableHostId();
        int port = PickLoopbackPort();
        var redirect = $"http://127.0.0.1:{port}/auth/callback";
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var clientId = issuedClientId ?? "dynamic_agent_client";
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["ext_agent_host_id"] = hostId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirect,
            ["scope"] = "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct",
            ["resource"] = Resource,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = challenge
        };
        if (issuedClientId is null) parameters["agent_name_hint"] = "Arcane Vault";
        else if (retainedIdToken is not null) parameters["id_token_hint"] = retainedIdToken;
        var authorize = BuildUrl(AuthBase + "/api/accounts/authorize", parameters);
        Process.Start(new ProcessStartInfo(authorize) { UseShellExecute = true });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        HttpListenerContext callback;
        callback = await listener.GetContextAsync().WaitAsync(timeout.Token);
        // Validate the full callback path and unpredictable state before exchange.
        var args = Query(callback.Request.Url?.Query);
        bool validPath = callback.Request.Url?.AbsolutePath == "/auth/callback";
        bool validState = args.TryGetValue("state", out var returnedState) &&
            CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(state)),
                SHA256.HashData(Encoding.UTF8.GetBytes(returnedState)));
        var html = validPath && validState && args.ContainsKey("code")
            ? "<h2>Authorization received.</h2><p>Return to Arcane Vault to finish verification.</p>"
            : "<h2>Authorization not accepted.</h2><p>Return to Arcane Vault.</p>";
        var bytes = Encoding.UTF8.GetBytes("<!doctype html><meta charset=\"utf-8\">" + html);
        callback.Response.ContentType = "text/html; charset=utf-8";
        callback.Response.ContentLength64 = bytes.Length;
        await callback.Response.OutputStream.WriteAsync(bytes, cancellationToken);
        callback.Response.Close();
        listener.Stop();
        if (!validPath || !validState) throw new InvalidOperationException("OAuth callback state or path mismatch.");
        if (args.TryGetValue("error", out var oauthError)) throw new InvalidOperationException("Sign-in declined: " + oauthError);
        if (!args.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Missing authorization code.");
        if (issuedClientId is null)
        {
            if (!args.TryGetValue("client_id", out clientId) || string.IsNullOrWhiteSpace(clientId) ||
                clientId == "dynamic_agent_client")
                throw new InvalidOperationException("OpenAI did not issue a client registration.");
        }
        else if (args.TryGetValue("client_id", out var callbackClient) && callbackClient != issuedClientId)
            throw new InvalidOperationException("Registered OAuth client mismatch.");

        using var exchange = await Http.PostAsync(AuthBase + "/api/accounts/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["code_verifier"] = verifier,
                ["redirect_uri"] = redirect,
                ["resource"] = Resource
            }), cancellationToken);
        if (!exchange.IsSuccessStatusCode)
            throw new InvalidOperationException("OpenAI authorization exchange failed (HTTP " +
                (int)exchange.StatusCode + ").");
        using var token = JsonDocument.Parse(await exchange.Content.ReadAsStringAsync(cancellationToken));
        var root = token.RootElement;
        string idToken = root.GetProperty("id_token").GetString() ?? throw new InvalidOperationException("No ID token.");
        string newSubject = await VerifyIdTokenAsync(idToken, clientId, nonce, cancellationToken);
        if (subject is not null && newSubject != subject)
            throw new InvalidOperationException("Signed-in account does not match the prior session.");
        string scopes = root.GetProperty("scope").GetString() ?? "";
        if (!scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(RequiredScope, StringComparer.Ordinal))
            throw new InvalidOperationException("ChatGPT account connected without plan usage permission.");
        string newAccessToken = root.GetProperty("access_token").GetString() ??
            throw new InvalidOperationException("No access token returned.");
        int expiresIn = root.GetProperty("expires_in").GetInt32();
        if (expiresIn < 60) throw new InvalidOperationException("Token lifetime too short.");

        // Set credentials only after every security check passes.
        accessToken = newAccessToken;
        subject = newSubject;
        issuedClientId = clientId;
        expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        retainedIdToken = idToken;
        refreshToken = root.GetProperty("refresh_token").GetString() ??
            throw new InvalidOperationException("Renewable access was not granted.");
        SaveSession();
    }

    private static async Task<string> VerifyIdTokenAsync(string jwt, string clientId,
        string expectedNonce, CancellationToken ct)
    {
        var segments = jwt.Split('.');
        if (segments.Length != 3) throw new InvalidOperationException("Malformed OpenID token.");
        using var header = JsonDocument.Parse(FromUrlSafe(segments[0]));
        using var payload = JsonDocument.Parse(FromUrlSafe(segments[1]));
        if (header.RootElement.GetProperty("alg").GetString() != "RS256")
            throw new InvalidOperationException("Unexpected OpenID signature algorithm.");
        var kid = header.RootElement.GetProperty("kid").GetString();
        using var response = await Http.GetAsync(AuthBase + "/.well-known/jwks.json", ct);
        response.EnsureSuccessStatusCode();
        using var jwks = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var matching = jwks.RootElement.GetProperty("keys").EnumerateArray()
            .Where(k => k.TryGetProperty("kid", out var kId) && kId.GetString() == kid &&
                        k.TryGetProperty("kty", out var kt) && kt.GetString() == "RSA")
            .ToArray();
        if (matching.Length != 1) throw new InvalidOperationException("OpenID signing key not found.");
        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = FromUrlSafe(matching[0].GetProperty("n").GetString() ?? ""),
            Exponent = FromUrlSafe(matching[0].GetProperty("e").GetString() ?? "")
        });
        bool validSignature = rsa.VerifyData(Encoding.ASCII.GetBytes(segments[0] + "." + segments[1]),
            FromUrlSafe(segments[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var p = payload.RootElement;
        bool validAudience = p.GetProperty("aud").ValueKind == JsonValueKind.Array
            ? p.GetProperty("aud").EnumerateArray().Any(x => x.GetString() == clientId)
            : p.GetProperty("aud").GetString() == clientId;
        if (!validSignature || p.GetProperty("iss").GetString() != AuthBase || !validAudience ||
            p.GetProperty("nonce").GetString() != expectedNonce ||
            p.GetProperty("exp").GetInt64() <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            throw new InvalidOperationException("OpenID token validation failed.");
        var sub = p.GetProperty("sub").GetString();
        if (string.IsNullOrWhiteSpace(sub)) throw new InvalidOperationException("No OpenID account subject.");
        return sub;
    }

    /// <summary>Reports request phases and the received partial response.
    /// Progress contains only user-visible output, never credentials.</summary>
    public async Task<(string Model, string Text)> ReviewAsync(
        string prompt, CancellationToken ct = default,
        IProgress<(string Stage, string Text)>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("No deck provided.", nameof(prompt));
        progress?.Report(("Checking ChatGPT authorization…", ""));
        await EnsureConnectedAsync(ct);
        if (accessToken is null)
            throw new InvalidOperationException("Connect ChatGPT in Options first.");

        progress?.Report(("Finding an available model…", ""));
        using var modelsRequest = new HttpRequestMessage(HttpMethod.Get, Resource + "/models");
        modelsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var modelsResponse = await Http.SendAsync(modelsRequest, ct);
        if (!modelsResponse.IsSuccessStatusCode)
            throw new InvalidOperationException("ChatGPT model list failed (HTTP " +
                (int)modelsResponse.StatusCode + ").");
        using var catalog = JsonDocument.Parse(await modelsResponse.Content.ReadAsStringAsync(ct));
        string model = catalog.RootElement.GetProperty("models").EnumerateArray()
            .Where(x => x.TryGetProperty("visibility", out var v) && v.GetString() == "list")
            .Select(x => x.GetProperty("slug").GetString())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? throw new InvalidOperationException("No compatible models available for this account.");

        progress?.Report(("Sending deck to ChatGPT (" + model + ")…", ""));
        using var request = new HttpRequestMessage(HttpMethod.Post, Resource + "/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model,
            input = new[] { new { role = "user", content = prompt } },
            store = false,
            stream = true
        }), Encoding.UTF8, "application/json");
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            string advice = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => " Reconnect your ChatGPT account in Options.",
                HttpStatusCode.Forbidden => " The connected account is not authorized for this model.",
                HttpStatusCode.TooManyRequests => " This account is rate limited; retry later.",
                _ => ""
            };
            throw new InvalidOperationException("ChatGPT review request failed (HTTP " +
                (int)response.StatusCode + ")." + advice);
        }

        progress?.Report(("Connected; receiving review…", ""));
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        var result = new StringBuilder();
        bool completed = false;
        string? eventName = null;
        var eventData = new StringBuilder();
        long lastUpdate = Environment.TickCount64;

        void ProcessEvent()
        {
            if (eventData.Length == 0) { eventName = null; return; }
            string data = eventData.ToString();
            eventData.Clear();
            if (data == "[DONE]") { eventName = null; return; }
            using var item = JsonDocument.Parse(data);
            var ev = item.RootElement;
            string? type = ev.TryGetProperty("type", out var t) ? t.GetString() : eventName;
            eventName = null;
            if (type == "response.output_text.delta" &&
                ev.TryGetProperty("delta", out var delta))
            {
                string fragment = delta.GetString() ?? "";
                if (result.Length + fragment.Length > 250_000)
                    throw new InvalidOperationException("Review exceeded the supported response size.");
                result.Append(fragment);
                long now = Environment.TickCount64;
                if (now - lastUpdate >= 220)
                {
                    progress?.Report(("Receiving review…", result.ToString()));
                    lastUpdate = now;
                }
            }
            else if (type == "response.completed")
            {
                completed = true;
                // The API may include the final full output instead of deltas.
                if (result.Length == 0 &&
                    ev.TryGetProperty("response", out var full) &&
                    full.TryGetProperty("output", out var output) &&
                    output.ValueKind == JsonValueKind.Array)
                {
                    foreach (var message in output.EnumerateArray())
                    {
                        if (!message.TryGetProperty("content", out var content) ||
                            content.ValueKind != JsonValueKind.Array) continue;
                        foreach (var segment in content.EnumerateArray())
                        {
                            if (segment.TryGetProperty("type", out var kind) &&
                                kind.GetString() == "output_text" &&
                                segment.TryGetProperty("text", out var text))
                                result.Append(text.GetString());
                        }
                    }
                }
            }
            else if (type is "response.failed" or "response.incomplete" or "error")
                throw new InvalidOperationException("ChatGPT ended the request with " + type + ".");
        }

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            // Responses uses SSE; one event may contain multiple data: lines.
            if (line.Length == 0) { ProcessEvent(); continue; }
            if (line.StartsWith("event:", StringComparison.Ordinal))
                eventName = line[6..].Trim();
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (eventData.Length != 0) eventData.Append('\n');
                eventData.Append(line[5..].TrimStart());
            }
        }
        ProcessEvent();
        if (!completed)
            throw new InvalidOperationException("The ChatGPT response stream ended before completion.");
        if (string.IsNullOrWhiteSpace(result.ToString()))
            throw new InvalidOperationException("ChatGPT completed the request but returned no review text.");
        progress?.Report(("Review received.", result.ToString()));
        return (model, result.ToString());
    }

}
