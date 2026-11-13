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
        Must(NativeDeckOwnedPicker.CountOwned(owned, NativeDeckOwnedPicker.Pool.Physical,
                 "sol ring") == 5 &&
             NativeDeckOwnedPicker.CountOwned(owned, NativeDeckOwnedPicker.Pool.Arena,
                 "Sol Ring") == 1 &&
             NativeDeckOwnedPicker.CountOwned(owned, NativeDeckOwnedPicker.Pool.Physical,
                 "Arena Exclusive") == 0 &&
             NativeDeckOwnedPicker.CountOwned(owned, NativeDeckOwnedPicker.Pool.Arena,
                 "Arena Exclusive") == 4 &&
             NativeDeckOwnedPicker.CountOwned(owned, NativeDeckOwnedPicker.Pool.Physical,
                 "Unowned Card") == 0,
             "Mainboard preview must count exact card-name copies in the selected pool, including unowned names outside the picker.");
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
        // v0.9.71: Double-click quick-add is a name-level singleton gesture,
        // independent of Commander exceptions for basic lands and variants.
        Must(NativeDeckOwnedPicker.AddIfAbsent("", "Sol Ring") == "1 Sol Ring" &&
             NativeDeckOwnedPicker.AddIfAbsent("1 Island", "Sol Ring") ==
                 "1 Island\n1 Sol Ring" &&
             NativeDeckOwnedPicker.AddIfAbsent("1 Sol Ring\n1 Island", "sol ring") ==
                 "1 Sol Ring\n1 Island" &&
             NativeDeckOwnedPicker.AddIfAbsent("4 Forest\n1 Island", "FOREST") ==
                 "4 Forest\n1 Island" &&
             NativeDeckOwnedPicker.AddIfAbsent("1 Fire // Ice", "fire // ice") ==
                 "1 Fire // Ice" &&
             NativeDeckOwnedPicker.AddIfAbsent("1 Sol Ring\n1 Island", "Island") ==
                 "1 Sol Ring\n1 Island",
             "Double-click must add a missing card exactly once, preserve existing quantities and ignore case.");
        Must(NativeDeckOwnedPicker.AddOne("4 Forest", "Forest") == "5 Forest",
             "Manual Add (+1) must remain usable for multiple basics.");
        bool invalidQuickAdd = false;
        try { NativeDeckOwnedPicker.AddIfAbsent("malformed", "Sol Ring"); }
        catch (InvalidDataException) { invalidQuickAdd = true; }
        Must(invalidQuickAdd, "Quick-add must reject malformed mainboards without silent rewriting.");
        bool invalid = false;
        try { NativeDeckOwnedPicker.AddOne("bad line", "Sol Ring"); }
        catch (InvalidDataException) { invalid = true; }
        Must(invalid, "Malformed draft must be rejected instead of rewritten.");
        Must(NativeDeckOwnedPicker.RemoveOne("2 Sol Ring\n1 Island", "sol ring") ==
            "1 Sol Ring\n1 Island", "Removing one copy should decrement only that name.");
        Must(NativeDeckOwnedPicker.RemoveOne("1 Fire // Ice\n2 Island", "fire // ice") ==
            "2 Island", "Removing final split-card copy should delete only its line.");
        // v0.9.73 visual mainboard manager always edits the single
        // authoritative plain-text list; no separate list is persisted.
        Must(NativeDeckOwnedPicker.SetQuantity("4 Forest\n1 Sol Ring", "forest", 5) ==
                 "5 Forest\n1 Sol Ring" &&
             NativeDeckOwnedPicker.SetQuantity("4 Forest\n1 Sol Ring", "forest", 0) ==
                 "1 Sol Ring" &&
             NativeDeckOwnedPicker.SetQuantity("4 Forest\n1 Sol Ring", "Sol Ring", 0) ==
                 "4 Forest" &&
             NativeDeckOwnedPicker.SetQuantity("1 Fire // Ice\n1 Island",
                 "fire // ice", 2) == "2 Fire // Ice\n1 Island",
             "Manager count edits must modify only the chosen card, respecting split names and removal.");
        string exactText = " 1 Island\r\n2 Plains\r\n";
        Must(NativeDeckOwnedPicker.SetQuantity(exactText, "island", 1) == exactText,
             "Unchanged manager quantity must preserve original raw text exactly.");
        bool refusedAbsent = false, refusedInvalidCount = false,
            refusedMalformedManager = false, refusedOverTotal = false;
        try { NativeDeckOwnedPicker.SetQuantity("1 Sol Ring", "Island", 0); }
        catch (InvalidDataException) { refusedAbsent = true; }
        try { NativeDeckOwnedPicker.SetQuantity("1 Island", "Island", 251); }
        catch (ArgumentOutOfRangeException) { refusedInvalidCount = true; }
        try { NativeDeckOwnedPicker.SetQuantity("not a deck", "Island", 1); }
        catch (InvalidDataException) { refusedMalformedManager = true; }
        try { NativeDeckOwnedPicker.SetQuantity(
            "1 Island\n250 Plains\n250 Swamp\n250 Mountain\n249 Forest",
            "Island", 2); }
        catch (InvalidDataException) { refusedOverTotal = true; }
        Must(refusedAbsent && refusedInvalidCount && refusedMalformedManager && refusedOverTotal,
             "Manager must refuse stale or invalid edits rather than silently rewriting the draft.");

        Must(NativeDeckOwnedPicker.SortAlphabetically("2 Swamp\n1 Fire // Ice\n3 Island") ==
            "1 Fire // Ice\n3 Island\n2 Swamp", "Sorting must preserve exact quantities and split names.");
        Must(NativeDeckOwnedPicker.SortAlphabetically("") == "",
            "Sorting an empty draft should remain empty.");
        bool absent = false;
        try { NativeDeckOwnedPicker.RemoveOne("1 Sol Ring", "Mountain"); }
        catch (InvalidDataException) { absent = true; }
        Must(absent, "Removing an absent card must reject instead of changing another card.");
        bool invalidRemoval = false, invalidSort = false;
        try { NativeDeckOwnedPicker.RemoveOne("bad line", "Sol Ring"); }
        catch (InvalidDataException) { invalidRemoval = true; }
        try { NativeDeckOwnedPicker.SortAlphabetically("bad line"); }
        catch (InvalidDataException) { invalidSort = true; }
        Must(invalidRemoval && invalidSort,
            "Malformed draft edits must reject without silently rewriting text.");
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
        // Shared commander/mainboard names must need an extra copy in both
        // pool comparisons; Arena cannot borrow any of the physical copies.
        var commanderInMainboard = NativeDeckOwnedPicker.Review(owned,
            NativeDeckOwnedPicker.Pool.Physical, "4 Sol Ring", "Sol Ring", "Commander");
        var commanderInArena = NativeDeckOwnedPicker.Review(owned,
            NativeDeckOwnedPicker.Pool.Arena, "4 Sol Ring", "Sol Ring", "Commander");
        Must(commanderInMainboard.TotalWithCommander == 5 &&
             commanderInMainboard.CoveredCopies == 5 &&
             commanderInMainboard.MissingCopies == 0 &&
             commanderInArena.TotalWithCommander == 5 &&
             commanderInArena.CoveredCopies == 1 &&
             commanderInArena.MissingCopies == 4 &&
             commanderInArena.Shortages.Single().Name == "Sol Ring",
             "Read-only Deck Library coverage must include commander copies without mixing inventories.");
        var emptyWithCommander = NativeDeckOwnedPicker.Review(owned,
            NativeDeckOwnedPicker.Pool.Arena, "", "Arena Exclusive", "Arena Brawl");
        Must(emptyWithCommander.MainboardCopies == 0 &&
             emptyWithCommander.TotalWithCommander == 1 &&
             emptyWithCommander.MissingCopies == 0,
             "An empty mainboard with an Arena Brawl commander must still be accounted for.");
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
