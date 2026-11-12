using System.Text.Json;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Appearance only; explicitly separate from native workspace, catalog and
/// legacy WebView settings. Corrupt preferences cannot block app startup.
/// </summary>
public static class NativeThemeSettings
{
    public static string DefaultPath => Path.Combine(
        NativeWorkspace.RootDirectory, "settings", "appearance.json");

    private sealed record Stored(int Schema, string Theme);
    private const int Schema = 1;

    public static string Load(string? path = null)
    {
        string file = path ?? DefaultPath;
        try
        {
            if (!File.Exists(file) || new FileInfo(file).Length is < 1 or > 8192)
                return NativeThemes.Default.Id;
            var data = JsonSerializer.Deserialize<Stored>(File.ReadAllBytes(file));
            return data is not null && data.Schema == Schema &&
                NativeThemes.Valid(data.Theme)
                ? NativeThemes.Find(data.Theme)!.Id : NativeThemes.Default.Id;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or JsonException or NotSupportedException or ArgumentException)
        {
            return NativeThemes.Default.Id;
        }
    }

    public static void Save(string theme, string? path = null)
    {
        var match = NativeThemes.Find(theme) ??
            throw new InvalidDataException("Unrecognized Arcane Vault theme.");
        string file = Path.GetFullPath(path ?? DefaultPath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            new Stored(Schema, match.Id));
        string stage = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(stage, bytes);
            File.Move(stage, file, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(stage)) File.Delete(stage); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
