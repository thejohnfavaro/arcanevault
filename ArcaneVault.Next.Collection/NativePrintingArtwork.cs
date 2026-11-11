using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Offline Scryfall printing chooser and the unchanged schema-2 legacy
/// preferredArtwork record format. All catalog lookups are read-only.
/// </summary>
public static class NativePrintingArtwork
{
    public sealed record Printing(
        Guid Id, string Name, string SetCode, string CollectorNumber,
        string SetName, string ImageUrl, bool Paper, string Released)
    {
        public string Title => $"{SetCode.ToUpperInvariant()} #{CollectorNumber}";
        public string Subtitle => $"{SetName} · {(Paper ? "Paper" : "Digital-only")} · {Released}";
    }

    public sealed record Preferred(Guid Id, string Image, string SetCode, string CollectorNumber);
    public sealed record Gallery(IReadOnlyList<Printing> Printings, bool Truncated);

    public static bool TrustedImage(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.Equals("cards.scryfall.io", StringComparison.OrdinalIgnoreCase) &&
        uri.Port == 443 && uri.UserInfo.Length == 0 &&
        uri.AbsolutePath.Length < 2048;

    private static SqliteConnection Open(string path)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        db.Open();
        return db;
    }

    /// <summary>
    /// Bounded printing list (newest first) for one exact card name.
    /// A missing/older catalog can browse textual Card Details, but must not
    /// pretend to supply a verified printing for artwork persistence.
    /// </summary>
    public static Gallery Read(string catalogPath, string name, int limit = 300,
        CancellationToken cancellation = default, bool arenaOnly = false)
    {
        if (string.IsNullOrWhiteSpace(catalogPath) || string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException("Select a card and load an official native Scryfall catalog.");
        if (limit is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(limit));
        using var db = Open(catalogPath);
        using var schema = db.CreateCommand();
        schema.CommandText = "PRAGMA table_info(variants)";
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var rows = schema.ExecuteReader())
            while (rows.Read()) columns.Add(rows.GetString(1));
        if (!new[] { "print_id", "name", "setcode", "collector", "setname", "image",
                     "games", "digital", "release" }.All(columns.Contains))
            throw new InvalidDataException(
                "This catalog lacks printing artwork metadata. Run Full Scryfall Sync first.");

        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT print_id,name,setcode,collector,setname,image,games,digital,release
            FROM variants WHERE name=$name COLLATE NOCASE
            """ + (arenaOnly
                ? " AND instr(',' || lower(replace(games,' ','')) || ',', ',arena,') > 0"
                : "") + (arenaOnly
                // In the Arena client, ownership is one card name; paper
                // collector-only alternate art does not imply a purchasable
                // Arena card style. Show the base shared edition per set,
                // plus independently cataloged digital-specific editions.
                ? "\nORDER BY digital DESC,release DESC,setcode COLLATE NOCASE," +
                  " CASE WHEN collector GLOB '[0-9]*' THEN 0 ELSE 1 END," +
                  " CAST(collector AS INTEGER),collector COLLATE NOCASE"
                : "\nORDER BY release DESC,setcode COLLATE NOCASE,collector") +
                (arenaOnly ? "" : "\nLIMIT $limit");
        cmd.Parameters.AddWithValue("$name", name.Trim());
        cmd.Parameters.AddWithValue("$limit", limit + 1);
        var result = new List<Printing>(limit);
        var sharedPerSet = arenaOnly
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : null;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            cancellation.ThrowIfCancellationRequested();
            if (reader.IsDBNull(0) || !Guid.TryParse(reader.GetString(0), out Guid id) ||
                id == Guid.Empty) continue;
            // The Scryfall games flag establishes card availability, NOT
            // whether each paper showcase printing is an Arena cosmetic. A
            // conservative gallery includes one regular representative per
            // set. Explicitly digital printings remain independently listed.
            if (sharedPerSet is not null && reader.GetInt64(7) == 0 &&
                !sharedPerSet.Add(reader.GetString(2))) continue;
            if (result.Count == limit) return new Gallery(result.AsReadOnly(), true);
            bool paper = reader.GetInt64(7) == 0 &&
                reader.GetString(6).Split(',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(s => s.Equals("paper", StringComparison.OrdinalIgnoreCase));
            result.Add(new Printing(
                id, reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), paper, reader.GetString(8)));
        }
        return new Gallery(result.AsReadOnly(), false);
    }

    public static Printing Require(string catalogPath, string name, Guid id)
    {
        if (id == Guid.Empty)
            throw new InvalidDataException("Choose a valid Scryfall printing.");
        // Query exactly this ID instead of assuming a 300-item gallery covers it.
        using var db = Open(catalogPath);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT print_id,name,setcode,collector,setname,image,games,digital,release
            FROM variants WHERE print_id=$id COLLATE NOCASE LIMIT 2
            """;
        cmd.Parameters.AddWithValue("$id", id.ToString("D"));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            throw new InvalidDataException("Selected printing is absent from the current Scryfall catalog.");
        if (reader.IsDBNull(0) || !Guid.TryParse(reader.GetString(0), out var actual) ||
            actual != id || !string.Equals(reader.GetString(1), name.Trim(),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Printing ID conflicts with the selected card name.");
        string image = reader.GetString(5);
        if (!TrustedImage(image))
            throw new InvalidDataException("The selected printing has no supported official artwork URL.");
        bool paper = reader.GetInt64(7) == 0 &&
            reader.GetString(6).Split(',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(s => s.Equals("paper", StringComparison.OrdinalIgnoreCase));
        var selected = new Printing(actual, reader.GetString(1), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), image, paper, reader.GetString(8));
        if (reader.Read())
            throw new InvalidDataException("Ambiguous duplicate printing ID in the current catalog.");
        return selected;
    }

    private static JsonObject Root(byte[] backup)
    {
        _ = NativeBackupReader.Read(backup); // validate schema and original quantities
        return JsonNode.Parse(backup) as JsonObject ??
            throw new InvalidDataException("Native collection backup is not an object.");
    }

    private static (JsonObject Artwork, string? Key) Locate(JsonObject root, string name)
    {
        var artwork = root["preferredArtwork"] as JsonObject ??
            throw new InvalidDataException("Native collection lacks preferredArtwork.");
        var keys = artwork.Select(k => k.Key)
            .Where(k => string.Equals(k.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (keys.Length > 1)
            throw new InvalidDataException(
                "Duplicate case-insensitive preferred artwork keys must be repaired before editing.");
        return (artwork, keys.SingleOrDefault());
    }

    public static Preferred? ReadPreferred(byte[] backup, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var (art, key) = Locate(Root(backup), name);
        if (key is null || art[key] is not JsonObject value ||
            !Guid.TryParse(value["id"]?.GetValue<string>(), out Guid id) || id == Guid.Empty)
            return null;
        string image = value["image"]?.GetValue<string>() ?? "";
        if (!TrustedImage(image)) return null;
        return new Preferred(id, image, value["set"]?.GetValue<string>() ?? "",
            value["collectorNumber"]?.GetValue<string>() ?? "");
    }

    /// <summary>
    /// Pure schema-2 transform: changes only ONE named preferredArtwork entry
    /// in the existing legacy-compatible {id,image,set,collectorNumber} shape.
    /// All opaque fields inside the chosen entry and in the backup are kept.
    /// </summary>
    public static byte[] Choose(byte[] backup, Printing selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if (selected.Id == Guid.Empty || string.IsNullOrWhiteSpace(selected.Name) ||
            !TrustedImage(selected.ImageUrl))
            throw new InvalidDataException("Select a valid official Scryfall artwork printing.");
        var before = NativeBackupReader.Read(backup);
        var root = Root(backup);
        var (art, key) = Locate(root, selected.Name);
        JsonObject record;
        if (key is null)
        {
            key = selected.Name.Trim().ToLowerInvariant();
            record = new JsonObject();
            art[key] = record;
        }
        else
        {
            record = art[key] as JsonObject ??
                throw new InvalidDataException("Existing preferred artwork has an unsupported record format.");
        }
        if (string.Equals(record["id"]?.ToString(), selected.Id.ToString("D"),
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record["image"]?.ToString(), selected.ImageUrl,
                StringComparison.Ordinal) &&
            string.Equals(record["set"]?.ToString(), selected.SetCode,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record["collectorNumber"]?.ToString(), selected.CollectorNumber,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("This printing is already the preferred artwork.");
        record["id"] = selected.Id.ToString("D");
        record["image"] = selected.ImageUrl;
        record["set"] = selected.SetCode;
        record["collectorNumber"] = selected.CollectorNumber;
        byte[] changed = JsonSerializer.SerializeToUtf8Bytes(root);
        var after = NativeBackupReader.Read(changed);
        CheckConserved(before, after);
        if (after.PreferredArtworks != before.PreferredArtworks + (key is not null &&
            !backupContains(backup, key) ? 1 : 0))
            throw new InvalidDataException("Preferred artwork record count changed unexpectedly.");
        return changed;
    }

    private static bool backupContains(byte[] backup, string key)
    {
        using var doc = JsonDocument.Parse(backup);
        return doc.RootElement.GetProperty("preferredArtwork").TryGetProperty(key, out _);
    }

    public static byte[] Reset(byte[] backup, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException("Choose a valid card name.");
        var before = NativeBackupReader.Read(backup);
        var root = Root(backup);
        var (art, key) = Locate(root, name);
        if (key is null)
            throw new InvalidDataException("This card has no saved artwork preference.");
        art.Remove(key);
        byte[] changed = JsonSerializer.SerializeToUtf8Bytes(root);
        var after = NativeBackupReader.Read(changed);
        CheckConserved(before, after);
        if (after.PreferredArtworks != before.PreferredArtworks - 1)
            throw new InvalidDataException("Reset affected unrelated preferred artwork.");
        return changed;
    }

    private static void CheckConserved(NativeBackupView before, NativeBackupView after)
    {
        if (!before.PhysicalCards.SequenceEqual(after.PhysicalCards) ||
            !before.ArenaCards.SequenceEqual(after.ArenaCards) ||
            before.PhysicalCopies != after.PhysicalCopies ||
            before.ArenaCopies != after.ArenaCopies ||
            before.Decks != after.Decks ||
            before.DeckRevisions != after.DeckRevisions)
            throw new InvalidDataException(
                "Artwork preference update would affect collection ownership or saved decks.");
    }
}
