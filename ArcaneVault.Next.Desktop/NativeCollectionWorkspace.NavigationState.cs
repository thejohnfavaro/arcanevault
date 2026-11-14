using ArcaneVault.Next.Collection;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Transient UI-only state for replacing a VERIFIED native workspace after a
/// real save/import. It never persists ownership data or changes a draft.
/// Ordinary sidebar navigation reuses the original view instead.
/// </summary>
public partial class NativeCollectionWorkspace
{
    internal sealed record NavigationState(
        int Section, string CollectionSearch, bool MissingOnly,
        string DeckSearch, bool FavoritesOnly, string? SelectedDeckId,
        int SetScope, string SetSearch, bool IncludeEmptySets,
        bool HideUnownedSlots, string? SelectedSetCode);

    private string? pendingSetRestoreCode;

    internal NavigationState CaptureNavigationState() => new(
        SelectedSection, SearchBox?.Text ?? "",
        MissingOnly?.IsChecked == true,
        DeckSearchBox?.Text ?? "",
        FavoriteDecksOnly?.IsChecked == true,
        (DeckLibraryList?.SelectedItem as NativeDeckEditing.DeckRow)?.Id,
        SetScopeBox?.SelectedIndex ?? 0,
        SetSearch?.Text ?? "",
        ShowEmptySets?.IsChecked == true,
        HideUnownedSetSlots?.IsChecked == true,
        selectedSetRow?.SetCode);

    internal async Task RestoreNavigationStateAsync(NavigationState state)
    {
        if (closing || state is null) return;
        // Each property may invoke an existing filter event, but none reloads
        // the verified physical collection; they filter the already loaded
        // read-only models. The active catalog snapshot is authoritative.
        SearchBox.Text = state.CollectionSearch;
        MissingOnly.IsChecked = state.MissingOnly;
        DeckSearchBox.Text = state.DeckSearch;
        FavoriteDecksOnly.IsChecked = state.FavoritesOnly;
        RefreshDeckRows();
        if (state.SelectedDeckId is not null)
        {
            var item = DeckLibraryList.Items
                .OfType<NativeDeckEditing.DeckRow>()
                .FirstOrDefault(x => x.Id.Equals(state.SelectedDeckId,
                    StringComparison.OrdinalIgnoreCase));
            if (item is not null) DeckLibraryList.SelectedItem = item;
        }
        SetSearch.Text = state.SetSearch;
        ShowEmptySets.IsChecked = state.IncludeEmptySets;
        HideUnownedSetSlots.IsChecked = state.HideUnownedSlots;
        pendingSetRestoreCode = state.SelectedSetCode;
        if (SetScopeBox.SelectedIndex != state.SetScope)
            SetScopeBox.SelectedIndex = state.SetScope;
        else
        {
            RefreshSetList();
            await RestorePendingSetAsync();
        }
        NavigateTo(Math.Clamp(state.Section, 0, 3));
    }

    private async Task RestorePendingSetAsync()
    {
        string? code = pendingSetRestoreCode;
        if (closing || string.IsNullOrEmpty(code)) return;
        var source = ArenaSetScope ? arenaProgress : progress;
        var found = source?.Sets.FirstOrDefault(x =>
            x.SetCode.Equals(code, StringComparison.OrdinalIgnoreCase));
        pendingSetRestoreCode = null;
        if (found is null) return; // Set no longer verified: don't fake state
        selectedSetRow = found;
        SetDetailsMode(true);
        await LoadSetDetailsAsync(found);
    }
}
