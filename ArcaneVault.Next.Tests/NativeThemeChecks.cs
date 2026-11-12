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
        // Identity names must actually expose their constituent mana colors.
        // The old WebView used multi-stop mana gradients; a blended accent
        // alone makes Boros/Orzhov/Esper/Grixis nearly indistinguishable.
        var mana = new Dictionary<char, string>
        {
            ['W'] = "#EDCD82", ['U'] = "#4B9DEC", ['B'] = "#79838C",
            ['R'] = "#F76A65", ['G'] = "#63CB90"
        };
        foreach (var theme in all.Where(x => x.Id.StartsWith("mtg-", StringComparison.Ordinal)))
        {
            string identity = theme.Id["mtg-".Length..];
            var expected = identity == "C"
                ? new[] { "#BAC3D3" }
                : identity.Select(symbol => mana[symbol]).ToArray();
            Must(theme.PreviewColors.SequenceEqual(expected),
                 "Theme identity must show its named mana colors in WUBRG order: " + theme.Id);
            Must(theme.Description.Contains("mana-color", StringComparison.Ordinal),
                 "Mana identity theme needs an accurate UI explanation: " + theme.Id);
        }
        var blackTheme = NativeThemes.Find("mtg-B")!;
        Must(blackTheme.Accent == "#CDD4D8" &&
             blackTheme.Background == "#0C0E11" &&
             blackTheme.Description.Contains("not purple", StringComparison.Ordinal) &&
             blackTheme.PreviewColors.Single() == "#79838C",
             "Black mana must be visibly neutral charcoal and ash, not a violet accent.");
        string[] blackIdentities = all.Where(x => x.Id.StartsWith("mtg-", StringComparison.Ordinal) &&
                x.Id["mtg-".Length..].Contains('B'))
            .Select(x => x.Id).ToArray();
        Must(blackIdentities.Length == 16,
             "Exactly sixteen mono/multicolor mana identities include black.");
        foreach (string identity in blackIdentities)
        {
            var theme = NativeThemes.Find(identity)!;
            Must(theme.PreviewColors.Contains("#79838C") &&
                 !theme.PreviewColors.Contains("#A66FD4") &&
                 theme.Brushes.Values.All(x => !x.Equals("#A66FD4",
                     StringComparison.OrdinalIgnoreCase)),
                 "Black mana identities must not carry the old purple color: " + identity);
        }
        Must(NativeThemes.Find("mtg-UB")!.Accent == "#79AFDD" &&
             NativeThemes.Find("mtg-BR")!.Accent == "#EA8072" &&
             NativeThemes.Find("mtg-BG")!.Accent == "#85C493" &&
             NativeThemes.Find("mtg-WB")!.Accent == "#DFD0B7",
             "Black dual-color themes must retain recognizable blue/red/green/ivory accents.");

        foreach (string classic in new[] { "noir", "light", "emerald", "ember", "ocean" })
        {
            var theme = NativeThemes.Find(classic)!;
            Must(theme.PreviewColors.Count == 3 &&
                 theme.PreviewColors[1] == theme.Accent &&
                 !string.IsNullOrWhiteSpace(theme.Description),
                 "Classic theme needs representative swatches and a description: " + classic);
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
