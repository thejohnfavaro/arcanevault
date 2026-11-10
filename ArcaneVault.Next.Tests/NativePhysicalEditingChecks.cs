using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativePhysicalEditingChecks
{
    private static void Must(bool yes, string reason)
    {
        if (!yes) throw new InvalidOperationException("Native physical edit: " + reason);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("An invalid ownership edit was accepted.");
    }

    public static void Run(string work)
    {
        var path = Path.Combine(work, "physical-edit-workspace.sqlite");
        var catalog = Path.Combine(work, "physical-edit-catalog.sqlite");
        var a = Guid.Parse("00000000-0000-4000-8000-000000000601");
        var b = Guid.Parse("00000000-0000-4000-8000-000000000602");
        var digital = Guid.Parse("00000000-0000-4000-8000-000000000603");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = catalog, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
        {
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = """
                CREATE TABLE variants (
                    print_id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    setcode TEXT NOT NULL,
                    collector TEXT NOT NULL,
                    games TEXT NOT NULL,
                    digital INTEGER NOT NULL
                );
                """;
            command.ExecuteNonQuery();
            using var add = db.CreateCommand();
            add.CommandText = """
                INSERT INTO variants(print_id,name,setcode,collector,games,digital)
                VALUES($id,$name,$set,$num,$games,$digital)
                """;
            foreach (var name in new[] { "$id", "$name", "$set", "$num", "$games", "$digital" })
                add.Parameters.Add(new SqliteParameter(name, ""));
            void Insert(Guid id, string name, string set, string num,
                string games = "paper,arena", int isDigital = 0)
            {
                add.Parameters["$id"].Value = id.ToString("D");
                add.Parameters["$name"].Value = name;
                add.Parameters["$set"].Value = set;
                add.Parameters["$num"].Value = num;
                add.Parameters["$games"].Value = games;
                add.Parameters["$digital"].Value = isDigital;
                add.ExecuteNonQuery();
            }
            Insert(a, "Proof% Card", "TST", "100");
            Insert(b, "Proof% Card", "ALT", "100");
            Insert(digital, "Proof Digital", "ANA", "7", "arena", 1);
        }

        var catalogHash = SHA256.HashData(File.ReadAllBytes(catalog));
        var search = NativePhysicalEditing.FindPaperPrintings(catalog, "Proof%");
        Must(search.Rows.Count == 2 && !search.HasMore &&
             search.Rows.All(p => p.Name == "Proof% Card"),
             "Search must treat % literally and return separate print identities.");
        Must(NativePhysicalEditing.FindPaperPrintings(catalog, "Proof", limit: 1).HasMore,
             "A bounded printing result must signal more matches.");
        Must(NativePhysicalEditing.FindPaperPrintings(catalog, "Proof Digital").Rows.Count == 0,
             "Digital-only printings must not appear in paper selection.");
        Reject(() => NativePhysicalEditing.FindPaperPrintings(catalog, "P"));
        Reject(() => NativePhysicalEditing.RequirePaperPrinting(catalog, digital));
        Reject(() => NativePhysicalEditing.RequirePaperPrinting(catalog, Guid.NewGuid()));

        var newWorkspace = NativeWorkspaceSafety.StartFresh(path);
        var emptyHash = newWorkspace.Current.BackupSha256;
        var first = NativeWorkspaceSafety.AddVerifiedPhysical(path, catalog,
            a, 2, "nonfoil", emptyHash);
        Must(first.Current.Collection.PhysicalCopies == 2 &&
             first.Current.Collection.PhysicalCards.Count == 1 &&
             first.Current.Collection.PhysicalCards[0].SetCode == "TST" &&
             first.Current.Collection.PhysicalCards[0].ScryfallId == a.ToString("D"),
             "First addition must select an exact paper printing.");
        Must(NativeWorkspace.Open(first.RecoveryPath).Collection.PhysicalCopies == 0,
             "Initial addition did not preserve a restorable empty collection.");

        var second = NativeWorkspaceSafety.AddVerifiedPhysical(path, catalog,
            a, 3, "nonfoil", first.Current.BackupSha256);
        Must(second.Current.Collection.PhysicalCopies == 5 &&
             second.Current.Collection.PhysicalCards.Count == 1 &&
             second.Current.Collection.PhysicalCards[0].Quantity == 5,
             "Adding the same printing and finish must change quantity, not duplicate rows.");
        var third = NativeWorkspaceSafety.AddVerifiedPhysical(path, catalog,
            a, 1, "foil", second.Current.BackupSha256);
        var fourth = NativeWorkspaceSafety.AddVerifiedPhysical(path, catalog,
            b, 4, "nonfoil", third.Current.BackupSha256);
        Must(fourth.Current.Collection.PhysicalCopies == 10 &&
             fourth.Current.Collection.PhysicalCards.Count == 3 &&
             fourth.Current.Collection.ArenaCopies == 0 &&
             fourth.Current.Collection.PhysicalCards.Any(p =>
                 p.SetCode == "ALT" && p.Quantity == 4),
             "Finish and printing variants must stay separate.");
        Must(NativeWorkspaceSafety.RecoveryPaths(path).Count == 4,
             "Each successful edit must preserve exactly one independent rollback.");

        var hashBeforeReject = SHA256.HashData(File.ReadAllBytes(path));
        var recoveryCount = NativeWorkspaceSafety.RecoveryPaths(path).Count;
        Reject(() => NativeWorkspaceSafety.AddVerifiedPhysical(path, catalog,
            a, 1, "foil", first.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.AddVerifiedPhysical(path, catalog,
            digital, 1, "nonfoil", fourth.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.AddVerifiedPhysical(path, catalog,
            a, 0, "nonfoil", fourth.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.AddVerifiedPhysical(path, catalog,
            a, 1, "unknown", fourth.Current.BackupSha256));
        Must(hashBeforeReject.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))) &&
             NativeWorkspaceSafety.RecoveryPaths(path).Count == recoveryCount,
             "Rejected edits changed live data or created recovery points.");

        var restored = NativeWorkspaceSafety.Restore(path, second.RecoveryPath,
            fourth.Current.BackupSha256);
        Must(restored.Current.Collection.PhysicalCopies == 2 &&
             restored.Current.Collection.PhysicalCards.Count == 1 &&
             NativeWorkspace.Open(restored.RecoveryPath).Collection.PhysicalCopies == 10,
             "Recovering a previous checkpoint must restore exact copies and preserve newer state.");
        Must(catalogHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(catalog))),
             "Physical edits modified the external catalog.");

        // Unknown backup keys, personal deck data and Arena rows survive a
        // physical-only edit, while unresolved name-only lines stay unresolved.
        var rich = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            cards = new[] { new { name = "Arena Test", qty = 4 } },
            physicalCards = new[] { new { name = "Proof% Card", qty = 6,
                set = "", number = "", finish = "nonfoil", scryfallId = "",
                location = "Do not guess" } },
            deckLibrary = new[] { new { name = "Deck", entries = Array.Empty<object>(),
                revisions = new[] { new { note = "old" } } } },
            preferredArtwork = new { selected = new { image = "art.jpg" } },
            extraFutureField = new { safety = "keep" }
        });
        var existing = NativeWorkspace.ImportNewBytes(rich, "rich-edit-fixture",
            Path.Combine(work, "physical-edit-rich.sqlite"));
        var richPath = existing.DatabasePath;
        var merged = NativeWorkspaceSafety.AddVerifiedPhysical(richPath, catalog,
            a, 1, "nonfoil", existing.BackupSha256);
        Must(merged.Current.Collection.PhysicalCopies == 7 &&
             merged.Current.Collection.PhysicalCards.Count == 2 &&
             merged.Current.Collection.CopiesWithMissingOrInvalidIds == 6 &&
             merged.Current.Collection.ArenaCopies == 4 &&
             merged.Current.Collection.Decks == 1 &&
             merged.Current.Collection.DeckRevisions == 1 &&
             merged.Current.Collection.PreferredArtworks == 1,
             "Name-only rows must stay unresolved and other data counts conserved.");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = richPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT backup FROM manifest WHERE id=1";
            using var json = JsonDocument.Parse((byte[])cmd.ExecuteScalar()!);
            var root = json.RootElement;
            Must(root.GetProperty("extraFutureField").GetProperty("safety").GetString() == "keep" &&
                 root.GetProperty("physicalCards")[0].GetProperty("location").GetString() == "Do not guess",
                 "Native editing discarded unknown backup fields or ownership metadata.");
        }

        Console.WriteLine("PASS: exact paper printing search, bounded literal search, native add/merge and foil variant, stale SHA and invalid edit guards, conserved unknown data, verified recovery and rollback");
    }
}
