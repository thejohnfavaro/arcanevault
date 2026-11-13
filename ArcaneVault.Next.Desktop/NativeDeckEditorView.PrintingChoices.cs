using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

public partial class NativeDeckEditorView
{
    private sealed record MainboardPrintingOption(string Id, string Display);
    private readonly Dictionary<string, string> deckPrintingChoices =
        new(StringComparer.OrdinalIgnoreCase);
    private int printingSelectionVersion;

    private void InitializeDeckPrintingChoices()
    {
        if (original is null) return;
        foreach (var pair in original.EntryPrintingIds)
            deckPrintingChoices[pair.Key] = pair.Value;
    }

    private async void RefreshSelectedMainboardPrintings()
    {
        if (MainboardPrintingBox is null || MainboardPrintingApply is null ||
            MainboardPrintingStatus is null) return;
        int version = ++printingSelectionVersion;
        MainboardPrintingBox.ItemsSource = null;
        MainboardPrintingBox.IsEnabled = false;
        MainboardPrintingApply.IsEnabled = false;
        if (MainboardManagerList?.SelectedItem is not
            ArcaneVault.Next.EDHLab.EdhLabMainboardView.Row { IsHeader: false } selected)
        {
            MainboardPrintingStatus.Text = "Select a mainboard card to browse printings.";
            return;
        }
        string cardName = selected.Name;
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            MainboardPrintingStatus.Text =
                "A synced local Scryfall printing catalog is required.";
            return;
        }
        MainboardPrintingStatus.Text = "Reading official printings from the local catalog…";
        try
        {
            bool arena = edhLabFormat == EdhLabFormat.ArenaBrawl;
            var gallery = await Task.Run(() => NativePrintingArtwork.Read(
                catalogPath!, cardName, arenaOnly: arena));
            if (version != printingSelectionVersion || !IsLoaded ||
                MainboardManagerList.SelectedItem is not
                    ArcaneVault.Next.EDHLab.EdhLabMainboardView.Row current ||
                !current.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase))
                return;
            var options = new List<MainboardPrintingOption>
            {
                new("", "Default card artwork (no deck printing preference)")
            };
            foreach (var printing in gallery.Printings)
            {
                long realCount = 0;
                if (!arena)
                    foreach (var owned in ownership.PhysicalCards)
                        if (Guid.TryParse(owned.ScryfallId, out var id) &&
                            id == printing.Id &&
                            owned.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase))
                            realCount += owned.Quantity;
                string label = realCount > 0
                    ? $"OWNED ×{realCount} · {printing.Title} · {printing.SetName}"
                    : $"{printing.Title} · {printing.SetName}" +
                        (arena ? " · Arena preview only" : " · not verified owned");
                options.Add(new(printing.Id.ToString("D"), label));
            }
            // This is a display choice, never an assertion of ownership.
            options = options.Take(1).Concat(options.Skip(1)
                .OrderByDescending(x => x.Display.StartsWith("OWNED ×",
                    StringComparison.Ordinal))).ToList();
            if (deckPrintingChoices.TryGetValue(cardName, out var savedId) &&
                !options.Any(x => x.Id.Equals(savedId, StringComparison.OrdinalIgnoreCase)))
                options.Add(new(savedId,
                    "Previously selected printing unavailable in current catalog (not editable)"));
            MainboardPrintingBox.ItemsSource = options;
            MainboardPrintingBox.SelectedItem = options.FirstOrDefault(x =>
                deckPrintingChoices.TryGetValue(cardName, out var id) &&
                x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? options[0];
            MainboardPrintingBox.IsEnabled = options.Count > 1;
            MainboardPrintingApply.IsEnabled = options.Count > 0;
            MainboardPrintingStatus.Text = arena
                ? "Arena uses a representative official preview, not a verified cosmetic style."
                : "OWNED is matched by exact printing ID. Other selections are display-only." +
                  (gallery.Truncated ? " Printing list limited; sync/search may be needed." : "");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or
            Microsoft.Data.Sqlite.SqliteException or ArgumentException)
        {
            if (version == printingSelectionVersion)
                MainboardPrintingStatus.Text = "Printings unavailable: " + ex.Message;
        }
    }

    private void OnMainboardPrintingApplyClicked(object sender, RoutedEventArgs e)
    {
        if (busy ||
            MainboardManagerList?.SelectedItem is not
                ArcaneVault.Next.EDHLab.EdhLabMainboardView.Row { IsHeader: false } row ||
            MainboardPrintingBox?.SelectedItem is not MainboardPrintingOption selected)
            return;
        if (selected.Id.Length == 0)
            deckPrintingChoices.Remove(row.Name);
        else
        {
            if (!Guid.TryParse(selected.Id, out var id) || id == Guid.Empty ||
                selected.Display.StartsWith("Previously selected", StringComparison.Ordinal))
            {
                MainboardPrintingStatus.Text = "Select a verified current catalog entry.";
                return;
            }
            deckPrintingChoices[row.Name] = id.ToString("D");
        }
        MainboardPrintingStatus.Text = selected.Id.Length == 0
            ? "Default artwork selected for this unsaved deck. Review and save to persist."
            : "Display printing selected for this unsaved deck. Review and save to persist. " +
              "No ownership, proxy ledger or card price was changed.";
        StatusText.Text = MainboardPrintingStatus.Text;
        if (previewedCard is { } visible &&
            visible.Name.Equals(row.Name, StringComparison.OrdinalIgnoreCase))
            _ = ShowCardPreviewAsync(visible);
    }
}
