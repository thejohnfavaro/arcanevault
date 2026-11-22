using System.IO;
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
        // Analytics are optional. A locked, corrupted, or inaccessible history
        // must never prevent inventory sync, backup or a verified baseline.
        MatchPerformanceAnalytics.Entry[] source;
        try
        {
            source = NativeMatchHistoryView.ReadPerformance().ToArray();
        }
        catch (Exception error) when (error is IOException or
            Microsoft.Data.Sqlite.SqliteException or UnauthorizedAccessException or
            InvalidOperationException or ArgumentException)
        {
            return original;
        }
        var rows = source
            .Where(x => x.Result is "win" or "loss" &&
                !string.IsNullOrWhiteSpace(x.DeckName))
            .OrderByDescending(x => x.Played)
            .Take(MaxMatchRows)
            .Select(x => new {
                playedUtc = x.Played.ToUniversalTime().ToString("O"),
                deckName = x.DeckName,
                format = x.Format,
                result = x.Result,
                opponentColors = x.OpponentColors
            }).ToArray();
        root["matchAnalytics"] = JsonSerializer.SerializeToNode(new {
            schema = 1,
            source = "Arcane Vault Windows confirmed match history",
            asOfUtc = DateTimeOffset.UtcNow.ToString("O"),
            records = rows
        });
        byte[] result = JsonSerializer.SerializeToUtf8Bytes(root);
        // Prefer the original fully verified inventory envelope if the
        // optional presentation metadata would exceed the peer size limit.
        return result.Length <= NativePeerSync.MaxBytes ? result : original;
    }
}
