using ArcaneVault.Next.Collection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Optional presentation-only analytics attached to peer sync envelopes.
/// Never participates in inventory merge, baseline authority or deck writes.
/// The existing peer format/schema/revision/data remain unchanged.
/// </summary>
internal static class NativePeerWithAnalytics
{
    internal const int MaxMatchRows = 2000;
    internal static byte[] Export(string workspacePath, string deviceId)
    {
        byte[] original = NativePeerSync.Export(workspacePath, deviceId);
        var root = JsonNode.Parse(original) as JsonObject ??
            throw new InvalidDataException("Peer envelope unavailable.");
        var rows = NativeMatchHistoryView.ReadPerformance()
            .Where(x => x.Result is "win" or "loss" &&
                !string.IsNullOrWhiteSpace(x.DeckName))
            .OrderByDescending(x => x.Played)
            .Take(MaxMatchRows)
            .Select(x => new {
                playedUtc = x.Played.ToUniversalTime().ToString("O"),
                deckName = x.DeckName,
                format = x.Format,
                result = x.Result
            }).ToArray();
        root["matchAnalytics"] = JsonSerializer.SerializeToNode(new {
            schema = 1,
            source = "Arcane Vault Windows confirmed match history",
            asOfUtc = DateTimeOffset.UtcNow.ToString("O"),
            records = rows
        });
        byte[] result = JsonSerializer.SerializeToUtf8Bytes(root);
        if (result.Length > NativePeerSync.MaxBytes)
            throw new InvalidDataException("Peer export with analytics exceeds size limit.");
        return result;
    }
}
