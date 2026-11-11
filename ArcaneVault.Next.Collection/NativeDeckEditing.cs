using ArcaneVault.Next.Domain;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Native deck library editor. Preserves imported deck IDs, metadata, artwork,
/// revisions, and unknown JSON keys; changes only the explicitly selected deck.
/// It never asserts format legality, ownership, or changes a physical/Arena row.
/// </summary>
public static class NativeDeckEditing
{
    public sealed record DeckCard(string Name, int Quantity);
    public sealed record DeckRow(int Ordinal, string Id, string Name,
        string Commander, string Format, IReadOnlyList<DeckCard> Cards,
        int Revisions, string OriginalJson)
    {
        public int CardCount => Cards.Sum(x => x.Quantity) +
            (string.IsNullOrWhiteSpace(Commander) ? 0 : 1);
        public string Summary => $"{CardCount:N0} cards · {Revisions:N0} revisions";
    }

    private const int MaxDecks = 10_000;
    private const int MaxEntries = 2_000;
    private const int MaxRevisions = 149_999;

    private static string Name(string? s, string field)
    {
        string value = (s ?? "").Trim();
        if (value.Length is < 1 or > 250 || value.Any(char.IsControl))
            throw new ArgumentException($"Provide a valid {field} (1–250 characters).", field);
        return value;
    }

    private static string Optional(string? s, string field)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        return Name(s, field);
    }

    public static IReadOnlyList<DeckCard> ParseCardLines(string lines)
    {
        if (lines.Length > 240_000)
            throw new InvalidDataException("Deck list is too large.");
        var result = new List<DeckCard>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            int space = line.IndexOf(' ');
            if (space <= 0 || !int.TryParse(line[..space], out int quantity) ||
                quantity is < 1 or > 999)
                throw new InvalidDataException(
                    "Use one card per line: quantity then card name (for example: 2 Sol Ring).");
            var name = Name(line[(space + 1)..], "card name");
            if (!seen.Add(name))
                throw new InvalidDataException(
                    $"Duplicate card name '{name}'. Combine duplicate quantities explicitly.");
            result.Add(new DeckCard(name, quantity));
            if (result.Count > MaxEntries)
                throw new InvalidDataException("Deck exceeds the 2,000-line limit.");
        }
        return result.AsReadOnly();
    }

    public static string FormatCardLines(IEnumerable<DeckCard> cards) =>
        string.Join(Environment.NewLine, cards.Select(c => $"{c.Quantity} {c.Name}"));

    private static string Txt(JsonObject row, string key)
    {
        if (row[key] is not JsonValue value) return "";
        try { return value.GetValue<string>(); }
        catch (InvalidOperationException) { return ""; }
    }

    private static IReadOnlyList<DeckCard> ReadEntries(JsonObject deck)
    {
        if (deck["entries"] is not JsonArray entries)
            throw new InvalidDataException("Saved deck lacks an entries array.");
        if (entries.Count > MaxEntries)
            throw new InvalidDataException("Deck contains too many card entries.");
        var cards = new List<DeckCard>(entries.Count);
        foreach (var item in entries)
        {
            if (item is not JsonObject entry ||
                entry["qty"] is not JsonValue qtyValue)
                throw new InvalidDataException("Saved deck contains an invalid card row.");
            int quantity;
            try { quantity = qtyValue.GetValue<int>(); }
            catch (InvalidOperationException)
            {
                throw new InvalidDataException("Saved deck has a non-integer quantity.");
            }
            if (quantity is < 1 or > 999)
                throw new InvalidDataException("Saved deck card quantities must be 1–999.");
            cards.Add(new DeckCard(Name(Txt(entry, "name"), "saved card name"), quantity));
        }
        return cards;
    }

    private static JsonObject Parse(byte[] bytes, out NativeBackupView view, out JsonArray decks)
    {
        view = NativeBackupReader.Read(bytes);
        var root = JsonNode.Parse(bytes) as JsonObject ??
            throw new InvalidDataException("Native backup is not a JSON object.");
        decks = root["deckLibrary"] as JsonArray ??
            throw new InvalidDataException("Native backup has no deck library.");
        if (decks.Count != view.Decks)
            throw new InvalidDataException("Native deck library count does not match backup.");
        return root;
    }

    private static DeckRow Inspect(JsonNode? node, int ordinal)
    {
        if (node is not JsonObject deck)
            throw new InvalidDataException("Imported deck is malformed.");
        var cards = ReadEntries(deck);
        var revisions = deck["revisions"] as JsonArray;
        string commander = Txt(deck, "commander");
        string format = Txt(deck, "format");
        if (format.Length == 0)
            format = string.IsNullOrWhiteSpace(commander) ? "60-card" : "Commander";
        return new DeckRow(ordinal, Txt(deck, "id"),
            Name(Txt(deck, "name"), "deck name"),
            commander, format, cards,
            revisions?.Count ?? 0, deck.ToJsonString());
    }

    public static IReadOnlyList<DeckRow> List(byte[] source)
    {
        _ = Parse(source, out _, out var decks);
        return decks.Select((deck, index) => Inspect(deck, index)).ToArray();
    }

    private static JsonObject RequireSelected(JsonArray decks, DeckRow selected)
    {
        if (selected.Ordinal < 0 || selected.Ordinal >= decks.Count ||
            decks[selected.Ordinal] is not JsonObject deck ||
            !JsonNode.DeepEquals(deck, JsonNode.Parse(selected.OriginalJson)))
            throw new InvalidDataException(
                "Selected deck has changed since it was displayed. Reopen the deck library.");
        return deck;
    }

    private static JsonArray PrepareEntries(IReadOnlyList<DeckCard> cards,
        JsonObject? previous)
    {
        if (cards.Count > MaxEntries)
            throw new InvalidDataException("Too many deck card rows.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new JsonArray();

        // Reuse annotations when the same card existed in the previous version.
        var originals = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        if (previous?["entries"] is JsonArray prior)
            foreach (var node in prior)
                if (node is JsonObject row)
                {
                    string name = Txt(row, "name");
                    if (!originals.ContainsKey(name)) originals.Add(name, row);
                }

        foreach (var card in cards)
        {
            string name = Name(card.Name, "deck card name");
            if (card.Quantity is < 1 or > 999)
                throw new ArgumentOutOfRangeException(nameof(cards),
                    "Card quantities must be between 1 and 999.");
            if (!seen.Add(name))
                throw new InvalidDataException("Duplicate card names must be combined explicitly.");
            var node = originals.TryGetValue(name, out var previousEntry)
                ? (JsonObject)previousEntry.DeepClone() : new JsonObject();
            node["name"] = name;
            node["qty"] = card.Quantity;
            entries.Add(node);
        }
        return entries;
    }

    private static JsonArray AsRevisions(JsonObject deck)
    {
        if (deck["revisions"] is JsonArray revisions) return revisions;
        if (deck.ContainsKey("revisions") && deck["revisions"] is not null)
            throw new InvalidDataException("Saved revision history has an invalid structure.");
        var made = new JsonArray();
        deck["revisions"] = made;
        return made;
    }

    private static void SnapshotBeforeEdit(JsonObject deck)
    {
        JsonArray revisions = AsRevisions(deck);
        if (revisions.Count >= MaxRevisions)
            throw new InvalidDataException(
                "Deck revision limit reached. Export your library before further edits.");
        var previous = (JsonObject)deck.DeepClone();
        previous.Remove("revisions");
        previous.Remove("id");
        previous["savedAt"] = DateTimeOffset.UtcNow.ToString("O");
        previous["label"] = "Before native deck update";
        revisions.Add(previous);
    }

    private static byte[] Verify(JsonObject previous, JsonObject edited,
        NativeBackupView before, int expectedDecks, int expectedRevisions)
    {
        foreach (var item in previous)
            if (item.Key != "deckLibrary" &&
                !JsonNode.DeepEquals(item.Value, edited[item.Key]))
                throw new InvalidDataException(
                    "Deck operation would modify a non-deck collection section.");
        if (edited.Count != previous.Count)
            throw new InvalidDataException("Deck operation changed backup root structure.");

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(edited);
        var after = NativeBackupReader.Read(bytes);
        if (after.Decks != expectedDecks || after.DeckRevisions != expectedRevisions ||
            !after.PhysicalCards.SequenceEqual(before.PhysicalCards) ||
            !after.ArenaCards.SequenceEqual(before.ArenaCards) ||
            after.PreferredArtworks != before.PreferredArtworks)
            throw new InvalidDataException("Deck operation unexpectedly changed inventory or deck history.");
        return bytes;
    }

    public static byte[] Create(byte[] source, string name, string commander,
        string format, IReadOnlyList<DeckCard> cards)
    {
        string title = Name(name, "deck name");
        string cmd = Optional(commander, "commander");
        string type = Name(format, "format");
        var root = Parse(source, out var before, out var decks);
        if (decks.Count >= MaxDecks)
            throw new InvalidDataException("Deck library is full.");
        var initial = new JsonObject
        {
            ["id"] = "native_" + Guid.NewGuid().ToString("N"),
            ["name"] = title,
            ["commander"] = cmd,
            ["format"] = type,
            ["entries"] = PrepareEntries(cards, null),
            ["revisions"] = new JsonArray(),
            ["updated"] = DateTimeOffset.UtcNow.ToString("O"),
            ["folder"] = "Unfiled",
            ["tags"] = new JsonArray(),
            ["favorite"] = false,
            ["coverCard"] = ""
        };
        var previous = (JsonObject)root.DeepClone();
        decks.Add(initial);
        return Verify(previous, root, before, before.Decks + 1, before.DeckRevisions);
    }

    public static byte[] Update(byte[] source, DeckRow selected,
        string name, string commander, string format, IReadOnlyList<DeckCard> cards)
    {
        ArgumentNullException.ThrowIfNull(selected);
        string title = Name(name, "deck name");
        string cmd = Optional(commander, "commander");
        string type = Name(format, "format");
        var root = Parse(source, out var before, out var decks);
        var original = RequireSelected(decks, selected);
        var newEntries = PrepareEntries(cards, original);
        if (Txt(original, "name") == title &&
            Txt(original, "commander") == cmd &&
            (Txt(original, "format") == type ||
             (Txt(original, "format") == "" && selected.Format == type)) &&
            JsonNode.DeepEquals(original["entries"], newEntries))
            throw new InvalidDataException("Deck has no changes. No recovery file was created.");

        var previous = (JsonObject)root.DeepClone();
        SnapshotBeforeEdit(original);
        original["name"] = title;
        original["commander"] = cmd;
        original["format"] = type;
        original["entries"] = newEntries;
        original["updated"] = DateTimeOffset.UtcNow.ToString("O");
        return Verify(previous, root, before, before.Decks, before.DeckRevisions + 1);
    }

    public static byte[] Delete(byte[] source, DeckRow selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        var root = Parse(source, out var before, out var decks);
        _ = RequireSelected(decks, selected);
        var previous = (JsonObject)root.DeepClone();
        decks.RemoveAt(selected.Ordinal);
        return Verify(previous, root, before,
            before.Decks - 1, before.DeckRevisions - selected.Revisions);
    }

    public static byte[] Duplicate(byte[] source, DeckRow selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        var root = Parse(source, out var before, out var decks);
        var original = RequireSelected(decks, selected);
        if (decks.Count >= MaxDecks)
            throw new InvalidDataException("Deck library is full.");
        var previous = (JsonObject)root.DeepClone();
        var copy = (JsonObject)original.DeepClone();
        copy["id"] = "native_" + Guid.NewGuid().ToString("N");
        copy["name"] = Name(selected.Name + " — Copy", "duplicate deck name");
        copy["revisions"] = new JsonArray();
        copy["arenaDeckId"] = "";
        copy["arenaSyncedAt"] = "";
        copy["updated"] = DateTimeOffset.UtcNow.ToString("O");
        decks.Add(copy);
        return Verify(previous, root, before, before.Decks + 1, before.DeckRevisions);
    }
}
