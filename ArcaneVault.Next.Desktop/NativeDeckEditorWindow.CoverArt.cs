using ArcaneVault.Next.Collection;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

public partial class NativeDeckEditorWindow
{
    private sealed record CoverPrintingOption(string Id, string Display);
    private bool updatingCoverControls;
    private int coverGalleryVersion;
    private string coverModeChoice = "Auto";
    private string coverCardChoice = "";
    private string coverPrintingChoice = "";

    private void InitializeDeckCoverChoice()
    {
        coverModeChoice = original?.DeckCoverMode ?? "Auto";
        coverCardChoice = original?.DeckCoverCardName ?? "";
        coverPrintingChoice = original?.DeckCoverPrintingId ?? "";
        updatingCoverControls = true;
        DeckCoverModeBox.SelectedIndex = coverModeChoice == "Custom" ? 1 : 0;
        updatingCoverControls = false;
        RefreshDeckCoverCandidates();
    }

    private void RefreshDeckCoverCandidates()
    {
        if (DeckCoverCardBox is null || MainboardBox is null ||
            CommanderBox is null || DeckCoverModeBox is null) return;
        IReadOnlyList<NativeDeckEditing.Entry> entries;
        try { entries = NativeDeckEditing.ParseMainboard(MainboardBox.Text); }
        catch (InvalidDataException) { return; } // transient draft typing
        var names = entries.Select(x => x.Name)
            .Concat(string.IsNullOrWhiteSpace(CommanderBox.Text)
                ? Array.Empty<string>() : new[] { CommanderBox.Text.Trim() })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        updatingCoverControls = true;
        try
        {
            DeckCoverCardBox.ItemsSource = names;
            DeckCoverCardBox.SelectedItem = names.FirstOrDefault(x =>
                x.Equals(coverCardChoice, StringComparison.OrdinalIgnoreCase));
            DeckCoverCardBox.IsEnabled = coverModeChoice == "Custom" && names.Length > 0;
            DeckCoverPrintingBox.IsEnabled = coverModeChoice == "Custom" &&
                DeckCoverCardBox.SelectedItem is string;
        }
        finally { updatingCoverControls = false; }
        if (coverModeChoice != "Custom")
            DeckCoverStatus.Text = "Auto: use commander art, or the first mainboard card in a 60-card deck.";
        else if (names.Length == 0)
            DeckCoverStatus.Text = "Add a card to this deck to choose its cover.";
        else if (!names.Any(x => x.Equals(coverCardChoice, StringComparison.OrdinalIgnoreCase)))
            DeckCoverStatus.Text = "Selected cover card is no longer in the deck. Saving will reset the cover to Auto.";
        else
            DeckCoverStatus.Text = "Custom cover is cosmetic only; it does not change owned printings.";
    }

    private void OnDeckCoverModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingCoverControls || DeckCoverModeBox is null ||
            MainboardBox is null) return;
        coverModeChoice = DeckCoverModeBox.SelectedIndex == 1 ? "Custom" : "Auto";
        RefreshDeckCoverCandidates();
        if (coverModeChoice == "Custom") _ = RefreshDeckCoverPrintings();
    }

    private void OnDeckCoverCardChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingCoverControls || DeckCoverCardBox is null) return;
        string next = DeckCoverCardBox.SelectedItem as string ?? "";
        if (string.Equals(next, coverCardChoice, StringComparison.OrdinalIgnoreCase)) return;
        coverCardChoice = next;
        coverPrintingChoice = "";
        RefreshDeckCoverCandidates();
        _ = RefreshDeckCoverPrintings();
    }

    private void OnDeckCoverPrintingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingCoverControls) return;
        if (DeckCoverPrintingBox?.SelectedItem is CoverPrintingOption chosen)
            coverPrintingChoice = chosen.Id;
    }

    private async Task RefreshDeckCoverPrintings()
    {
        if (DeckCoverPrintingBox is null || DeckCoverCardBox is null) return;
        int version = ++coverGalleryVersion;
        string name = coverCardChoice;
        updatingCoverControls = true;
        DeckCoverPrintingBox.ItemsSource = null;
        DeckCoverPrintingBox.IsEnabled = false;
        updatingCoverControls = false;
        if (coverModeChoice != "Custom" || name.Length == 0 ||
            string.IsNullOrWhiteSpace(catalogPath)) return;
        try
        {
            var gallery = await Task.Run(() => NativePrintingArtwork.Read(
                catalogPath!, name, limit: 300));
            if (version != coverGalleryVersion || coverModeChoice != "Custom" ||
                !name.Equals(coverCardChoice, StringComparison.OrdinalIgnoreCase)) return;
            var choices = new List<CoverPrintingOption> {
                new("", "Default printing for selected cover card")
            };
            foreach (var printing in gallery.Printings)
                choices.Add(new(printing.Id.ToString("D"),
                    printing.Title + " · " + printing.SetName));
            if (coverPrintingChoice.Length > 0 && !choices.Any(x =>
                x.Id.Equals(coverPrintingChoice, StringComparison.OrdinalIgnoreCase)))
                choices.Add(new(coverPrintingChoice, "Saved printing not in current catalog"));
            updatingCoverControls = true;
            try
            {
                DeckCoverPrintingBox.ItemsSource = choices;
                DeckCoverPrintingBox.SelectedItem = choices.FirstOrDefault(x =>
                    x.Id.Equals(coverPrintingChoice, StringComparison.OrdinalIgnoreCase))
                    ?? choices[0];
                DeckCoverPrintingBox.IsEnabled = true;
            }
            finally { updatingCoverControls = false; }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or
            Microsoft.Data.Sqlite.SqliteException or ArgumentException)
        {
            if (version == coverGalleryVersion)
                DeckCoverStatus.Text = "Custom card selected; official printing catalog unavailable. " +
                    "Default artwork will be used unless a saved print ID is available.";
        }
    }
}
