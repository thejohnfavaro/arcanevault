using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeDeckEditingChecks
{
    private static void Must(bool value, string reason)
    {
        if (!value) throw new InvalidOperationException("Native saved deck editor: " + reason);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Native saved deck editor accepted an invalid change.");
    }

    private static byte[] Source() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schema = 2,
        cards = new object[]
        {
            new { name = "Arena Card", qty = 4, note = "Owned unchanged" }
        },
        physicalCards = new object[]
        {
            new { name = "Sol Ring", qty = 2, set = "TST", number = "21",
                scryfallId = "00000000-0000-4000-8000-000000000771",
                finish = "foil", location = "Deckbox A" },
            new { name = "Unresolved", qty = 1, set = "", number = "",
                scryfallId = "", finish = "nonfoil" }
        },
        deckLibrary = new object[]
        {
            new {
                id = "deck_legacy_1", name = "Legacy Commander",
                commander = "Test Commander",
                entries = new object[]
                {
                    new { name = "Sol Ring", qty = 1, role = "mana rock" },
                    new { name = "Island", qty = 12 },
                    new { name = "Split // Back", qty = 1 }
                },
                updated = "2026-11-01T00:00:00Z",
                favorite = true,
                folder = "EDH",
                tags = new[] { "blue", "casual" },
                coverCard = "Sol Ring",
                arenaDeckId = "arena-special-identifier",
                unknownFuture = new { nested = new[] { 4, 2 } },
                revisions = new object[]
                {
                    new {
                        name = "Earlier",
                        commander = "Test Commander",
                        entries = new[] { new { name = "Island", qty = 12 } },
                        savedAt = "2026-10-01T00:00:00Z",
                        label = "Original history"
                    }
                }
            },
            new { id = "deck_legacy_2", name = "Second Deck",
                commander = "Other Commander",
                entries = new [] { new { name = "Forest", qty = 20 } },
                revisions = Array.Empty<object>(), favorite = false }
        },
        preferredArtwork = new { selected = new { image = "test-art.png" } },
        favorites = new[] { "Sol Ring" },
        collectionHistory = new[] { new { action = "Import" } },
        legacyStorage = new { secretField = "Do not change" }
    });

    public static void Run(string work)
    {
        string path = Path.Combine(work, "native-saved-deck-editor.sqlite");
        string exportPath = Path.Combine(work, "deck-fixture-original.json");
        byte[] source = Source();
        File.WriteAllBytes(exportPath, source);
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(exportPath));

        var initial = NativeWorkspace.ImportNewBytes(source, "full-deck-fixture", path);
        var list = NativeWorkspaceSafety.ListDecks(path);
        Must(list.Count == 2 && list[0].Name == "Legacy Commander" &&
             list[0].Commander == "Test Commander" &&
             list[0].TotalCopies == 14 && list[0].EntryCount == 3 &&
             list[0].Revisions == 1 && list[0].Folder == "EDH" &&
             list[0].Editable && list[1].Editable,
             "Existing deck library metadata did not survive reading the native workspace.");

        var mainboard = NativeDeckEditing.ParseMainboard(
            "1 Sol Ring\n11 Island\n2 island\n1 Split // Back\n");
        Must(mainboard.Count == 3 && mainboard[1].Quantity == 13 &&
             mainboard[2].Name == "Split // Back",
             "Duplicate case-insensitive land lines or split card names were parsed incorrectly.");

        // Mainboard preview resolves only the active caret line; it must
        // never rewrite or parse an unrelated in-progress invalid line.
        const string previewDraft = "1 Sol Ring\r\n2 Fire // Ice\n\n4 Island\nbad line";
        int fire = previewDraft.IndexOf("Fire // Ice", StringComparison.Ordinal);
        int island = previewDraft.IndexOf("Island", StringComparison.Ordinal);
        int blank = previewDraft.IndexOf("\n\n", StringComparison.Ordinal) + 1;
        Must(NativeDeckEditing.PreviewNameAtCaret(previewDraft, 0) == "Sol Ring" &&
             NativeDeckEditing.PreviewNameAtCaret(previewDraft, fire) == "Fire // Ice" &&
             NativeDeckEditing.PreviewNameAtCaret(previewDraft, fire + 5) == "Fire // Ice" &&
             NativeDeckEditing.PreviewNameAtCaret(previewDraft, island) == "Island" &&
             NativeDeckEditing.PreviewNameAtCaret(previewDraft, blank) is null &&
             NativeDeckEditing.PreviewNameAtCaret(previewDraft, previewDraft.Length) is null &&
             NativeDeckEditing.PreviewNameAtCaret("1 Sol Ring", 0) == "Sol Ring" &&
             NativeDeckEditing.PreviewNameAtCaret("", 0) is null &&
             NativeDeckEditing.PreviewNameAtCaret(previewDraft, -1) is null &&
             NativeDeckEditing.PreviewNameAtCaret(previewDraft, previewDraft.Length + 1) is null &&
             NativeDeckEditing.PreviewNameAtCaret("0 Island", 4) is null &&
             NativeDeckEditing.PreviewNameAtCaret("251 Island", 5) is null,
             "Card preview must resolve split names and keyboard/mouse caret lines without accepting invalid or blank entries.");
        Must(previewDraft == "1 Sol Ring\r\n2 Fire // Ice\n\n4 Island\nbad line",
             "Previewing an in-progress draft must not normalize or mutate its text.");

        Reject(() => NativeDeckEditing.ParseMainboard("Sol Ring"));
        Reject(() => NativeDeckEditing.ParseMainboard("0 Island"));
        Reject(() => NativeDeckEditing.ParseMainboard("251 Forest"));
        Reject(() => NativeDeckEditing.ParseMainboard("1 \n"));
        Reject(() => NativeDeckEditing.ParseMainboard(
            string.Join("\n", Enumerable.Repeat("2 Plains", 501))));

        // Deck-specific art selections round-trip without changing owned
        // printings, card quantities, or unrelated saved decks.
        const string chosenPrint = "00000000-0000-4000-8000-000000000771";
        var chosen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sol Ring"] = chosenPrint,
            ["Split // Back"] = "00000000-0000-4000-8000-000000000772"
        };
        var artSaved = NativeDeckEditing.Save(source,
            "Legacy Commander", "Test Commander",
            list[0].MainboardText, list[0], displayPrintIds: chosen);
        var artRows = NativeDeckEditing.List(artSaved);
        Must(artRows[0].EntryPrintingIds["Sol Ring"] == chosenPrint &&
             artRows[0].EntryPrintingIds.ContainsKey("Split // Back") &&
             artRows[0].TotalCopies == list[0].TotalCopies &&
             artRows[0].Revisions == list[0].Revisions + 1 &&
             artRows[1].SourceSha256 == list[1].SourceSha256,
             "Deck-specific printing choice did not round-trip independently.");
        using (var choicesJson = JsonDocument.Parse(artSaved))
        {
            var artRoot = choicesJson.RootElement;
            var deck = artRoot.GetProperty("deckLibrary")[0];
            Must(deck.GetProperty("entries")[0].GetProperty("displayPrintId")
                     .GetString() == chosenPrint &&
                 deck.GetProperty("revisions")[1].GetProperty("entries")[0]
                     .TryGetProperty("displayPrintId", out _) == false &&
                 artRoot.GetProperty("physicalCards")[0].GetProperty("scryfallId")
                     .GetString() == chosenPrint,
                 "Printing preference modified original physical identity or revisions.");
        }
        var cleared = NativeDeckEditing.Save(artSaved,
            "Legacy Commander", "Test Commander",
            artRows[0].MainboardText, artRows[0],
            displayPrintIds: new Dictionary<string, string>());
        Must(NativeDeckEditing.List(cleared)[0].EntryPrintingIds.Count == 0,
             "Explicitly restoring default artwork did not remove deck preference.");
        Reject(() => NativeDeckEditing.Save(source, "Legacy Commander",
            "Test Commander", list[0].MainboardText, list[0],
            displayPrintIds: new Dictionary<string, string> { ["Missing"] = chosenPrint }));
        Reject(() => NativeDeckEditing.Save(source, "Legacy Commander",
            "Test Commander", list[0].MainboardText, list[0],
            displayPrintIds: new Dictionary<string, string> { ["Sol Ring"] = "bogus" }));

        var updated = NativeWorkspaceSafety.SaveDeck(path,
            "Legacy Commander Revised", "Test Commander",
            "1 Sol Ring\n13 Island\n1 Split // Back\n1 Counterspell",
            list[0], initial.BackupSha256);
        Must(updated.Current.Collection.Decks == 2 &&
             updated.Current.Collection.DeckRevisions == 2 &&
             updated.Current.Collection.PhysicalCopies == 3 &&
             updated.Current.Collection.ArenaCopies == 4 &&
             updated.Current.Collection.PreferredArtworks == 1,
             "Deck update changed unrelated sections or failed to append a revision.");
        var afterUpdate = NativeWorkspaceSafety.ListDecks(path);
        Must(afterUpdate[0].Name == "Legacy Commander Revised" &&
             afterUpdate[0].TotalCopies == 16 &&
             afterUpdate[0].EntryCount == 4 &&
             afterUpdate[0].Revisions == 2 &&
             afterUpdate[1].Name == "Second Deck",
             "Deck update did not preserve other saved decks or its card list.");

        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = path, Mode = SqliteOpenMode.ReadOnly,
                 Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT backup FROM manifest WHERE id=1";
            using var json = JsonDocument.Parse((byte[])cmd.ExecuteScalar()!);
            var root = json.RootElement;
            var deck = root.GetProperty("deckLibrary")[0];
            Must(deck.GetProperty("id").GetString() == "deck_legacy_1" &&
                 deck.GetProperty("favorite").GetBoolean() &&
                 deck.GetProperty("folder").GetString() == "EDH" &&
                 deck.GetProperty("tags").GetArrayLength() == 2 &&
                 deck.GetProperty("arenaDeckId").GetString() == "arena-special-identifier" &&
                 deck.GetProperty("unknownFuture").GetProperty("nested")[1].GetInt32() == 2 &&
                 deck.GetProperty("entries")[0].GetProperty("role").GetString() == "mana rock" &&
                 deck.GetProperty("revisions")[0].GetProperty("label").GetString() == "Original history" &&
                 deck.GetProperty("revisions")[1].GetProperty("name").GetString() == "Legacy Commander" &&
                 root.GetProperty("legacyStorage").GetProperty("secretField").GetString() == "Do not change" &&
                 root.GetProperty("physicalCards")[0].GetProperty("location").GetString() == "Deckbox A" &&
                 root.GetProperty("cards")[0].GetProperty("note").GetString() == "Owned unchanged",
                 "Deck update discarded legacy metadata, earlier revisions, unknown fields or card annotations.");
        }

        int priorRollbackCount = NativeWorkspaceSafety.RecoveryPaths(path).Count;
        byte[] nativeHash = SHA256.HashData(File.ReadAllBytes(path));
        Reject(() => NativeWorkspaceSafety.SaveDeck(path,
            "Legacy Commander Revised", "Test Commander",
            "1 Sol Ring\n13 Island\n1 Split // Back\n1 Counterspell",
            afterUpdate[0], updated.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.SaveDeck(path,
            "Incorrect stale deck", "Test Commander", "1 Sol Ring",
            list[0], updated.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.SaveDeck(path,
            "Another name", "", "1 Island", afterUpdate[0], initial.BackupSha256));
        Must(nativeHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))) &&
             NativeWorkspaceSafety.RecoveryPaths(path).Count == priorRollbackCount,
             "No-op/stale deck edits changed native collection or added unnecessary rollback.");

        var added = NativeWorkspaceSafety.SaveDeck(path,
            "Fresh Modern Deck", "", "4 Llanowar Elves\n2 Forest",
            null, updated.Current.BackupSha256);
        var afterAdd = NativeWorkspaceSafety.ListDecks(path);
        Must(added.Current.Collection.Decks == 3 &&
             added.Current.Collection.DeckRevisions == 2 &&
             afterAdd.Count == 3 &&
             afterAdd[2].Name == "Fresh Modern Deck" &&
             afterAdd[2].Commander == "" &&
             afterAdd[2].TotalCopies == 6 &&
             afterAdd[2].Id.StartsWith("deck_native_", StringComparison.Ordinal) &&
             afterAdd[2].Revisions == 0,
             "New commander-optional saved deck did not get stable ID and correct card counts.");

        var deleted = NativeWorkspaceSafety.DeleteDeck(path,
            afterAdd[0], added.Current.BackupSha256);
        var afterDelete = NativeWorkspaceSafety.ListDecks(path);
        Must(deleted.Current.Collection.Decks == 2 &&
             deleted.Current.Collection.DeckRevisions == 0 &&
             afterDelete.Count == 2 &&
             afterDelete[0].Name == "Second Deck" &&
             afterDelete[1].Name == "Fresh Modern Deck" &&
             NativeWorkspace.Open(deleted.RecoveryPath).Collection.DeckRevisions == 2,
             "Delete failed to remove the selected deck and its revisions while preserving recovery.");

        var restored = NativeWorkspaceSafety.Restore(path,
            updated.RecoveryPath, deleted.Current.BackupSha256);
        var recoveryRows = NativeWorkspaceSafety.ListDecks(path);
        Must(restored.Current.BackupSha256 == initial.BackupSha256 &&
             recoveryRows.Count == 2 &&
             recoveryRows[0].Name == "Legacy Commander" &&
             recoveryRows[0].Revisions == 1 &&
             recoveryRows[0].TotalCopies == 14 &&
             restored.Current.Collection.PhysicalCopies == 3 &&
             restored.Current.Collection.ArenaCopies == 4 &&
             sourceHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(exportPath))) &&
             File.Exists(restored.RecoveryPath),
             "Native deck save/delete recovery did not restore the full original deck library.");

        // Unknown/unsupported imported deck entry payloads must not be
        // rewritten or silently normalized during an edit.
        byte[] malformed = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            cards = Array.Empty<object>(),
            physicalCards = Array.Empty<object>(),
            deckLibrary = new object[]
            {
                new { id = "legacy-weird", name = "Legacy unknown entries",
                    commander = "Any", entries = new object[]
                    { new { name = "Example", qty = "unknown", future = "preserve" } },
                    revisions = new[] { new { label = "original" } }
                }
            },
            preferredArtwork = new { }
        });
        string weird = Path.Combine(work, "native-deck-unsupported.sqlite");
        var unusual = NativeWorkspace.ImportNewBytes(malformed, "unsupported-old", weird);
        var weirdDeck = NativeWorkspaceSafety.ListDecks(weird)[0];
        Must(!weirdDeck.Editable && weirdDeck.Revisions == 1,
            "Unsupported legacy deck list should display but prohibit rewriting.");
        Reject(() => NativeWorkspaceSafety.SaveDeck(weird,
            "Rename unsafe row", "Any", "1 Example",
            weirdDeck, unusual.BackupSha256));

        // AVN-029: format is explicit stored metadata, not a claim that the
        // list is legal. On old backups, inferred display format is stable.
        string formatPath = Path.Combine(work, "native-deck-format-duplicate.sqlite");
        var baseline = NativeWorkspace.ImportNewBytes(source,
            "format-duplicate-fixture", formatPath);
        var originalRows = NativeWorkspaceSafety.ListDecks(formatPath);
        Must(originalRows[0].Format == "Commander" &&
             originalRows[1].Format == "Commander",
             "Imported legacy commander decks should default to Commander format.");

        var formatted = NativeWorkspaceSafety.SaveDeck(formatPath,
            originalRows[0].Name, originalRows[0].Commander,
            originalRows[0].MainboardText, originalRows[0],
            baseline.BackupSha256, format: "Arena Brawl");
        var formattedRows = NativeWorkspaceSafety.ListDecks(formatPath);
        Must(formatted.Current.Collection.Decks == 2 &&
             formatted.Current.Collection.DeckRevisions == 2 &&
             formattedRows[0].Id == "deck_legacy_1" &&
             formattedRows[0].Format == "Arena Brawl" &&
             formattedRows[1].Format == "Commander",
             "Changing just stored format must retain all original deck identity and history.");

        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = formatPath, Mode = SqliteOpenMode.ReadOnly,
                 Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT backup FROM manifest WHERE id=1";
            using var doc = JsonDocument.Parse((byte[])cmd.ExecuteScalar()!);
            var updatedDeck = doc.RootElement.GetProperty("deckLibrary")[0];
            Must(updatedDeck.GetProperty("format").GetString() == "Arena Brawl" &&
                 updatedDeck.GetProperty("revisions")[1].GetProperty("name").GetString() ==
                     "Legacy Commander" &&
                 updatedDeck.GetProperty("arenaDeckId").GetString() ==
                     "arena-special-identifier",
                "Format edit discarded existing deck history or external metadata.");
        }

        Reject(() => NativeWorkspaceSafety.SaveDeck(formatPath,
            formattedRows[0].Name, formattedRows[0].Commander,
            formattedRows[0].MainboardText, formattedRows[0],
            formatted.Current.BackupSha256, format: "Arena Brawl"));

        int oldCopies = NativeWorkspaceSafety.RecoveryPaths(formatPath).Count;
        Reject(() => NativeWorkspaceSafety.DuplicateDeck(
            formatPath, originalRows[0], formatted.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.DuplicateDeck(
            formatPath, formattedRows[0], baseline.BackupSha256));
        Must(NativeWorkspaceSafety.RecoveryPaths(formatPath).Count == oldCopies,
             "Stale selection or SHA on duplication made an unnecessary rollback.");

        var cloned = NativeWorkspaceSafety.DuplicateDeck(formatPath,
            formattedRows[0], formatted.Current.BackupSha256);
        var clonedRows = NativeWorkspaceSafety.ListDecks(formatPath);
        Must(cloned.Current.Collection.Decks == 3 &&
             cloned.Current.Collection.DeckRevisions == 2 &&
             clonedRows[2].Id != clonedRows[0].Id &&
             clonedRows[2].Id.StartsWith("deck_native_", StringComparison.Ordinal) &&
             clonedRows[2].Name == "Legacy Commander — Copy" &&
             clonedRows[2].Format == "Arena Brawl" &&
             clonedRows[2].Revisions == 0 &&
             clonedRows[2].MainboardText == clonedRows[0].MainboardText &&
             cloned.Current.Collection.PhysicalCopies == baseline.Collection.PhysicalCopies &&
             cloned.Current.Collection.ArenaCopies == baseline.Collection.ArenaCopies,
             "Deck duplication changed source deck, owned cards, copied revisions or lost format.");

        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = formatPath, Mode = SqliteOpenMode.ReadOnly,
                 Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT backup FROM manifest WHERE id=1";
            using var doc = JsonDocument.Parse((byte[])cmd.ExecuteScalar()!);
            var rows = doc.RootElement.GetProperty("deckLibrary");
            Must(rows[0].GetProperty("arenaDeckId").GetString() ==
                     "arena-special-identifier" &&
                 rows[2].GetProperty("arenaDeckId").GetString() == "" &&
                 rows[2].GetProperty("revisions").GetArrayLength() == 0 &&
                 rows[2].GetProperty("folder").GetString() == "EDH" &&
                 rows[2].GetProperty("unknownFuture").GetProperty("nested")[1].GetInt32() == 2,
                 "Duplicate should clear Arena link/history yet retain copied deck metadata.");
        }

        var recoveredCopy = NativeWorkspaceSafety.Restore(formatPath,
            cloned.RecoveryPath, cloned.Current.BackupSha256);
        Must(recoveredCopy.Current.Collection.Decks == 2 &&
             NativeWorkspaceSafety.ListDecks(formatPath)[0].Format == "Arena Brawl" &&
             File.Exists(recoveredCopy.RecoveryPath) &&
             NativeWorkspace.Open(recoveredCopy.RecoveryPath).Collection.Decks == 3,
             "Recovery did not reverse duplication while keeping newer state.");

        Console.WriteLine("PASS: native deck CRUD, explicit stored format, independent duplicate ID, unchanged metadata and recovery");
    }
}
