using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.Text.Json;

internal static class NativeDeckOwnedPickerChecks
{
    private static void Must(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Owned deck picker: " + message);
    }

    public static void Run()
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            cards = new object[] {
                new { name = "Sol Ring", qty = 1 },
                new { name = "Arena Exclusive", qty = 4 },
                new { name = "Unused", qty = 0 }
            },
            physicalCards = new object[] {
                new { name = "Sol Ring", qty = 2 },
                new { name = "sol ring", qty = 3 },
                new { name = "Forest", qty = 10 }
            },
            deckLibrary = Array.Empty<object>(),
            preferredArtwork = new { }
        });
        NativeBackupView owned = NativeBackupReader.Read(data);
        var physical = NativeDeckOwnedPicker.Search(owned, NativeDeckOwnedPicker.Pool.Physical, "sol");
        var arena = NativeDeckOwnedPicker.Search(owned, NativeDeckOwnedPicker.Pool.Arena, "sol");
        Must(physical.Count == 1 && physical[0].Owned == 5,
            "Printings and case variants must aggregate within physical inventory.");
        Must(arena.Count == 1 && arena[0].Owned == 1,
            "Arena quantities must not be combined with physical quantities.");
        Must(NativeDeckOwnedPicker.Search(owned, NativeDeckOwnedPicker.Pool.Arena, "")
            .All(x => x.Name != "Unused"), "Zero-count cards must not appear.");
        Must(NativeDeckOwnedPicker.Search(owned, NativeDeckOwnedPicker.Pool.Physical, "forest")
            .Single().Owned == 10, "Case-insensitive search failed.");
        Must(NativeDeckOwnedPicker.AddOne("", "Sol Ring") == "1 Sol Ring",
            "New draft card was not appended.");
        Must(NativeDeckOwnedPicker.AddOne("2 Sol Ring\n1 Island", "sol ring") ==
            "3 Sol Ring\n1 Island", "Existing card count was not incremented.");
        Must(NativeDeckOwnedPicker.AddOne("1 Fire // Ice", "Island") ==
            "1 Fire // Ice\n1 Island", "Split names were not preserved.");
        bool invalid = false;
        try { NativeDeckOwnedPicker.AddOne("bad line", "Sol Ring"); }
        catch (InvalidDataException) { invalid = true; }
        Must(invalid, "Malformed draft must be rejected instead of rewritten.");
        Must(NativeDeckOwnedPicker.Search(owned, NativeDeckOwnedPicker.Pool.Physical, "")
            .Count == 2, "Ownership search mutated or introduced inventory lines.");
        var physicalDraft = NativeDeckOwnedPicker.Review(owned,
            NativeDeckOwnedPicker.Pool.Physical,
            "2 Sol Ring\n1 Forest\n1 Not Owned", "Sol Ring", "Commander");
        Must(physicalDraft.MainboardCopies == 4 &&
             physicalDraft.TotalWithCommander == 5 &&
             physicalDraft.MissingCopies == 1 &&
             physicalDraft.Shortages.Single().Name == "Not Owned",
             "Commander must be counted separately without mixing owned pools.");
        var arenaDraft = NativeDeckOwnedPicker.Review(owned,
            NativeDeckOwnedPicker.Pool.Arena,
            "2 Sol Ring\n1 Arena Exclusive", "", "Arena Brawl");
        Must(arenaDraft.TotalWithCommander == 3 &&
             arenaDraft.MissingCopies == 1 &&
             arenaDraft.Shortages.Single().Name == "Sol Ring",
             "Arena shortage must use only Arena inventory.");
        var regularDraft = NativeDeckOwnedPicker.Review(owned,
            NativeDeckOwnedPicker.Pool.Physical,
            "1 Sol Ring", "Missing Commander", "60-card");
        Must(regularDraft.TotalWithCommander == 1 &&
             regularDraft.MissingCopies == 0,
             "A non-Commander format must not count the optional leader field.");
        bool malformed = false;
        try {
            NativeDeckOwnedPicker.Review(owned,
                NativeDeckOwnedPicker.Pool.Physical, "invalid", "", "60-card");
        } catch (InvalidDataException) { malformed = true; }
        Must(malformed, "Invalid draft text must not yield misleading shortage totals.");
        Console.WriteLine("PASS: native deck owned-card picker (pool isolation, aggregates, safe add)");
    }
}
