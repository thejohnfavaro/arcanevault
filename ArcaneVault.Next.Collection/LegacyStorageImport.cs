using System.Text;
using System.Text.Json;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Transform a trustworthy snapshot of the *legacy WebView2 origin's*
/// localStorage into a full schema-2 backup IN MEMORY. This does not load
/// the old app or permit a fallback to bundled/default collection cards.
/// </summary>
public static class LegacyStorageImport
{
    private const int MaximumStorageKeys = 512;
    private const long MaximumTotalChars = 32_000_000;

    public static byte[] BuildFullBackup(IReadOnlyDictionary<string, string?> storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        if (storage.Count > MaximumStorageKeys ||
            storage.Sum(p => (long)p.Key.Length + (p.Value?.Length ?? 0)) > MaximumTotalChars)
            throw new InvalidDataException("Legacy WebView storage exceeds safe import size.");

        // Fail closed when real physical/Arena source keys are absent. Silent
        // fallback to the bundled demo list would be disastrous.
        JsonElement Parse(string key, JsonValueKind expected, bool mandatory)
        {
            if (!storage.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                if (mandatory)
                    throw new InvalidDataException(
                        "Installed Windows profile is missing required ownership data: " + key +
                        ". No native collection was created.");
                return default;
            }

            try
            {
                using var json = JsonDocument.Parse(value);
                if (json.RootElement.ValueKind != expected)
                    throw new InvalidDataException("Invalid legacy storage structure for " + key);
                return json.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Corrupt legacy WebView storage: " + key, ex);
            }
        }

        var arena = Parse("vault_cards", JsonValueKind.Array, mandatory: true);
        var physical = Parse("vault_physicalCollectionV1", JsonValueKind.Array, mandatory: true);
        // An actually empty Arena inventory is valid only when the key is
        // present. Do not synthesize ownership from installed catalog mirrors.
        var deckLibrary = Parse("vault_libraryV3", JsonValueKind.Array, mandatory: false);
        var favorite = Parse("vault_favorites", JsonValueKind.Array, mandatory: false);
        var deck = Parse("vault_deck", JsonValueKind.Object, mandatory: false);
        var studio = Parse("vault_studio", JsonValueKind.Object, mandatory: false);
        var artwork = Parse("vault_preferredPrintingArtworkV1", JsonValueKind.Object, mandatory: false);
        var wildcards = Parse("vault_wildcardsV3", JsonValueKind.Object, mandatory: false);
        var history = Parse("arcaneVault.collectionHistory.v1", JsonValueKind.Array, mandatory: false);

        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", 2);
            writer.WriteString("source", "installed-webview2-offline-snapshot");
            writer.WriteString("exported", DateTimeOffset.UtcNow);
            writer.WritePropertyName("cards"); arena.WriteTo(writer);
            writer.WritePropertyName("physicalCards"); physical.WriteTo(writer);
            void WriteOrDefault(string name, JsonElement element, JsonValueKind fallback)
            {
                writer.WritePropertyName(name);
                if (element.ValueKind != JsonValueKind.Undefined)
                {
                    element.WriteTo(writer);
                }
                else if (fallback == JsonValueKind.Array)
                {
                    writer.WriteStartArray(); writer.WriteEndArray();
                }
                else if (fallback == JsonValueKind.Object)
                {
                    writer.WriteStartObject(); writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNullValue();
                }
            }
            WriteOrDefault("deckLibrary", deckLibrary, JsonValueKind.Array);
            WriteOrDefault("favorites", favorite, JsonValueKind.Array);
            WriteOrDefault("deck", deck, JsonValueKind.Null);
            WriteOrDefault("studio", studio, JsonValueKind.Object);
            WriteOrDefault("preferredArtwork", artwork, JsonValueKind.Object);
            WriteOrDefault("wildcards", wildcards, JsonValueKind.Object);
            WriteOrDefault("collectionHistory", history, JsonValueKind.Array);
            // Preserve all other legacy origin keys verbatim for future data
            // parity/recovery, without interpreting or double-counting them.
            writer.WritePropertyName("legacyStorage");
            writer.WriteStartObject();
            foreach (var pair in storage.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (pair.Key.Length == 0 || pair.Key.Length > 512)
                    throw new InvalidDataException("Invalid browser storage key.");
                writer.WriteString(pair.Key, pair.Value);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return output.ToArray();
    }
}

/// <summary>
/// Copy ONLY the old profile's Local Storage LevelDB files into a new scratch
/// WebView2 profile. The live legacy database is never opened in write mode.
/// Unsupported/missing layouts fail instead of pretending the user has 0 cards.
/// </summary>
public static class LegacyProfileSnapshot
{
    public static string LiveProfilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ArcaneVault", "WebView2");

    public static string CopyToTemporaryProfile(string sourceProfileRoot, string scratchParent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceProfileRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(scratchParent);

        string source = Path.GetFullPath(sourceProfileRoot);
        string scratch = Path.GetFullPath(scratchParent);
        if (scratch.Equals(source, StringComparison.OrdinalIgnoreCase) ||
            scratch.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith(scratch.TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Migration scratch must be separate from the installed profile.");

        var localStorage = Path.Combine(source, "Default", "Local Storage", "leveldb");
        if (!Directory.Exists(localStorage) ||
            !File.Exists(Path.Combine(localStorage, "CURRENT")))
            throw new InvalidDataException(
                "Cannot locate the original WebView2 Local Storage database. No import was attempted.");

        if (Directory.Exists(scratch) && Directory.EnumerateFileSystemEntries(scratch).Any())
            throw new InvalidDataException("Migration scratch profile must be new and empty.");

        var dest = Path.Combine(scratch, "Default", "Local Storage", "leveldb");
        Directory.CreateDirectory(dest);
        var sourceFiles = Directory.GetFiles(localStorage, "*", SearchOption.TopDirectoryOnly)
            .Where(p => !Path.GetFileName(p).Equals("LOCK", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (!sourceFiles.Any(p => Path.GetExtension(p).Equals(".ldb", StringComparison.OrdinalIgnoreCase) ||
                                  Path.GetExtension(p).Equals(".log", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Legacy Local Storage has no usable data records.");

        long copiedBytes = 0;
        foreach (var file in sourceFiles)
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Refusing symbolic link in legacy browser profile.");
            var info = new FileInfo(file);
            copiedBytes = checked(copiedBytes + info.Length);
            if (copiedBytes > 128L * 1024 * 1024)
                throw new InvalidDataException("Legacy Local Storage exceeds the safe snapshot size.");

            var dst = Path.Combine(dest, Path.GetFileName(file));
            var srcHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file));
            File.Copy(file, dst, overwrite: false);
            var copyHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(dst));
            var recheck = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file));
            if (!srcHash.SequenceEqual(copyHash) || !srcHash.SequenceEqual(recheck))
                throw new InvalidDataException(
                    "Legacy browser storage changed while migrating. Close the old app and retry.");
        }
        return scratch;
    }
}
