using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.Text.Json;

internal static class NativeArenaImportChecks
{
    private static void Must(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Arena import: " + message);
    }

    private static byte[] Fixture() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schema = 2,
        cards = new object[] { new { name = "Sol Ring", qty = 2, note = "Keep" } },
        physicalCards = new[] { new {
            name = "Sol Ring", qty = 1, set = "CMM", number = "396",
            scryfallId = "", finish = "nonfoil" } },
        deckLibrary = new[] { new { name = "Deck", entries = Array.Empty<object>() } },
        preferredArtwork = new { image = "kept" }
    });

    private static NativeExcelCollectionImport.Row Row(int index, string name, int count) =>
        new(index, name, count, "", "", "unspecified", "");

    public static void Run(string work)
    {
        string path = Path.Combine(work, "arena-import-check.sqlite");
        var original = NativeWorkspace.ImportNewBytes(Fixture(), "arena-import-test", path);
        var rows = new[]
        {
            Row(2, "sol ring", 2),
            Row(3, "New Spell", 1),
            Row(4, "new spell", 3)
        };
        var updated = NativeWorkspaceSafety.AddArenaImport(path, rows, original.BackupSha256);
        var view = updated.Current.Collection;
        Must(view.ArenaCards.Count == 2 && view.ArenaCopies == 8,
            "File duplicate names must combine and add to existing quantities.");
        Must(view.ArenaCards[0].Name == "Sol Ring" &&
             view.ArenaCards[0].Quantity == 4 &&
             view.ArenaCards[1].Quantity == 4,
            "Arena import must preserve existing capitalization and totals.");
        Must(view.PhysicalCards.SequenceEqual(original.Collection.PhysicalCards) &&
             view.Decks == original.Collection.Decks &&
             view.PreferredArtworks == original.Collection.PreferredArtworks,
            "Arena import must preserve physical collection, decks and artwork.");
        Must(NativeWorkspace.Open(updated.RecoveryPath).BackupSha256 ==
             original.BackupSha256,
            "Arena import must save a complete recovery point.");

        try
        {
            NativeWorkspaceSafety.AddArenaImport(path,
                new[] { Row(2, "Sol Ring", 99_999) }, updated.Current.BackupSha256);
            throw new InvalidOperationException("Arena import accepted overflowing quantities.");
        }
        catch (InvalidDataException) { }
        try
        {
            NativeWorkspaceSafety.AddArenaImport(path, rows, original.BackupSha256);
            throw new InvalidOperationException("Arena import accepted stale SHA.");
        }
        catch (InvalidDataException) { }
        Must(NativeWorkspace.Open(path).BackupSha256 == updated.Current.BackupSha256,
            "Rejected import must not change the active workspace.");
    }
}
