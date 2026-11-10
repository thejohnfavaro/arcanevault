using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Local, read-only card-identity loader. Reuses the installed Scryfall cards
/// table, but deliberately does not read ownership mirrors or modify the DB.
/// It never makes network calls or stores an invented legality verdict.
/// </summary>
public static class CommanderCatalog
{
    public static EdhLabCommanderIndex OpenReadOnly(string catalogPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(catalogPath) || !File.Exists(catalogPath))
            throw new FileNotFoundException("Choose an existing Scryfall catalog.", catalogPath);

        using var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = catalogPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        db.Open();
        using (var metadata = db.CreateCommand())
        {
            metadata.CommandText = "PRAGMA table_info(cards)";
            using var reader = metadata.ExecuteReader();
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read()) columns.Add(reader.GetString(1));
            if (!new[] { "name", "type", "oracle", "colors", "legalities", "arena", "image" }
                .All(columns.Contains))
                throw new InvalidDataException(
                    "Scryfall catalog needs its completed cards sync before EDH Lab can browse.");
        }

        var rows = new List<EdhLabCommanderCard>();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT name,type,oracle,colors,legalities,arena,image FROM cards
            WHERE type LIKE '%Legendary%' COLLATE NOCASE
               OR oracle LIKE '%can be your commander%' COLLATE NOCASE
               OR oracle LIKE '%may be your commander%' COLLATE NOCASE
            ORDER BY name COLLATE NOCASE
            """;
        using var list = cmd.ExecuteReader();
        while (list.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (list.IsDBNull(0) || list.IsDBNull(1) || list.IsDBNull(4)) continue;
            IReadOnlyDictionary<string, string> legalities;
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(list.GetString(4));
                legalities = parsed is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                legalities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            var colors = list.IsDBNull(3) ? "" : list.GetString(3);
            try
            {
                // Unknown identities do not silently become colorless candidates.
                EdhLabCommanderRules.ColorBits(colors);
            }
            catch (ArgumentException) { continue; }

            rows.Add(new EdhLabCommanderCard(
                list.GetString(0),
                list.GetString(1),
                list.IsDBNull(2) ? "" : list.GetString(2),
                colors,
                !list.IsDBNull(5) && list.GetInt32(5) != 0,
                legalities,
                list.IsDBNull(6) ? "" : list.GetString(6)));
        }
        return new EdhLabCommanderIndex(rows);
    }
}
