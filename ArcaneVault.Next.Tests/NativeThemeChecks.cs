using ArcaneVault.Next.Collection;

internal static class NativeThemeChecks
{
    private static void Must(bool yes, string why)
    {
        if (!yes) throw new InvalidOperationException("Native themes: " + why);
    }

    public static void Run(string work)
    {
        var all = NativeThemes.All;
        Must(all.Count == 37 && all[0].Id == "noir" &&
             all.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 37,
             "Five original themes and 32 identity palettes must be present exactly once.");
        Must(all.Count(x => x.Id.StartsWith("mtg-", StringComparison.Ordinal)) == 32 &&
             all.Any(x => x.Id == "mtg-WUBRG") &&
             all.Any(x => x.Id == "ember") &&
             all.Any(x => x.Id == "light"),
             "Original mana-color and classic theme identifiers were lost.");
        foreach (var palette in all)
        {
            Must(palette.Brushes.Count == 14 &&
                 palette.Brushes.All(x => x.Key.StartsWith("Vault", StringComparison.Ordinal) &&
                    x.Value.Length == 7 && x.Value[0] == '#' &&
                    x.Value.Skip(1).All(Uri.IsHexDigit)),
                 "Every theme must provide valid full-color values for shared WPF brushes.");
        }
        string file = Path.Combine(work, "appearance-regression", "settings.json");
        Must(NativeThemeSettings.Load(file) == "noir",
             "Unconfigured theme must keep the existing default.");
        NativeThemeSettings.Save("mtg-BG", file);
        Must(NativeThemeSettings.Load(file) == "mtg-BG",
             "Theme choice must survive an app restart.");
        NativeThemeSettings.Save("LiGhT", file);
        Must(NativeThemeSettings.Load(file) == "light",
             "Theme persistence must canonicalize IDs.");
        File.WriteAllText(file, "{broken");
        Must(NativeThemeSettings.Load(file) == "noir",
             "Broken optional appearance settings must fall back without affecting startup.");
        bool refused = false;
        try { NativeThemeSettings.Save("not-a-theme", file); }
        catch (InvalidDataException) { refused = true; }
        Must(refused && NativeThemeSettings.Load(file) == "noir",
             "Invalid theme ID must not overwrite current preferences.");
        Must(!Directory.GetFiles(Path.GetDirectoryName(file)!, "*.tmp").Any(),
             "Theme persistence must clean up temporary files.");
        Console.WriteLine("PASS: native appearance (37 themes, persistence, fallback)");
    }
}
