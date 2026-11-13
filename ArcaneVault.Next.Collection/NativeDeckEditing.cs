using ArcaneVault.Next.Domain;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Edits ONLY the deckLibrary array in a preserved full native schema-2
/// document. All card ownership, printing identities, preferences and
/// opaque/legacy fields remain intact. Saves go through version-checked
/// SQLite replacement with a complete previous-workspace recovery point.
/// </summary>
public static class NativeDeckEditing
{
    public sealed record Entry(string Name, int Quantity);

    public sealed record DeckRow(
        int Ordinal, string Id, string Name, string Commander,
        string Folder, int EntryCount, int TotalCopies, int Revisions,
        bool Editable, string MainboardText, string SourceSha256,
        string Format = "", bool Favorite = false)
    {
        // Presentation-only catalog art, never persisted into the deck library.
        public string CoverArtworkUrl { get; init; } = "";
        public string FavoriteLabel => Favorite ? "★ Favorite" : "";
        // Exact per-deck display choice; independent of collection ownership.
        public IReadOnlyDictionary<string, string> EntryPrintingIds { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static string Hash(string json) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

    private static string CheckedText(string value, string field, int max, bool required)
    {
        if (value is null) throw new ArgumentNullException(field);
        string text = value.Trim();
        if ((required && text.Length == 0) || text.Length > max ||
            text.Any(c => char.IsControl(c)))
            throw new ArgumentException(
                field + " must be " + (required ? "1–" : "0–") +
                max + " characters without control characters.", field);
        return text;
    }

    /// <summary>
    /// A mainboard is plain "count card name" lines. Names are preserved,
    /// including split cards. Duplicate lines combine only within the
    /// explicitly edited mainboard; no owned inventory is inferred or changed.
    /// </summary>
    public static IReadOnlyList<Entry> ParseMainboard(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > 64_000)
            throw new InvalidDataException("The deck list is larger than the supported text limit.");

        var names = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        int lines = 0, total = 0;
        foreach (var raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (++lines > 500)
                throw new InvalidDataException("Deck lists may contain at most 500 nonempty lines.");
            var match = Regex.Match(line, @"^([0-9]{1,4})\s+(.+)$",
                RegexOptions.CultureInvariant);
            if (!match.Success ||
                !int.TryParse(match.Groups[1].Value, out int quantity) ||
                quantity is < 1 or > 250)
                throw new InvalidDataException(
                    "Enter mainboard entries as '1 Card Name' (1–250 each). Invalid line: " +
                    line[..Math.Min(90, line.Length)]);
            string name = CheckedText(match.Groups[2].Value, "Card name", 255, true);
            total = checked(total + quantity);
            if (total > 1_000)
                throw new InvalidDataException("Deck list exceeds 1,000 card copies.");
            if (names.TryGetValue(name, out var prior))
            {
                int combined = checked(prior.Quantity + quantity);
                if (combined > 250)
                    throw new InvalidDataException("Too many copies of " + name);
                names[name] = prior with { Quantity = combined };
            }
            else
                names.Add(name, new Entry(name, quantity));
        }
        return names.Values.ToArray();
    }

    /// <summary>
    /// Resolve only the mainboard line under a text editor's caret. This is a
    /// read-only preview hint, NOT validation of the entire draft or evidence
    /// that the card is owned/locally legal. It deliberately leaves split and
    /// double-faced names intact.
    /// </summary>
    public static string? PreviewNameAtCaret(string text, int caretIndex)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0 || text.Length > 64_000 ||
            caretIndex < 0 || caretIndex > text.Length)
            return null;

        int before = Math.Max(0, caretIndex - 1);
        int start = text.LastIndexOf('\n', before) + 1;
        int end = text.IndexOf('\n', start);
        if (end < 0) end = text.Length;
        string line = text[start..end].Trim();
        if (line.Length == 0) return null;

        var match = Regex.Match(line, @"^([0-9]{1,4})\s+(.+)$",
            RegexOptions.CultureInvariant);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, out int quantity) ||
            quantity is < 1 or > 250)
            return null;
        string name = match.Groups[2].Value.Trim();
        return name.Length is > 0 and <= 255 && !name.Any(char.IsControl)
            ? name : null;
    }

    private static IReadOnlyList<DeckRow> Describe(byte[] backup,
        out JsonObject root, out JsonArray library)
    {
        NativeBackupReader.Read(backup);
        root = JsonNode.Parse(backup) as JsonObject ??
            throw new InvalidDataException("Invalid full native backup.");
        library = root["deckLibrary"] as JsonArray ??
            throw new InvalidDataException("Missing native deck library.");
        var items = new List<DeckRow>(library.Count);
        for (int i = 0; i < library.Count; i++)
        {
            if (library[i] is not JsonObject deck)
                throw new InvalidDataException("A saved deck is not a JSON object.");
            string name = GetString(deck, "name");
            string commander = GetString(deck, "commander");
            string id = GetString(deck, "id");
            string folder = GetString(deck, "folder");
            string format = GetString(deck, "format");
            bool favorite = deck["favorite"] is JsonValue favoriteValue &&
                bool.TryParse(favoriteValue.ToJsonString(), out bool storedFavorite) &&
                storedFavorite;
            if (format.Length == 0)
                format = string.IsNullOrWhiteSpace(commander) ? "60-card" : "Commander";
            var entries = deck["entries"] as JsonArray ??
                throw new InvalidDataException("A saved deck is missing its card list.");
            int revisions = (deck["revisions"] as JsonArray)?.Count ?? 0;
            var mainboard = new List<string>(entries.Count);
            bool editable = entries.Count <= 500;
            int copies = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (entry is not JsonObject card ||
                    !int.TryParse(GetStringOrNumber(card, "qty"), out int qty) ||
                    qty is < 1 or > 250)
                {
                    editable = false;
                    continue;
                }
                string cardName = GetString(card, "name");
                if (string.IsNullOrWhiteSpace(cardName) || cardName.Length > 255 ||
                    !seen.Add(cardName))
                {
                    editable = false;
                    continue;
                }
                copies = checked(copies + qty);
                if (copies > 1_000) editable = false;
                mainboard.Add(qty + " " + cardName);
            }

            string json = deck.ToJsonString();
            items.Add(new DeckRow(i, id, name, commander,
                string.IsNullOrWhiteSpace(folder) ? "Unfiled" : folder,
                entries.Count, copies, revisions, editable,
                string.Join("\n", mainboard), Hash(json), format, favorite)
            {
                EntryPrintingIds = entries.OfType<JsonObject>()
                    .Where(x => GetString(x, "name").Length > 0 &&
                        Guid.TryParse(GetString(x, "displayPrintId"), out var id) &&
                        id != Guid.Empty)
                    .GroupBy(x => GetString(x, "name"), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => GetString(x.First(), "displayPrintId"),
                        StringComparer.OrdinalIgnoreCase)
            });
        }
        return items.AsReadOnly();
    }

    private static string GetString(JsonObject obj, string key)
    {
        try { return obj[key]?.GetValue<string>() ?? ""; }
        catch (InvalidOperationException) { return ""; }
        catch (FormatException) { return ""; }
    }

    private static string GetStringOrNumber(JsonObject obj, string key)
    {
        try
        {
            if (obj[key] is JsonValue v)
                return v.ToJsonString().Trim('"');
        }
        catch (InvalidOperationException) { }
        return "";
    }

    public static IReadOnlyList<DeckRow> List(byte[] preservedBackup)
    {
        ArgumentNullException.ThrowIfNull(preservedBackup);
        return Describe(preservedBackup, out _, out _);
    }

    private static JsonArray EntriesForSave(IReadOnlyList<Entry> entries,
        JsonArray? original)
    {
        var prior = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        if (original is not null)
            foreach (var node in original)
                if (node is JsonObject row)
                {
                    var name = GetString(row, "name");
                    if (!string.IsNullOrWhiteSpace(name) && !prior.ContainsKey(name))
                        prior[name] = row;
                }

        var result = new JsonArray();
        foreach (var entry in entries)
        {
            // Preserve any future metadata attached to an unchanged card name.
            // Only name and qty are edited; deleted rows are explicit.
            JsonObject payload = prior.TryGetValue(entry.Name, out var old)
                ? (JsonObject)old.DeepClone() : new JsonObject();
            payload["name"] = entry.Name;
            payload["qty"] = entry.Quantity;
            result.Add(payload);
        }
        return result;
    }

    private static bool SamePrintingMap(IReadOnlyDictionary<string, string> a,
        IReadOnlyDictionary<string, string> b) =>
        a.Count == b.Count && a.All(x =>
            b.TryGetValue(x.Key, out var id) &&
            string.Equals(x.Value, id, StringComparison.OrdinalIgnoreCase));

    private static JsonArray WithDisplayPrints(JsonArray entries,
        IReadOnlyDictionary<string, string>? selected)
    {
        if (selected is null) return entries;
        foreach (var row in entries.OfType<JsonObject>())
        {
            var name = GetString(row, "name");
            if (selected.TryGetValue(name, out var id))
                row["displayPrintId"] = Guid.Parse(id).ToString("D");
            else row.Remove("displayPrintId");
        }
        return entries;
    }

    private static void VerifyOtherSections(NativeBackupView before,
        NativeBackupView after, int expectedDeckCount, int expectedRevisions)
    {
        if (after.Decks != expectedDeckCount || after.DeckRevisions != expectedRevisions ||
            before.PhysicalCopies != after.PhysicalCopies ||
            before.ArenaCopies != after.ArenaCopies ||
            before.PreferredArtworks != after.PreferredArtworks ||
            !before.PhysicalCards.SequenceEqual(after.PhysicalCards) ||
            !before.ArenaCards.SequenceEqual(after.ArenaCards))
            throw new InvalidDataException(
                "Saving a deck would modify unrelated ownership or artwork data.");
    }

    private static JsonObject PreviousRevision(JsonObject deck)
    {
        var previous = new JsonObject
        {
            ["name"] = GetString(deck, "name"),
            ["commander"] = GetString(deck, "commander"),
            ["format"] = GetString(deck, "format"),
            ["entries"] = deck["entries"]?.DeepClone() ?? new JsonArray(),
            ["savedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["label"] = "Before native deck edit"
        };
        foreach (string key in new[] { "coverCard", "folder", "tags", "favorite" })
            if (deck[key] is not null)
                previous[key] = deck[key]!.DeepClone();
        return previous;
    }

    /// <summary>
    /// Add a saved deck or edit precisely one selected row with an unchanged
    /// fingerprint. Imported legacy IDs, revisions, deck cover, tags, folder,
    /// linked Arena IDs and other unknown fields are preserved on an update.
    /// </summary>
    public static byte[] Save(byte[] backup,
        string name, string commander, string mainboardText,
        DeckRow? selected = null, string? format = null,
        string? folder = null, bool? favorite = null,
        IReadOnlyDictionary<string, string>? displayPrintIds = null)
    {
        ArgumentNullException.ThrowIfNull(backup);
        string safeName = CheckedText(name, nameof(name), 120, true);
        string safeCommander = CheckedText(commander, nameof(commander), 255, false);
        string safeFormat = CheckedText(format ?? selected?.Format ??
            (safeCommander.Length > 0 ? "Commander" : "60-card"),
            nameof(format), 60, true);
        string safeFolder = CheckedText(
            folder ?? selected?.Folder ?? "Unfiled", nameof(folder), 64, true);
        bool safeFavorite = favorite ?? selected?.Favorite ?? false;
        var entries = ParseMainboard(mainboardText);
        // Null preserves all legacy selections; non-null is the explicit
        // authoritative unsaved display-choice map, with no ownership effect.
        if (displayPrintIds is not null)
        {
            var known = entries.Select(e => e.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var (cardName, printId) in displayPrintIds)
                if (!known.Contains(cardName) || !Guid.TryParse(printId, out var id) ||
                    id == Guid.Empty)
                    throw new InvalidDataException(
                        "A selected display printing has an invalid ID or card name.");
        }
        var before = NativeBackupReader.Read(backup);
        var currentRows = Describe(backup, out var root, out var library);
        int expectedCount = library.Count, expectedRevisions = before.DeckRevisions;

        if (selected is null)
        {
            if (library.Count >= 150_000)
                throw new InvalidDataException("Maximum supported saved decks reached.");
            var id = "deck_native_" + Guid.NewGuid().ToString("N");
            if (currentRows.Any(x => x.Id == id))
                throw new InvalidDataException("The new deck identifier collided with an existing deck.");
            library.Add(new JsonObject
            {
                ["id"] = id,
                ["name"] = safeName,
                ["commander"] = safeCommander,
                ["format"] = safeFormat,
                ["entries"] = WithDisplayPrints(EntriesForSave(entries, null),
                    displayPrintIds),
                ["updated"] = DateTimeOffset.UtcNow.ToString("O"),
                ["favorite"] = safeFavorite,
                ["folder"] = safeFolder,
                ["tags"] = new JsonArray(),
                ["coverCard"] = "",
                ["revisions"] = new JsonArray()
            });
            expectedCount++;
        }
        else
        {
            if (selected.Ordinal < 0 || selected.Ordinal >= library.Count ||
                currentRows[selected.Ordinal].SourceSha256 != selected.SourceSha256 ||
                !currentRows[selected.Ordinal].Editable)
                throw new InvalidDataException(
                    "The selected deck changed or cannot be edited safely. Reopen the library.");

            var row = (JsonObject)library[selected.Ordinal]!;
            var parsedOld = currentRows[selected.Ordinal];
            string newText = string.Join("\n", entries.Select(e => e.Quantity + " " + e.Name));
            if (parsedOld.Name == safeName &&
                parsedOld.Commander == safeCommander &&
                parsedOld.MainboardText == newText &&
                parsedOld.Format == safeFormat &&
                parsedOld.Folder == safeFolder &&
                parsedOld.Favorite == safeFavorite &&
                (displayPrintIds is null ||
                 SamePrintingMap(parsedOld.EntryPrintingIds, displayPrintIds)))
                throw new InvalidDataException("The deck has no changes; nothing was saved.");

            var history = row["revisions"] as JsonArray;
            if (history is null)
            {
                history = new JsonArray();
                row["revisions"] = history;
            }
            if (history.Count >= 10_000)
                throw new InvalidDataException(
                    "Deck revision history is too large to append safely.");
            history.Add(PreviousRevision(row));
            expectedRevisions++;
            var previousEntries = row["entries"] as JsonArray;
            row["name"] = safeName;
            row["commander"] = safeCommander;
            row["format"] = safeFormat;
            row["folder"] = safeFolder;
            row["favorite"] = safeFavorite;
            row["entries"] = WithDisplayPrints(
                EntriesForSave(entries, previousEntries), displayPrintIds);
            row["updated"] = DateTimeOffset.UtcNow.ToString("O");
        }

        byte[] output = JsonSerializer.SerializeToUtf8Bytes(root);
        var after = NativeBackupReader.Read(output);
        VerifyOtherSections(before, after, expectedCount, expectedRevisions);
        return output;
    }

    /// <summary>
    /// Duplicate precisely one saved deck using a new ID and no revision
    /// history or Arena sync link. No ownership, printing, or source deck
    /// is modified. Complete previous SQLite workspace is backed up by caller.
    /// </summary>
    public static byte[] Duplicate(byte[] backup, DeckRow selected)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(selected);
        var before = NativeBackupReader.Read(backup);
        var rows = Describe(backup, out var root, out var library);
        if (selected.Ordinal < 0 || selected.Ordinal >= library.Count ||
            rows[selected.Ordinal].SourceSha256 != selected.SourceSha256)
            throw new InvalidDataException("The selected deck changed since it was displayed.");
        if (library.Count >= 150_000)
            throw new InvalidDataException("Maximum saved decks reached.");

        var original = (JsonObject)library[selected.Ordinal]!;
        var clone = (JsonObject)original.DeepClone();
        clone["id"] = "deck_native_" + Guid.NewGuid().ToString("N");
        clone["name"] = CheckedText(selected.Name + " — Copy",
            "Duplicated deck name", 120, true);
        clone["revisions"] = new JsonArray();
        clone["arenaDeckId"] = "";
        clone["arenaSyncedAt"] = "";
        clone["updated"] = DateTimeOffset.UtcNow.ToString("O");
        library.Add(clone);

        byte[] output = JsonSerializer.SerializeToUtf8Bytes(root);
        var after = NativeBackupReader.Read(output);
        VerifyOtherSections(before, after, before.Decks + 1, before.DeckRevisions);
        return output;
    }

    public static byte[] Delete(byte[] backup, DeckRow selected)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(selected);
        var before = NativeBackupReader.Read(backup);
        var rows = Describe(backup, out var root, out var library);
        if (selected.Ordinal < 0 || selected.Ordinal >= library.Count ||
            rows[selected.Ordinal].SourceSha256 != selected.SourceSha256)
            throw new InvalidDataException(
                "The selected deck changed since it was displayed.");
        int revisions = rows[selected.Ordinal].Revisions;
        library.RemoveAt(selected.Ordinal);
        byte[] output = JsonSerializer.SerializeToUtf8Bytes(root);
        var after = NativeBackupReader.Read(output);
        VerifyOtherSections(before, after, before.Decks - 1,
            before.DeckRevisions - revisions);
        return output;
    }
}
