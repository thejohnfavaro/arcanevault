using ArcaneVault.Next.Domain;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Arena inventory is deliberately name-only; a card name is not a verified
/// physical printing or a statement about current Arena format legality.
/// Every edit modifies one explicit row or an unambiguous name match, preserves
/// unknown schema-2 data and is committed by the native rollback pipeline.
/// </summary>
public static class NativeArenaEditing
{
    private static string ValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Enter an Arena card name.", nameof(name));
        string value = name.Trim();
        if (value.Length > 255 || value.Any(char.IsControl))
            throw new ArgumentException("Card names must be 1–255 characters and contain no control characters.", nameof(name));
        return value;
    }

    private static void ValidQuantity(int quantity, bool allowZero)
    {
        if (quantity > 99_999 || (allowZero ? quantity < 0 : quantity < 1))
            throw new ArgumentOutOfRangeException(nameof(quantity),
                allowZero ? "Choose 0–99,999 (zero removes the row)." : "Choose 1–99,999.");
    }

    private static JsonObject Parsed(byte[] backup, out NativeBackupView view,
        out JsonArray arena)
    {
        ArgumentNullException.ThrowIfNull(backup);
        view = NativeBackupReader.Read(backup);
        var root = JsonNode.Parse(backup) as JsonObject ??
            throw new InvalidDataException("Invalid native backup document.");
        arena = root["cards"] as JsonArray ??
            throw new InvalidDataException("Arena inventory section is missing.");
        if (arena.Count != view.ArenaCards.Count)
            throw new InvalidDataException("Arena source rows do not match verified backup.");
        return root;
    }

    private static byte[] Validated(JsonObject root, NativeBackupView previous,
        int oldRowCount, long expectedArenaCopies)
    {
        byte[] result = JsonSerializer.SerializeToUtf8Bytes(root);
        var current = NativeBackupReader.Read(result);
        if (current.ArenaCards.Count != oldRowCount ||
            current.ArenaCopies != expectedArenaCopies ||
            current.PhysicalCopies != previous.PhysicalCopies ||
            !current.PhysicalCards.SequenceEqual(previous.PhysicalCards) ||
            current.Decks != previous.Decks ||
            current.DeckRevisions != previous.DeckRevisions ||
            current.PreferredArtworks != previous.PreferredArtworks)
            throw new InvalidDataException("Arena edit would change unrelated collection sections.");
        return result;
    }

    public static byte[] AddCopies(byte[] backup, string cardName, int count)
    {
        string name = ValidName(cardName);
        ValidQuantity(count, allowZero: false);
        var root = Parsed(backup, out var view, out var arena);

        // Require an unambiguous existing name. Never silently combine
        // duplicate legacy entries that may contain distinct annotations.
        var matches = view.ArenaCards
            .Select((row, ordinal) => (row, ordinal))
            .Where(x => x.row.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length > 1)
            throw new InvalidDataException(
                "Multiple Arena rows already have that name. Select an individual row to edit.");

        int expectedRows = view.ArenaCards.Count;
        if (matches.Length == 1)
        {
            var row = matches[0];
            long sum = (long)row.row.Quantity + count;
            if (sum > 99_999)
                throw new InvalidDataException("This Arena row would exceed 99,999 copies.");
            ((JsonObject)arena[row.ordinal]!)["qty"] = (int)sum;
        }
        else
        {
            if (arena.Count >= 150_000)
                throw new InvalidDataException("Arena inventory row limit reached.");
            arena.Add(new JsonObject { ["name"] = name, ["qty"] = count });
            expectedRows++;
        }
        return Validated(root, view, expectedRows, checked(view.ArenaCopies + count));
    }

    /// <summary>
    /// Add a whole CSV/XLSX import as one validated arena-name-only transaction.
    /// Duplicate names in a file are consolidated case-insensitively; existing
    /// rows retain their original spelling and unknown annotations. Never infer
    /// an Arena printing or replace physical collection/decks/art.
    /// </summary>
    public static byte[] AddImport(byte[] backup,
        IReadOnlyList<NativeExcelCollectionImport.Row> imported)
    {
        ArgumentNullException.ThrowIfNull(imported);
        if (imported.Count == 0)
            throw new InvalidDataException("Arena import contains no rows.");
        var byName = new Dictionary<string, (string Name, long Quantity)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var item in imported)
        {
            string name = ValidName(item.Name);
            ValidQuantity(item.Quantity, allowZero: false);
            if (byName.TryGetValue(name, out var previous))
                byName[name] = (previous.Name, checked(previous.Quantity + item.Quantity));
            else
                byName.Add(name, (name, item.Quantity));
        }
        if (byName.Values.Any(x => x.Quantity > 99_999))
            throw new InvalidDataException("Arena import exceeds 99,999 copies for a card name.");

        var root = Parsed(backup, out var view, out var arena);
        var existing = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < view.ArenaCards.Count; i++)
        {
            if (!existing.TryAdd(view.ArenaCards[i].Name, i))
                if (byName.ContainsKey(view.ArenaCards[i].Name))
                    throw new InvalidDataException(
                        "Existing Arena rows contain duplicate names targeted by this import. Resolve them first.");
        }
        long added = 0;
        int expectedRows = view.ArenaCards.Count;
        foreach (var item in byName.Values)
        {
            added = checked(added + item.Quantity);
            if (existing.TryGetValue(item.Name, out int index))
            {
                long combined = view.ArenaCards[index].Quantity + item.Quantity;
                if (combined > 99_999)
                    throw new InvalidDataException(
                        $"Arena card '{item.Name}' would exceed 99,999 copies.");
                ((JsonObject)arena[index]!)["qty"] = (int)combined;
            }
            else
            {
                if (++expectedRows > 150_000)
                    throw new InvalidDataException("Arena inventory row limit reached.");
                arena.Add(new JsonObject
                {
                    ["name"] = item.Name,
                    ["qty"] = (int)item.Quantity
                });
            }
        }
        return Validated(root, view, expectedRows, checked(view.ArenaCopies + added));
    }

    public static byte[] EditRow(byte[] backup, int arenaOrdinal,
        NativeArenaRow expectedRow, string newCardName, int newQuantity)
    {
        ArgumentNullException.ThrowIfNull(expectedRow);
        string newName = ValidName(newCardName);
        ValidQuantity(newQuantity, allowZero: true);
        var root = Parsed(backup, out var view, out var arena);
        if (arenaOrdinal < 0 || arenaOrdinal >= arena.Count ||
            view.ArenaCards[arenaOrdinal] != expectedRow)
            throw new InvalidDataException("Selected Arena entry changed since it was displayed.");

        // Renaming to another existing Arena entry must be deliberate:
        // stop rather than merging/duplicating potentially different records.
        if (newQuantity > 0 &&
            !expectedRow.Name.Equals(newName, StringComparison.OrdinalIgnoreCase) &&
            view.ArenaCards.Select((row, idx) => (row, idx))
                .Any(x => x.idx != arenaOrdinal &&
                          x.row.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException(
                "Another Arena row already has that name. Edit its quantity or remove this row.");

        if (expectedRow.Name == newName && expectedRow.Quantity == newQuantity)
            throw new InvalidDataException("Arena row is unchanged. No recovery point was created.");

        int newCount = arena.Count;
        if (newQuantity == 0)
        {
            arena.RemoveAt(arenaOrdinal);
            newCount--;
        }
        else
        {
            if (arena[arenaOrdinal] is not JsonObject row)
                throw new InvalidDataException("Invalid selected Arena row.");
            row["name"] = newName;
            row["qty"] = newQuantity;
        }
        byte[] result = Validated(root, view, newCount,
            checked(view.ArenaCopies - expectedRow.Quantity + newQuantity));
        var after = NativeBackupReader.Read(result);
        for (int i = 0; i < view.ArenaCards.Count; i++)
        {
            if (i == arenaOrdinal) continue;
            int next = i > arenaOrdinal && newQuantity == 0 ? i - 1 : i;
            if (view.ArenaCards[i] != after.ArenaCards[next])
                throw new InvalidDataException("Arena edit changed an unrelated row.");
        }
        return result;
    }
}
