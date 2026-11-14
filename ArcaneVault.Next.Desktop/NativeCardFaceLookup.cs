using Microsoft.Data.Sqlite;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Read-only exact-edition face lookup. Never select the reverse image from a
/// different printing merely because it shares the card's name.
/// </summary>
internal static class NativeCardFaceLookup
{
    internal static string Alternate(string? catalogPath,string name,string frontUrl)
    {
        if (string.IsNullOrWhiteSpace(catalogPath) ||
            !File.Exists(catalogPath) ||
            !NativeCardArtwork.IsTrustedArtworkUrl(frontUrl)) return "";
        try
        {
            using var db = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource=catalogPath, Mode=SqliteOpenMode.ReadOnly, Pooling=false
            }.ToString());
            db.Open();
            using var cmd=db.CreateCommand();
            cmd.CommandText = """
                SELECT image_back FROM variants
                WHERE name=$name COLLATE NOCASE AND image=$front
                  AND image_back IS NOT NULL AND image_back!=''
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("$name",name);
            cmd.Parameters.AddWithValue("$front",frontUrl);
            var alternate=cmd.ExecuteScalar() as string;
            return NativeCardArtwork.IsTrustedArtworkUrl(alternate) ? alternate : "";
        }
        catch (SqliteException) { return ""; }
        catch (IOException) { return ""; }
        catch (UnauthorizedAccessException) { return ""; }
    }
}
