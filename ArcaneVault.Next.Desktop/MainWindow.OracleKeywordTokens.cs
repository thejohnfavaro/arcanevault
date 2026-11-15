using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcaneVault.Next.Desktop;

public partial class MainWindow
{
    // Presentation-only tokens. CatalogSnapshot still receives comma-separated
    // Oracle phrases; these controls never edit inventory or card metadata.
    private readonly List<string> oracleKeywordTokens = new();
    private bool oracleKeywordUiChanging;
    private static readonly string[] OracleKeywordDictionary = (
        "Adapt|Addendum|Affinity|Afterlife|Aftermath|Alliance|Amass|Amplify|Annihilator|" +
        "Backup|Banding|Bargain|Battalion|Bestow|Blitz|Bloodthirst|Boast|Bushido|Buyback|" +
        "Cascade|Casualty|Champion|Changeling|Channel|Cipher|Cleave|Collect evidence|" +
        "Compleated|Connive|Conspire|Convoke|Corrupted|Craft|Crew|Cycling|Dash|Daybound|" +
        "Deathtouch|Defender|Delirium|Demonstrate|Descend|Devour|Discover|Disturb|Domain|" +
        "Double strike|Dredge|Echo|Embalm|Emerge|Encore|Enlist|Entwine|Epic|Escalate|" +
        "Escape|Evoke|Evolve|Exalted|Exploit|Explore|Extort|Fabricate|Fading|Fear|" +
        "Ferocious|First strike|Flanking|Flash|Flashback|Flying|Forecast|Foretell|" +
        "Formidable|Fortify|Frenzy|Fuse|Goad|Gravestorm|Haste|Haunt|Hellbent|Heroic|" +
        "Hexproof|Hideaway|Horsemanship|Improvise|Infect|Inspired|Investigate|Jump-start|" +
        "Kicker|Landfall|Landwalk|Learn|Lifelink|Living weapon|Madness|Manifest|Menace|" +
        "Mentor|Miracle|Modular|Monstrosity|Morph|Mutate|Myriad|Ninjutsu|Offering|" +
        "Outlast|Overload|Persist|Phasing|Plot|Poisonous|Populate|Proliferate|Protection|" +
        "Prowess|Prowl|Prototype|Radiance|Raid|Rampage|Ravenous|Reach|Rebound|" +
        "Reconfigure|Recover|Reinforce|Renown|Replicate|Retrace|Riot|Ripple|Sacrifice|" +
        "Saddle|Scry|Shadow|Skulk|Soulbond|Soulshift|Spectacle|Splice|Split second|Squad|" +
        "Storm|Sunburst|Surge|Suspend|Surveil|Toxic|Training|Trample|Transfigure|" +
        "Transform|Transmute|Tribute|Undaunted|Undying|Unearth|Unleash|Vanishing|" +
        "Vigilance|Ward|Wither|Discard|Draw|Token|Tokens|Treasure|Clue|Food|Counter|" +
        "Counters|Exile|Graveyard|Mill|Destroy|Search your library"
    ).Split('|');

    private string? BestOracleKeyword(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return OracleKeywordDictionary.FirstOrDefault(x =>
                   x.Equals(value, StringComparison.OrdinalIgnoreCase)) ??
               OracleKeywordDictionary.FirstOrDefault(x =>
                   x.StartsWith(value, StringComparison.OrdinalIgnoreCase) &&
                   !oracleKeywordTokens.Contains(x, StringComparer.OrdinalIgnoreCase));
    }

    private void OnOracleKeywordChanged(object sender, TextChangedEventArgs e)
    {
        if (oracleKeywordUiChanging || OracleFilterBox is null) return;
        var raw = OracleFilterBox.Text;
        if (raw.Contains(','))
        {
            var chunks = raw.Split(',');
            foreach (var chunk in chunks.Take(chunks.Length - 1))
                AddOracleToken(chunk.Trim(), refresh: false);
            OracleFilterBox.Text = chunks[^1];
            OracleFilterBox.CaretIndex = OracleFilterBox.Text.Length;
        }
        UpdateOracleSuggestions();
        OnCatalogFilterChanged(sender, e);
    }

    private void OnOracleKeywordKeyDown(object sender, KeyEventArgs e)
    {
        if (OracleFilterBox is null) return;
        var input = OracleFilterBox.Text.Trim();
        if (e.Key == Key.Back && input.Length == 0 && oracleKeywordTokens.Count > 0)
        {
            oracleKeywordTokens.RemoveAt(oracleKeywordTokens.Count - 1);
            DrawOracleTokens();
            _ = FilterAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Space || e.Key == Key.Enter || e.Key == Key.Tab)
        {
            var completion = BestOracleKeyword(input);
            if (completion is null && e.Key == Key.Space) return; // allow custom multi-word text
            if (input.Length == 0) return;
            AddOracleToken(completion ?? input);
            e.Handled = true;
        }
    }

    private void OnOracleSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (oracleKeywordUiChanging || OracleKeywordSuggestions?.SelectedItem is not string term)
            return;
        AddOracleToken(term);
        OracleFilterBox.Focus();
    }

    private void AddOracleToken(string word, bool refresh = true)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length > 100) return;
        word = word.Trim();
        if (!oracleKeywordTokens.Contains(word, StringComparer.OrdinalIgnoreCase))
            oracleKeywordTokens.Add(word);
        oracleKeywordUiChanging = true;
        if (OracleFilterBox is not null)
        {
            OracleFilterBox.Text = "";
            OracleFilterBox.CaretIndex = 0;
        }
        if (OracleKeywordSuggestions is not null)
        {
            OracleKeywordSuggestions.SelectedIndex = -1;
            OracleKeywordSuggestions.ItemsSource = null;
            OracleKeywordSuggestions.Visibility = Visibility.Collapsed;
        }
        oracleKeywordUiChanging = false;
        DrawOracleTokens();
        if (refresh) _ = FilterAsync();
    }

    private void UpdateOracleSuggestions()
    {
        if (OracleKeywordSuggestions is null || OracleFilterBox is null) return;
        var prefix = OracleFilterBox.Text.Trim();
        oracleKeywordUiChanging = true;
        OracleKeywordSuggestions.ItemsSource = prefix.Length == 0 ? null :
            OracleKeywordDictionary.Where(x =>
                x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                !oracleKeywordTokens.Contains(x, StringComparer.OrdinalIgnoreCase))
                .Take(5).ToArray();
        OracleKeywordSuggestions.Visibility =
            OracleKeywordSuggestions.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        oracleKeywordUiChanging = false;
    }

    private void DrawOracleTokens()
    {
        if (OracleTokensPanel is null) return;
        OracleTokensPanel.Children.Clear();
        foreach (string token in oracleKeywordTokens)
        {
            var pill = new Button
            {
                Content = token + "  ×",
                FontSize = 12,
                Padding = new Thickness(7, 3, 7, 3),
                Margin = new Thickness(0, 0, 5, 5),
                ToolTip = "Remove " + token + " from the rules-text filter"
            };
            pill.Click += (_, _) =>
            {
                oracleKeywordTokens.Remove(token);
                DrawOracleTokens();
                _ = FilterAsync();
            };
            OracleTokensPanel.Children.Add(pill);
        }
    }

    private string EffectiveOracleFilter()
    {
        var parts = oracleKeywordTokens.ToList();
        string pending = OracleFilterBox?.Text.Trim() ?? "";
        if (pending.Length > 0) parts.Add(pending);
        return string.Join(",", parts);
    }
}
