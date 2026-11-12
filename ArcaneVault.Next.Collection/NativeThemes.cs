namespace ArcaneVault.Next.Collection;

/// <summary>
/// Native palette definitions ported from the legacy WebView theme CSS.
/// These alter presentation brushes only. No ownership, Scryfall catalog,
/// deck contents or preferred card artwork is modified.
/// </summary>
public static class NativeThemes
{
    public sealed record Palette(
        string Id, string Label, string Background, string Sidebar,
        string Panel, string PanelRaised, string Line, string Text,
        string Muted, string Accent, string AccentMuted, string AccentSurface,
        string AccentBorder, string Teal, string Good, string Warning)
    {
        public IReadOnlyDictionary<string, string> Brushes => new Dictionary<string, string>
        {
            ["VaultBackground"] = Background, ["VaultSidebar"] = Sidebar,
            ["VaultPanel"] = Panel, ["VaultPanelRaised"] = PanelRaised,
            ["VaultLine"] = Line, ["VaultText"] = Text,
            ["VaultMuted"] = Muted, ["VaultAccent"] = Accent,
            ["VaultAccentMuted"] = AccentMuted,
            ["VaultAccentSurface"] = AccentSurface,
            ["VaultAccentBorder"] = AccentBorder,
            ["VaultTeal"] = Teal, ["VaultGood"] = Good,
            ["VaultWarning"] = Warning
        };

        // Match the old WebView mana-identity gradient rather than presenting
        // an entire guild/wedge as a single blended accent color. This also
        // powers the theme picker's honest, per-color preview.
        public IReadOnlyList<string> PreviewColors
        {
            get
            {
                if (!Id.StartsWith("mtg-", StringComparison.Ordinal))
                    return new[] { Background, Accent, Teal };
                const string symbols = "WUBRG";
                var mana = new Dictionary<char, string>
                {
                    ['W'] = "#EDCD82", ['U'] = "#4B9DEC",
                    ['B'] = "#79838C", ['R'] = "#F76A65",
                    ['G'] = "#63CB90"
                };
                string identity = Id["mtg-".Length..];
                if (identity == "C") return new[] { "#BAC3D3" };
                return identity.Where(x => symbols.Contains(x))
                    .Select(x => mana[x]).ToArray();
            }
        }

        public string Description => Id switch
        {
            "noir" => "Dark charcoal with lavender accents",
            "light" => "Light panels with violet accents",
            "emerald" => "Deep forest-green panels with mint accents",
            "ember" => "Warm charcoal, copper and orange accents",
            "ocean" => "Midnight-blue panels with cyan accents",
            "mtg-B" => "Black mana-color: obsidian and charcoal with ash-silver accents — not purple",
            _ => Id.StartsWith("mtg-", StringComparison.Ordinal)
                ? "Dark foundation with " +
                  (PreviewColors.Count > 1 ? "separate" : "one") +
                  " mana-color accent" + (PreviewColors.Count > 1 ? "s" : "")
                : "Arcane Vault theme"
        };
    }

    public static IReadOnlyList<Palette> All { get; } = new Palette[]
    {
        new("noir", "Arcane Noir", "#0D1017", "#121722", "#171C28", "#202737", "#343D51", "#F0F1F6", "#9DA9BD", "#B59BFC", "#27233E", "#27233E", "#343D51", "#7FCAE1", "#8BD3AC", "#E5BF79"),
        new("light", "Daylight Archive", "#f1f3f9", "#e4e8f3", "#ffffff", "#e5e9f5", "#cbd2e1", "#192537", "#566579", "#6d49bb", "#d9d1f7", "#d9d1f7", "#cbd2e1", "#047d95", "#167755", "#975400"),
        new("emerald", "Emerald Grove", "#081915", "#0b211b", "#102b24", "#1a3a30", "#31574a", "#e5f6ed", "#9cc4b2", "#7de0a4", "#1b4735", "#1b4735", "#31574a", "#9bdcc3", "#78e9ac", "#f1bf80"),
        new("ember", "Ember Forge", "#1a1010", "#241512", "#2b1b1b", "#412a28", "#62413d", "#fff0e7", "#d0b0a5", "#ffac69", "#523126", "#523126", "#62413d", "#f0d391", "#a8e1a4", "#ffd078"),
        new("ocean", "Deep Ocean", "#081522", "#0c2030", "#10283b", "#193b52", "#2c5571", "#e6f7ff", "#9fbdd0", "#70ccff", "#1c4564", "#1c4564", "#2c5571", "#88e7d8", "#91e2c7", "#f7cb83"),
        new("mtg-C", "Colorless (C)", "#171b23", "#13161d", "#262c37", "#313845", "#4e5766", "#f4f4f7", "#afb6c6", "#bac3d3", "#474d59", "#474d59", "#4e5766", "#a2cdd8", "#89dba6", "#f6bf7b"),
        new("mtg-W", "White (W)", "#1b1b1e", "#161619", "#2c2d2f", "#38393b", "#575957", "#f4f4f7", "#afb6c6", "#edcd82", "#565041", "#565041", "#575957", "#c3d3a4", "#89dba6", "#f6bf7b"),
        new("mtg-U", "Blue (U)", "#0f1825", "#0c141e", "#1b283a", "#233348", "#3a506a", "#f4f4f7", "#afb6c6", "#4b9dec", "#264261", "#264261", "#3a506a", "#5ab4e8", "#89dba6", "#f6bf7b"),
        new("mtg-B", "Black (B)", "#0C0E11", "#101317", "#1B1F24", "#272C32", "#434B53", "#f4f4f7", "#afb6c6", "#CDD4D8", "#333A41", "#333A41", "#434B53", "#AAB4BB", "#89dba6", "#f6bf7b"),
        new("mtg-R", "Red (R)", "#1c141c", "#171017", "#2c232c", "#392c37", "#594752", "#f4f4f7", "#afb6c6", "#f76a65", "#593339", "#593339", "#594752", "#ca9391", "#89dba6", "#f6bf7b"),
        new("mtg-G", "Green (G)", "#111b1f", "#0e1619", "#1e2d31", "#26393c", "#3e585a", "#f4f4f7", "#afb6c6", "#63cb90", "#2d5046", "#2d5046", "#3e585a", "#6ad2ad", "#89dba6", "#f6bf7b"),
        new("mtg-WU", "Azorius (WU)", "#151a21", "#11151b", "#232a34", "#2e3642", "#485461", "#f4f4f7", "#afb6c6", "#a6bbbf", "#3e4951", "#3e4951", "#485461", "#4b9dec", "#89dba6", "#f6bf7b"),
        new("mtg-WB", "Orzhov (WB)", "#131313", "#18191A", "#27292B", "#34373A", "#55595C", "#f4f4f7", "#afb6c6", "#DFD0B7", "#454542", "#454542", "#55595C", "#B8C0C3", "#89dba6", "#f6bf7b"),
        new("mtg-WR", "Boros (WR)", "#1b181d", "#161418", "#2c282e", "#393339", "#585055", "#f4f4f7", "#afb6c6", "#f0a585", "#58423d", "#58423d", "#585055", "#f76a65", "#89dba6", "#f6bf7b"),
        new("mtg-WG", "Selesnya (WG)", "#161b1e", "#121619", "#252d30", "#2f393c", "#4b5859", "#f4f4f7", "#afb6c6", "#b0cf97", "#425044", "#425044", "#4b5859", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-UB", "Dimir (UB)", "#0E141B", "#111923", "#1D2B36", "#283A4A", "#435565", "#f4f4f7", "#afb6c6", "#79AFDD", "#30475B", "#30475B", "#435565", "#A8B6C3", "#89dba6", "#f6bf7b"),
        new("mtg-UR", "Izzet (UR)", "#161620", "#12121a", "#242633", "#2e3040", "#494b5e", "#f4f4f7", "#afb6c6", "#aa91b2", "#403b4d", "#403b4d", "#494b5e", "#f76a65", "#89dba6", "#f6bf7b"),
        new("mtg-UG", "Simic (UG)", "#101a22", "#0d151c", "#1c2a35", "#253642", "#3c5462", "#f4f4f7", "#afb6c6", "#6bbac5", "#294853", "#294853", "#3c5462", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-BR", "Rakdos (BR)", "#171010", "#1C1413", "#2C1E1D", "#3B2927", "#604741", "#f4f4f7", "#afb6c6", "#EA8072", "#593530", "#593530", "#604741", "#B5A6A2", "#89dba6", "#f6bf7b"),
        new("mtg-BG", "Golgari (BG)", "#0E1510", "#131B15", "#1F2B23", "#2A3A2E", "#46564A", "#f4f4f7", "#afb6c6", "#85C493", "#324D37", "#324D37", "#46564A", "#AAB8AB", "#89dba6", "#f6bf7b"),
        new("mtg-RG", "Gruul (RG)", "#16181d", "#121418", "#25282e", "#30323a", "#4b4f56", "#f4f4f7", "#afb6c6", "#b5a48a", "#43413f", "#43413f", "#4b4f56", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-WUB", "Esper (WUB)", "#11171B", "#161D23", "#222C35", "#2F3D49", "#52606C", "#f4f4f7", "#afb6c6", "#AEC2D5", "#3C4B58", "#3C4B58", "#52606C", "#BBC3CA", "#89dba6", "#f6bf7b"),
        new("mtg-WUR", "Jeskai (WUR)", "#17181f", "#131419", "#262832", "#31333e", "#4e505c", "#f4f4f7", "#afb6c6", "#c0a5a7", "#474249", "#474249", "#4e505c", "#f76a65", "#89dba6", "#f6bf7b"),
        new("mtg-WUG", "Bant (WUG)", "#141a20", "#10151a", "#222b33", "#2b3740", "#45565f", "#f4f4f7", "#afb6c6", "#96c1b4", "#394b4d", "#394b4d", "#45565f", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-WBR", "Mardu (WBR)", "#181412", "#1E1916", "#2D2520", "#3F322B", "#604F45", "#f4f4f7", "#afb6c6", "#E0B08E", "#584338", "#584338", "#604F45", "#BEAF9E", "#89dba6", "#f6bf7b"),
        new("mtg-WBG", "Abzan (WBG)", "#131810", "#181D17", "#282E26", "#353E32", "#566051", "#f4f4f7", "#afb6c6", "#BECDA6", "#435144", "#435144", "#566051", "#B4C1AE", "#89dba6", "#f6bf7b"),
        new("mtg-WRG", "Naya (WRG)", "#18191d", "#141418", "#27292f", "#32353a", "#4f5256", "#f4f4f7", "#afb6c6", "#c7b28d", "#494640", "#494640", "#4f5256", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-UBR", "Grixis (UBR)", "#10151B", "#141B23", "#222B35", "#303A45", "#505A64", "#f4f4f7", "#afb6c6", "#90B2D0", "#394955", "#394955", "#505A64", "#A9B3BA", "#89dba6", "#f6bf7b"),
        new("mtg-UBG", "Sultai (UBG)", "#0D1718", "#122021", "#1D2D2C", "#2A3E3D", "#45605B", "#f4f4f7", "#afb6c6", "#86C2B8", "#33554D", "#33554D", "#45605B", "#A7BEC0", "#89dba6", "#f6bf7b"),
        new("mtg-URG", "Temur (URG)", "#141820", "#10141a", "#222832", "#2b333f", "#45505d", "#f4f4f7", "#afb6c6", "#98a4ab", "#39414a", "#39414a", "#45505d", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-BRG", "Jund (BRG)", "#15140F", "#1B1B15", "#29291F", "#37382B", "#545743", "#f4f4f7", "#afb6c6", "#B6C68B", "#444A33", "#444A33", "#545743", "#AEB9A5", "#89dba6", "#f6bf7b"),
        new("mtg-WUBR", "Yore-Tiller (WUBR)", "#151619", "#1B1D20", "#292D31", "#383E44", "#555E65", "#f4f4f7", "#afb6c6", "#C7BEB3", "#45494C", "#45494C", "#555E65", "#B9C1C7", "#89dba6", "#f6bf7b"),
        new("mtg-WUBG", "Witch-Maw (WUBG)", "#121818", "#19201F", "#28312F", "#35423F", "#53625D", "#f4f4f7", "#afb6c6", "#B6C9BD", "#40534B", "#40534B", "#53625D", "#AFBEC2", "#89dba6", "#f6bf7b"),
        new("mtg-WURG", "Ink-Treader (WURG)", "#16191f", "#121419", "#242932", "#2f343e", "#4a525c", "#f4f4f7", "#afb6c6", "#adb0a5", "#404549", "#404549", "#4a525c", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-WBRG", "Dune-Brood (WBRG)", "#161710", "#1C1F17", "#2B2E23", "#3A3F30", "#585D48", "#f4f4f7", "#afb6c6", "#D4C19F", "#4D5038", "#4D5038", "#585D48", "#B7C0A8", "#89dba6", "#f6bf7b"),
        new("mtg-UBRG", "Glint-Eye (UBRG)", "#11191A", "#172021", "#253032", "#344246", "#536266", "#f4f4f7", "#afb6c6", "#A1BBC1", "#3E5157", "#3E5157", "#536266", "#AEBFC2", "#89dba6", "#f6bf7b"),
        new("mtg-WUBRG", "Five Color (WUBRG)", "#141718", "#191D1F", "#272C30", "#353C41", "#555F64", "#f4f4f7", "#afb6c6", "#C4CED2", "#424B50", "#424B50", "#555F64", "#B8C3C9", "#89dba6", "#f6bf7b")
    };
    public static Palette Default => All[0];

    public static Palette? Find(string? id) =>
        All.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static bool Valid(string? id) => Find(id) is not null;
}
