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
        new("mtg-B", "Black (B)", "#161523", "#12111d", "#242337", "#2f2d45", "#4a4866", "#f4f4f7", "#afb6c6", "#a66fd4", "#41345a", "#41345a", "#4a4866", "#9596d9", "#89dba6", "#f6bf7b"),
        new("mtg-R", "Red (R)", "#1c141c", "#171017", "#2c232c", "#392c37", "#594752", "#f4f4f7", "#afb6c6", "#f76a65", "#593339", "#593339", "#594752", "#ca9391", "#89dba6", "#f6bf7b"),
        new("mtg-G", "Green (G)", "#111b1f", "#0e1619", "#1e2d31", "#26393c", "#3e585a", "#f4f4f7", "#afb6c6", "#63cb90", "#2d5046", "#2d5046", "#3e585a", "#6ad2ad", "#89dba6", "#f6bf7b"),
        new("mtg-WU", "Azorius (WU)", "#151a21", "#11151b", "#232a34", "#2e3642", "#485461", "#f4f4f7", "#afb6c6", "#a6bbbf", "#3e4951", "#3e4951", "#485461", "#4b9dec", "#89dba6", "#f6bf7b"),
        new("mtg-WB", "Orzhov (WB)", "#181820", "#14141a", "#282833", "#333340", "#51505f", "#f4f4f7", "#afb6c6", "#cea7b4", "#4c424d", "#4c424d", "#51505f", "#a66fd4", "#89dba6", "#f6bf7b"),
        new("mtg-WR", "Boros (WR)", "#1b181d", "#161418", "#2c282e", "#393339", "#585055", "#f4f4f7", "#afb6c6", "#f0a585", "#58423d", "#58423d", "#585055", "#f76a65", "#89dba6", "#f6bf7b"),
        new("mtg-WG", "Selesnya (WG)", "#161b1e", "#121619", "#252d30", "#2f393c", "#4b5859", "#f4f4f7", "#afb6c6", "#b0cf97", "#425044", "#425044", "#4b5859", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-UB", "Dimir (UB)", "#131624", "#10121e", "#202639", "#293047", "#424c68", "#f4f4f7", "#afb6c6", "#8792e2", "#333b5e", "#333b5e", "#424c68", "#a66fd4", "#89dba6", "#f6bf7b"),
        new("mtg-UR", "Izzet (UR)", "#161620", "#12121a", "#242633", "#2e3040", "#494b5e", "#f4f4f7", "#afb6c6", "#aa91b2", "#403b4d", "#403b4d", "#494b5e", "#f76a65", "#89dba6", "#f6bf7b"),
        new("mtg-UG", "Simic (UG)", "#101a22", "#0d151c", "#1c2a35", "#253642", "#3c5462", "#f4f4f7", "#afb6c6", "#6bbac5", "#294853", "#294853", "#3c5462", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-BR", "Rakdos (BR)", "#19151f", "#141119", "#282332", "#342c3e", "#51475c", "#f4f4f7", "#afb6c6", "#d17ca7", "#4d3349", "#4d3349", "#51475c", "#f76a65", "#89dba6", "#f6bf7b"),
        new("mtg-BG", "Golgari (BG)", "#131821", "#10141b", "#212834", "#2a3341", "#445060", "#f4f4f7", "#afb6c6", "#91a6ba", "#374250", "#374250", "#445060", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-RG", "Gruul (RG)", "#16181d", "#121418", "#25282e", "#30323a", "#4b4f56", "#f4f4f7", "#afb6c6", "#b5a48a", "#43413f", "#43413f", "#4b4f56", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-WUB", "Esper (WUB)", "#151822", "#11141c", "#242836", "#2e3343", "#495063", "#f4f4f7", "#afb6c6", "#a9a7c7", "#3f4255", "#3f4255", "#495063", "#a66fd4", "#89dba6", "#f6bf7b"),
        new("mtg-WUR", "Jeskai (WUR)", "#17181f", "#131419", "#262832", "#31333e", "#4e505c", "#f4f4f7", "#afb6c6", "#c0a5a7", "#474249", "#474249", "#4e505c", "#f76a65", "#89dba6", "#f6bf7b"),
        new("mtg-WUG", "Bant (WUG)", "#141a20", "#10151a", "#222b33", "#2b3740", "#45565f", "#f4f4f7", "#afb6c6", "#96c1b4", "#394b4d", "#394b4d", "#45565f", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-WBR", "Mardu (WBR)", "#19171f", "#141319", "#2a2631", "#35313d", "#534d5b", "#f4f4f7", "#afb6c6", "#db98a1", "#503d47", "#503d47", "#534d5b", "#f76a65", "#89dba6", "#f6bf7b"),
        new("mtg-WBG", "Abzan (WBG)", "#161920", "#12141a", "#242a32", "#2f353f", "#4a535d", "#f4f4f7", "#afb6c6", "#b0b4ad", "#41474b", "#41474b", "#4a535d", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-WRG", "Naya (WRG)", "#18191d", "#141418", "#27292f", "#32353a", "#4f5256", "#f4f4f7", "#afb6c6", "#c7b28d", "#494640", "#494640", "#4f5256", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-UBR", "Grixis (UBR)", "#161621", "#12121b", "#242534", "#2e2f42", "#4a4a61", "#f4f4f7", "#afb6c6", "#ac8bbf", "#403851", "#403851", "#4a4a61", "#f76a65", "#89dba6", "#f6bf7b"),
        new("mtg-UBG", "Sultai (UBG)", "#121822", "#0f141c", "#1f2836", "#283343", "#415063", "#f4f4f7", "#afb6c6", "#81a6cb", "#314255", "#314255", "#415063", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-URG", "Temur (URG)", "#141820", "#10141a", "#222832", "#2b333f", "#45505d", "#f4f4f7", "#afb6c6", "#98a4ab", "#39414a", "#39414a", "#45505d", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-BRG", "Jund (BRG)", "#16171f", "#121319", "#252631", "#2f313e", "#4b4d5b", "#f4f4f7", "#afb6c6", "#b397a4", "#423c48", "#423c48", "#4b4d5b", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-WUBR", "Yore-Tiller (WUBR)", "#171720", "#13131a", "#262733", "#313140", "#4d4e5f", "#f4f4f7", "#afb6c6", "#bc9cb4", "#453e4d", "#453e4d", "#4d4e5f", "#f76a65", "#89dba6", "#f6bf7b"),
        new("mtg-WUBG", "Witch-Maw (WUBG)", "#141921", "#10141b", "#222934", "#2c3441", "#465260", "#f4f4f7", "#afb6c6", "#9cb0bc", "#3a4550", "#3a4550", "#465260", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-WURG", "Ink-Treader (WURG)", "#16191f", "#121419", "#242932", "#2f343e", "#4a525c", "#f4f4f7", "#afb6c6", "#adb0a5", "#404549", "#404549", "#4a525c", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-WBRG", "Dune-Brood (WBRG)", "#17181f", "#131419", "#262831", "#32333d", "#4e505a", "#f4f4f7", "#afb6c6", "#c1a5a0", "#474247", "#474247", "#4e505a", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-UBRG", "Glint-Eye (UBRG)", "#151721", "#11131b", "#222734", "#2c3140", "#474e5f", "#f4f4f7", "#afb6c6", "#9e9bb6", "#3b3e4f", "#3b3e4f", "#474e5f", "#63cb90", "#89dba6", "#f6bf7b"),
        new("mtg-WUBRG", "Five Color (WUBRG)", "#161820", "#12141a", "#242833", "#2f333f", "#4a505e", "#f4f4f7", "#afb6c6", "#aea5af", "#40424c", "#40424c", "#4a505e", "#63cb90", "#89dba6", "#f6bf7b")
    };
    public static Palette Default => All[0];

    public static Palette? Find(string? id) =>
        All.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static bool Valid(string? id) => Find(id) is not null;
}
