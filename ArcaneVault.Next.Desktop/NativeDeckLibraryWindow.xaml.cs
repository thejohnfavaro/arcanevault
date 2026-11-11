using ArcaneVault.Next.Collection;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Native, schema-2-preserving deck editor. No old browser, export or
/// Scryfall catalog required; source-version and recovery checks are
/// performed in the collection backend before every write.
/// </summary>
public partial class NativeDeckLibraryWindow : Window
{
    private readonly string workspacePath;
    private readonly Action? saved;
    private NativeWorkspaceSafety.DeckLibraryState? state;
    private NativeDeckEditing.DeckRow? editing;
    private bool busy;
    private bool dirty;
    private bool loading;
    private bool closed;

    public NativeDeckLibraryWindow(string workspacePath, Action? saved = null)
    {
        this.workspacePath = workspacePath;
        this.saved = saved;
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
        Closed += (_, _) => closed = true;
    }

    private void OnFormChanged(object sender, TextChangedEventArgs e)
    {
        if (!loading) dirty = true;
    }

    private void OnFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loading) dirty = true;
    }

    private bool ConfirmDiscard()
    {
        return !dirty || MessageBox.Show(this,
            "Discard unsaved deck changes? The saved deck library will not be affected.",
            "Discard draft", MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes;
    }

    private void EmptyDraft()
    {
        loading = true;
        editing = null;
        EditorTitle.Text = "New deck";
        NameBox.Text = "";
        CommanderBox.Text = "";
        CardsBox.Text = "";
        FormatBox.Text = "60-card";
        loading = false;
        dirty = false;
    }

    private void OnNew(object sender, RoutedEventArgs e)
    {
        if (busy || !ConfirmDiscard()) return;
        EmptyDraft();
    }

    private void OnLoadSelected(object sender, RoutedEventArgs e)
    {
        if (busy || DeckList.SelectedItem is not NativeDeckEditing.DeckRow selected ||
            !ConfirmDiscard()) return;
        loading = true;
        editing = selected;
        EditorTitle.Text = "Editing: " + selected.Name;
        NameBox.Text = selected.Name;
        CommanderBox.Text = selected.Commander;
        FormatBox.Text = selected.Format;
        CardsBox.Text = NativeDeckEditing.FormatCardLines(selected.Cards);
        loading = false;
        dirty = false;
    }

    private async Task ReloadAsync(int? targetOrdinal = null)
    {
        if (closed) return;
        busy = true;
        SetEnabled(false);
        StatusText.Text = "Verifying native workspace and loading saved decks…";
        try
        {
            var data = await Task.Run(() =>
                NativeWorkspaceSafety.OpenDeckLibrary(workspacePath));
            if (closed) return;
            state = data;
            DeckList.ItemsSource = data.Decks;
            if (targetOrdinal.HasValue)
                DeckList.SelectedItem = data.Decks.FirstOrDefault(
                    d => d.Ordinal == targetOrdinal.Value);
            StatusText.Text = $"{data.Decks.Count:N0} decks loaded from native SQLite. " +
                "Every save retains a complete recovery copy; card legality is not inferred.";
        }
        catch (Exception ex)
        {
            state = null;
            if (!closed)
                StatusText.Text = "Deck library could not be verified: " + ex.Message +
                    ". No data was reset.";
        }
        finally
        {
            busy = false;
            if (!closed) SetEnabled(true);
        }
    }

    private void SetEnabled(bool available)
    {
        SaveButton.IsEnabled = available && state is not null;
        LoadButton.IsEnabled = available && state is not null;
        DeleteButton.IsEnabled = available && state is not null;
        DuplicateButton.IsEnabled = available && state is not null;
        DeckList.IsEnabled = available;
        NameBox.IsEnabled = available;
        CommanderBox.IsEnabled = available;
        FormatBox.IsEnabled = available;
        CardsBox.IsEnabled = available;
    }

    private async Task ApplyAsync(Func<NativeWorkspaceSafety.Replacement> operation,
        string description, int? selectIndex = null)
    {
        busy = true;
        SetEnabled(false);
        StatusText.Text = "Validating " + description +
            " and preserving the previous native collection…";
        try
        {
            var result = await Task.Run(operation);
            if (closed) return;
            saved?.Invoke();
            EmptyDraft();
            await ReloadAsync(selectIndex);
            if (!closed)
                StatusText.Text = description + " saved. Previous native database at " +
                    result.RecoveryPath + ". Use Restore native recovery to undo.";
        }
        catch (Exception ex)
        {
            if (!closed)
            {
                StatusText.Text = description + " not saved: " + ex.Message +
                    ". Reopen the library if data changed elsewhere.";
                MessageBox.Show(this, StatusText.Text,
                    "Deck save not completed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            busy = false;
            if (!closed) SetEnabled(true);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (busy || state is null) return;
        IReadOnlyList<NativeDeckEditing.DeckCard> cards;
        try
        {
            cards = NativeDeckEditing.ParseCardLines(CardsBox.Text);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            return;
        }
        string name = NameBox.Text.Trim(), commander = CommanderBox.Text.Trim();
        string format = FormatBox.Text.Trim();
        string action = editing is null ? "Create new deck" : "Update existing deck";
        if (MessageBox.Show(this,
            $"{action} '{name}' ({format}), {cards.Sum(x => x.Quantity):N0} library card copies" +
            (commander.Length > 0 ? $" plus commander {commander}" : "") +
            "?\n\nThe complete previous native database will be saved for recovery. " +
            "Your physical/Arena ownership and old Windows app will not be edited.",
            "Confirm native deck save", MessageBoxButton.YesNo, MessageBoxImage.Question)
            != MessageBoxResult.Yes) return;

        var prior = editing;
        string expected = state.BackupSha256;
        int ordinal = prior?.Ordinal ?? state.Decks.Count;
        await ApplyAsync(() => NativeWorkspaceSafety.SaveDeck(workspacePath,
                prior, name, commander, format, cards, expected),
            prior is null ? "New deck" : "Deck update", ordinal);
    }

    private async void OnDuplicate(object sender, RoutedEventArgs e)
    {
        if (busy || state is null ||
            DeckList.SelectedItem is not NativeDeckEditing.DeckRow row) return;
        if (MessageBox.Show(this,
            $"Duplicate '{row.Name}' as a new saved deck? The copy will retain " +
            "card quantities and deck settings, but start without revision history.",
            "Duplicate saved deck", MessageBoxButton.YesNo, MessageBoxImage.Question)
            != MessageBoxResult.Yes) return;
        var expected = state.BackupSha256;
        await ApplyAsync(() => NativeWorkspaceSafety.DuplicateDeck(
                workspacePath, row, expected), "Deck duplicate", state.Decks.Count);
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (busy || state is null ||
            DeckList.SelectedItem is not NativeDeckEditing.DeckRow row) return;
        if (MessageBox.Show(this,
            $"Delete '{row.Name}' and its {row.Revisions:N0} revisions from the ACTIVE " +
            "native deck library?\n\nThe previous complete native workspace is " +
            "retained as an independent recovery file. No physical/Arena card is deleted.",
            "Delete saved deck", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes) return;
        var expected = state.BackupSha256;
        await ApplyAsync(() => NativeWorkspaceSafety.DeleteDeck(
            workspacePath, row, expected), "Deck deletion");
    }
}
