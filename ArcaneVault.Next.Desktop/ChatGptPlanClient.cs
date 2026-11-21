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
/// Preview-only native ChatGPT plan OAuth client. Tokens live in memory and
/// are never logged, synced or persisted. Browser authorization is explicit.
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

    public bool IsConnected => accessToken is not null &&
        expiresAt > DateTimeOffset.UtcNow.AddSeconds(30);
    public string Status => IsConnected
        ? "ChatGPT plan usage authorized for this session"
        : "Not connected — no AI requests can be sent";

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

    public async Task<(string Model, string Text)> ReviewAsync(string prompt, CancellationToken ct = default)
    {
        if (!IsConnected || accessToken is null) throw new InvalidOperationException("Connect ChatGPT before reviewing.");
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("No deck provided.", nameof(prompt));
        using var modelsRequest = new HttpRequestMessage(HttpMethod.Get, Resource + "/models");
        modelsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var modelsResponse = await Http.SendAsync(modelsRequest, ct);
        if (!modelsResponse.IsSuccessStatusCode)
            throw new InvalidOperationException("ChatGPT model list request failed (HTTP " + (int)modelsResponse.StatusCode + ").");
        using var catalog = JsonDocument.Parse(await modelsResponse.Content.ReadAsStringAsync(ct));
        string model = catalog.RootElement.GetProperty("models").EnumerateArray()
            .Where(x => x.TryGetProperty("visibility", out var v) && v.GetString() == "list")
            .Select(x => x.GetProperty("slug").GetString())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? throw new InvalidOperationException("No compatible models available for this account.");
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
            throw new InvalidOperationException("ChatGPT review failed (HTTP " + (int)response.StatusCode + ").");
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        var result = new StringBuilder();
        bool completed = false;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;
            var data = line.Substring(6);
            if (data == "[DONE]") break;
            using var item = JsonDocument.Parse(data);
            var ev = item.RootElement;
            string? eventType = ev.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (eventType == "response.output_text.delta" && ev.TryGetProperty("delta", out var d))
                result.Append(d.GetString());
            else if (eventType == "response.completed") completed = true;
            else if (eventType is "response.failed" or "response.incomplete")
                throw new InvalidOperationException("ChatGPT did not complete the review.");
        }
        if (!completed) throw new InvalidOperationException("ChatGPT response stream ended before completion.");
        return (model, result.ToString());
    }

    public void Disconnect()
    {
        accessToken = null;
        expiresAt = default;
        subject = null;
        // Keep the issued client ID for routine sign-in during the same session.
    }
}
