using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArcaneVault.CloudSync;

/// <summary>Opt-in HTTP transport; never changes Arcane Vault local storage or applies remote content.</summary>
public sealed class WixSyncClient
{
    private readonly HttpClient _http;
    private readonly Uri _baseUri;

    public WixSyncClient(HttpClient http, Uri baseUri)
    {
        if (baseUri.Scheme != Uri.UriSchemeHttps) throw new ArgumentException("HTTPS required", nameof(baseUri));
        _http = http;
        _baseUri = baseUri;
    }

    public async Task<SyncPullResponse> PullAsync(string token, CancellationToken cancellationToken = default)
    {
        using var request = Request("avSyncPull", token, new { });
        using var result = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await ThrowIfNotSuccess(result, cancellationToken);
        return await result.Content.ReadFromJsonAsync<SyncPullResponse>(cancellationToken: cancellationToken)
            ?? throw new JsonException("Empty cloud sync response");
    }

    public async Task<int> PushAsync(string token, string deviceId, int expectedRevision, string peerSnapshotJson,
        CancellationToken cancellationToken = default)
    {
        if (deviceId is not ("windows" or "android")) throw new ArgumentException("Invalid device", nameof(deviceId));
        if (expectedRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
        using var parsed = JsonDocument.Parse(peerSnapshotJson);
        if (parsed.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Snapshot must be an object");
        if (peerSnapshotJson.Length > 250000) throw new ArgumentException("Snapshot exceeds Wix Light transport limit");
        using var request = Request("avSyncPush", token, new { deviceId, expectedRevision, payload = peerSnapshotJson });
        using var result = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await ThrowIfNotSuccess(result, cancellationToken);
        var receipt = await result.Content.ReadFromJsonAsync<SyncPushResponse>(cancellationToken: cancellationToken);
        return receipt?.revision ?? throw new JsonException("Missing revision");
    }

    private HttpRequestMessage Request(string endpoint, string token, object payload)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Missing sync token", nameof(token));
        var url = new Uri(_baseUri, "/_functions/" + endpoint);
        var message = new HttpRequestMessage(HttpMethod.Post, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        message.Content = JsonContent.Create(payload);
        return message;
    }

    private static async Task ThrowIfNotSuccess(HttpResponseMessage result, CancellationToken cancellationToken)
    {
        if (result.IsSuccessStatusCode) return;
        // Never log token or peer payload. Let the caller display a user-friendly status.
        var error = result.StatusCode == HttpStatusCode.Conflict ? "Cloud slot changed; pull again before retrying."
            : result.StatusCode == HttpStatusCode.Unauthorized ? "Cloud token rejected."
            : $"Wix sync HTTP {(int)result.StatusCode}.";
        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();
        throw new HttpRequestException(error, null, result.StatusCode);
    }

    public sealed record SyncSlot(string deviceId, int revision, string payload, DateTimeOffset? updatedAt);
    public sealed record SyncPullResponse(int protocol, SyncSlot[] slots);
    public sealed record SyncPushResponse(string deviceId, int revision);
}
