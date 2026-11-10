using System.Text.Json;

namespace ArcaneVault.Next.Domain;

/// <summary>
/// Read-only view of an explicitly selected, exported schema-2 Windows full backup.
/// This is not the active collection or a restore/migration operation.
/// </summary>
public sealed record NativePhysicalRow(
    string Name, int Quantity, string SetCode, string CollectorNumber,
    string Finish, string ScryfallId)
{
    public bool HasValidPrintId =>
        Guid.TryParse(ScryfallId, out var id) && id != Guid.Empty;

    public string PrintingStatus => HasValidPrintId
        ? "ID supplied (not catalog verified)"
        : string.IsNullOrWhiteSpace(ScryfallId)
            ? "Unresolved: missing printing ID"
            : "Unresolved: invalid printing ID";

    public string SetAndNumber =>
        string.IsNullOrWhiteSpace(SetCode) && string.IsNullOrWhiteSpace(CollectorNumber)
            ? "Unspecified"
            : $"{SetCode} #{CollectorNumber}".Trim();
}

public sealed record NativeArenaRow(string Name, int Quantity);

public sealed record NativeBackupView(
    IReadOnlyList<NativePhysicalRow> PhysicalCards,
    IReadOnlyList<NativeArenaRow> ArenaCards,
    int Decks,
    int DeckRevisions,
    int PreferredArtworks,
    long PhysicalCopies,
    long ArenaCopies,
    long CopiesWithUnverifiedIds,
    long CopiesWithMissingOrInvalidIds)
{
    public int DistinctPhysicalNames =>
        PhysicalCards.Select(c => c.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();
}

/// <summary>
/// No profile auto-detection, SQL writes, staging, network operations or
/// collection merging. Parsing errors fail closed and never change the export.
/// </summary>
public static class NativeBackupReader
{
    private const long MaxBytes = 100 * 1024 * 1024;
    private const int MaxRecords = 150_000;

    private static void Require(bool ok)
    {
        if (!ok) throw new InvalidDataException("The selected full backup has an invalid or missing field.");
    }

    private static JsonElement Required(JsonElement row, string key, JsonValueKind type)
    {
        if (row.ValueKind != JsonValueKind.Object ||
            !row.TryGetProperty(key, out var value) || value.ValueKind != type)
            throw new InvalidDataException("The full backup is missing a required field.");
        return value;
    }

    private static JsonElement RequiredArray(JsonElement root, string key)
    {
        var array = Required(root, key, JsonValueKind.Array);
        Require(array.GetArrayLength() <= MaxRecords);
        return array;
    }

    private static string Text(JsonElement row, string key, bool required = false)
    {
        if (!row.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            Require(!required);
            return "";
        }

        Require(value.ValueKind == JsonValueKind.String);
        string result = value.GetString() ?? "";
        Require(!required || !string.IsNullOrWhiteSpace(result));
        return result;
    }

    private static int Qty(JsonElement row, bool allowZero)
    {
        var v = Required(row, "qty", JsonValueKind.Number);
        Require(v.TryGetInt32(out int n) && n <= 99_999 &&
                (allowZero ? n >= 0 : n > 0));
        return n;
    }

    public static NativeBackupView Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var source = new FileInfo(path);
        Require(source.Exists && source.Length is > 0 and <= MaxBytes);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        var schema = Required(root, "schema", JsonValueKind.Number);
        Require(schema.TryGetInt32(out var version) && version == 2);
        // Full Windows backup only, never a lossy QR/USB portable snapshot.
        Require(!root.TryGetProperty("format", out var format) ||
                format.ValueKind != JsonValueKind.String ||
                format.GetString() != "arcane-vault-collection-sync");

        var arena = RequiredArray(root, "cards");
        var physical = RequiredArray(root, "physicalCards");
        var decks = RequiredArray(root, "deckLibrary");
        var artwork = Required(root, "preferredArtwork", JsonValueKind.Object);

        var arenaRows = new List<NativeArenaRow>(arena.GetArrayLength());
        var physicalRows = new List<NativePhysicalRow>(physical.GetArrayLength());
        long arenaCopies = 0, physicalCopies = 0;
        long withIds = 0, missingIds = 0;

        foreach (var sourceRow in arena.EnumerateArray())
        {
            Require(sourceRow.ValueKind == JsonValueKind.Object);
            string name = Text(sourceRow, "name", required: true);
            int count = Qty(sourceRow, allowZero: true);
            arenaRows.Add(new NativeArenaRow(name, count));
            arenaCopies = checked(arenaCopies + count);
        }

        foreach (var sourceRow in physical.EnumerateArray())
        {
            Require(sourceRow.ValueKind == JsonValueKind.Object);
            string name = Text(sourceRow, "name", required: true);
            int count = Qty(sourceRow, allowZero: false);
            var row = new NativePhysicalRow(name, count,
                Text(sourceRow, "set"), Text(sourceRow, "number"),
                Text(sourceRow, "finish"), Text(sourceRow, "scryfallId"));
            physicalRows.Add(row);
            physicalCopies = checked(physicalCopies + count);
            if (row.HasValidPrintId)
                withIds = checked(withIds + count);
            else
                missingIds = checked(missingIds + count);
        }

        int revisions = 0;
        foreach (var sourceDeck in decks.EnumerateArray())
        {
            Require(sourceDeck.ValueKind == JsonValueKind.Object);
            Text(sourceDeck, "name", required: true);
            RequiredArray(sourceDeck, "entries");
            if (sourceDeck.TryGetProperty("revisions", out var r))
            {
                Require(r.ValueKind == JsonValueKind.Array && r.GetArrayLength() <= MaxRecords);
                revisions = checked(revisions + r.GetArrayLength());
            }
        }

        int artworkCount = 0;
        foreach (var _ in artwork.EnumerateObject())
        {
            artworkCount++;
            Require(artworkCount <= MaxRecords);
        }

        Require(physicalCopies == checked(withIds + missingIds));
        return new NativeBackupView(physicalRows.AsReadOnly(), arenaRows.AsReadOnly(),
            decks.GetArrayLength(), revisions, artworkCount,
            physicalCopies, arenaCopies, withIds, missingIds);
    }
}
