namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Deterministic, read-only mapping of existing match records to saved decks.
/// Does not create results or change a match's verified stored attribution.
/// </summary>
internal static class MatchDeckAttribution
{
    internal sealed record Deck(string Id, string Name, string ArenaId);
    internal sealed record Match(string MatchId, string DeckName, string ArenaId,
        string NativeId, string Note);
    // An older Match History row may preserve a verified Arena submission
    // or explicit manual assignment in its note but lack native_deck_id.
    // Only these existing attribution markers may supplement exact ID matches.
    internal static bool HasVerifiedAssignmentNote(string? note)
    {
        note ??= "";
        return note.Contains("manually confirmed", StringComparison.OrdinalIgnoreCase) ||
            note.Equals("Deck assignment: manual", StringComparison.OrdinalIgnoreCase) ||
            note.StartsWith("Deck assignment: recent_arena_", StringComparison.OrdinalIgnoreCase) ||
            note.StartsWith("Deck assignment: verified_arena_", StringComparison.OrdinalIgnoreCase) ||
            note.StartsWith("Deck assignment: match_deck_name", StringComparison.OrdinalIgnoreCase);
    }

    internal static string? Resolve(Match match, IReadOnlyList<Deck> decks,
        IReadOnlyDictionary<string, string> arenaToNative,
        IReadOnlySet<string>? blockedArenaIds = null)
    {
        if (string.IsNullOrWhiteSpace(match.MatchId)) return null;
        var byNative = decks.Where(x => x.Id == match.NativeId).ToArray();
        if (!string.IsNullOrWhiteSpace(match.NativeId))
            return byNative.Length == 1 ? byNative[0].Id : null;

        if (!string.IsNullOrWhiteSpace(match.ArenaId) &&
            arenaToNative.TryGetValue(match.ArenaId.Trim(), out var mapped))
            return decks.Count(x => x.Id == mapped) == 1 ? mapped : null;

        if (blockedArenaIds?.Contains(match.ArenaId.Trim()) == true) return null;
        if (string.IsNullOrWhiteSpace(match.DeckName)) return null;
        var byName = decks.Where(x =>
            x.Name.Trim().Equals(match.DeckName.Trim(),
                StringComparison.OrdinalIgnoreCase)).ToArray();
        if (byName.Length != 1) return null;

        // An unmatched Arena ID does not negate an explicitly confirmed
        // assignment or a named result verified by Arena. Untrusted inventory
        // labels and ambiguous IDs must NOT be matched merely by name.
        if (string.IsNullOrWhiteSpace(match.ArenaId))
            return byName[0].Id;
        return HasVerifiedAssignmentNote(match.Note) ? byName[0].Id : null;
    }
}
