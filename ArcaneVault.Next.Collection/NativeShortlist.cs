using ArcaneVault.Next.Domain;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Native implementation of the shipping WebView 'favorites' name array.
/// Shortlist entries are bookmarks, NEVER inventory or printing ownership.
/// This module preserves the original schema-2 backup and unknown fields.
/// </summary>
public static class NativeShortlist
{
    public sealed record Entry(string Name, long VerifiedPhysicalCopies,
        long UnresolvedPhysicalCopies, long ArenaCopies)
    {
        public bool ConfirmedOwned => VerifiedPhysicalCopies > 0 || ArenaCopies > 0;
        public string PhysicalLabel => VerifiedPhysicalCopies > 0
            ? VerifiedPhysicalCopies.ToString("N0") + " verified"
            : "0 verified";
        public string UnresolvedLabel => UnresolvedPhysicalCopies > 0
            ? UnresolvedPhysicalCopies.ToString("N0") + " unresolved"
            : "—";
        public string ArenaLabel => ArenaCopies.ToString("N0");
        public string OwnershipLabel =>
            ConfirmedOwned ? "Some owned" :
            UnresolvedPhysicalCopies > 0 ? "Possibly physically owned (unresolved)" :
            "No verified owned copies";
    }

    private const int MaximumBookmarks = 20_000;

    public static IReadOnlyList<string> Read(byte[] backup)
    {
        ArgumentNullException.ThrowIfNull(backup);
        _ = NativeBackupReader.Read(backup);
        using var doc = JsonDocument.Parse(backup);
        if (!doc.RootElement.TryGetProperty("favorites", out var favorites))
            return Array.Empty<string>();
        if (favorites.ValueKind != JsonValueKind.Array ||
            favorites.GetArrayLength() > MaximumBookmarks)
            throw new InvalidDataException("The legacy shortlist is not a valid favorites array.");
        var names = new List<string>(favorites.GetArrayLength());
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in favorites.EnumerateArray())
        {
            if (name.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("A shortlist entry is not a card name.");
            string value = name.GetString() ?? "";
            if (!Valid(value) || !seen.Add(value.Trim()))
                throw new InvalidDataException(
                    "The shortlist has an invalid or ambiguous duplicate card name; " +
                    "the original data was left unchanged.");
            names.Add(value);
        }
        return names.AsReadOnly();
    }

    private static bool Valid(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.Length <= 300 && name.Trim().Length > 0 &&
        !name.Any(char.IsControl);

    public static byte[] Set(byte[] backup, string name, bool bookmarked)
    {
        if (!Valid(name))
            throw new InvalidDataException("Choose a valid nonempty card name (up to 300 characters).");
        name = name.Trim();
        var before = NativeBackupReader.Read(backup);
        var names = Read(backup);
        bool found = names.Any(x => x.Trim().Equals(name,
            StringComparison.OrdinalIgnoreCase));
        if (found == bookmarked)
            throw new InvalidDataException(bookmarked
                ? "This card is already in My Shortlist."
                : "This card is not saved in My Shortlist.");
        if (bookmarked && names.Count >= MaximumBookmarks)
            throw new InvalidDataException("My Shortlist is at its 20,000-card safety limit.");

        var root = JsonNode.Parse(backup) as JsonObject ??
            throw new InvalidDataException("The native collection backup is invalid.");
        var updated = new JsonArray();
        foreach (var oldName in names)
            if (!oldName.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                updated.Add(oldName);
        if (bookmarked) updated.Add(name);
        root["favorites"] = updated;

        byte[] result = JsonSerializer.SerializeToUtf8Bytes(root);
        var after = NativeBackupReader.Read(result);
        if (!before.PhysicalCards.SequenceEqual(after.PhysicalCards) ||
            !before.ArenaCards.SequenceEqual(after.ArenaCards) ||
            before.Decks != after.Decks ||
            before.DeckRevisions != after.DeckRevisions ||
            before.PreferredArtworks != after.PreferredArtworks ||
            before.PhysicalCopies != after.PhysicalCopies ||
            before.ArenaCopies != after.ArenaCopies)
            throw new InvalidDataException("Shortlist update would change collection or deck state.");
        return result;
    }

    public static IReadOnlyList<Entry> Summarize(IReadOnlyList<string> names,
        NativeBackupView collection, CollectionCatalogReview.Result review)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(review);
        var physical = new Dictionary<string, (long Verified, long Unresolved)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var row in review.Rows)
        {
            var values = physical.GetValueOrDefault(row.Name.Trim());
            long count = row.Quantity;
            physical[row.Name.Trim()] = row.Confirmed
                ? (checked(values.Verified + count), values.Unresolved)
                : (values.Verified, checked(values.Unresolved + count));
        }
        var arena = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in collection.ArenaCards)
            arena[card.Name.Trim()] = checked(arena.GetValueOrDefault(card.Name.Trim())
                + card.Quantity);

        return names.Select(name =>
        {
            var counts = physical.GetValueOrDefault(name.Trim());
            return new Entry(name, counts.Verified, counts.Unresolved,
                arena.GetValueOrDefault(name.Trim()));
        }).ToArray();
    }

    /// <summary>Matches legacy 'Copy as Arena list' exactly: one card per name.</summary>
    public static string ArenaText(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (names.Count == 0) return "";
        if (names.Count > MaximumBookmarks || names.Any(n => !Valid(n)))
            throw new InvalidDataException("Shortlist cannot be exported as valid Arena text.");
        return string.Join("\n", names.Select(n => "1 " + n.Trim()));
    }
}
