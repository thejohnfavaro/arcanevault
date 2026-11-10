using ArcaneVault.Next.Domain;
using System.Text.Json;

internal static class OwnershipGoldenChecks
{
    private sealed record GoldenDocument(
        int SchemaVersion, string Description,
        CatalogPrinting[] CatalogPrintings,
        PhysicalInventoryLine[] PhysicalLines,
        GoldenExpected Expected);
    private sealed record GoldenExpected(
        long TotalCopies, long ConfirmedCopies, long UnresolvedCopies,
        int ConfirmedDistinctPrintings, ExpectedSet[] BySet, ExpectedUnresolved[] UnresolvedLines);
    private sealed record ExpectedSet(string SetCode, int DistinctPrintings, long Copies);
    private sealed record ExpectedUnresolved(Guid LineId, long Copies, string Reason);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Ownership golden contract: " + message);
    }

    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Ownership contract accepted invalid input: " + message);
    }

    public static void Run()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "ownership-v1.golden.json");
        var fixture = JsonSerializer.Deserialize<GoldenDocument>(File.ReadAllText(fixturePath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Cannot parse ownership golden fixture.");
        Check(fixture.SchemaVersion == 1, "Unexpected schema version.");
        Check(fixture.CatalogPrintings is not null && fixture.PhysicalLines is not null && fixture.Expected is not null,
            "Fixture missing required sections.");

        var physicalBefore = JsonSerializer.Serialize(fixture.PhysicalLines);
        var catalogBefore = JsonSerializer.Serialize(fixture.CatalogPrintings);
        var result = PrintingReconciliation.Reconcile(fixture.PhysicalLines, fixture.CatalogPrintings);
        var expected = fixture.Expected;
        Check(result.TotalCopies == expected.TotalCopies, "Physical copy total changed.");
        Check(result.ConfirmedCopies == expected.ConfirmedCopies, "Confirmed copy count changed.");
        Check(result.UnresolvedCopies == expected.UnresolvedCopies, "Unresolved copy count changed.");
        Check(result.TotalCopies == result.ConfirmedCopies + result.UnresolvedCopies,
            "Reconciliation lost or duplicated copies.");
        Check(result.ConfirmedDistinctPrintings == expected.ConfirmedDistinctPrintings,
            "Confirmed printing count changed.");
        Check(result.ConfirmedBySet.Count == expected.BySet.Length, "Unexpected confirmed set count.");
        foreach (var set in expected.BySet)
        {
            Check(result.ConfirmedBySet.TryGetValue(set.SetCode, out var observed),
                "Missing confirmed set " + set.SetCode);
            Check(observed!.DistinctPrintings == set.DistinctPrintings && observed.Copies == set.Copies,
                "Wrong confirmed set summary " + set.SetCode);
        }
        Check(result.UnresolvedLines.Count == expected.UnresolvedLines.Length,
            "Unresolved lines were dropped.");
        foreach (var line in expected.UnresolvedLines)
        {
            var observed = result.UnresolvedLines.SingleOrDefault(x => x.LineId == line.LineId);
            Check(observed is not null && observed.Copies == line.Copies &&
                observed.Reason.ToString() == line.Reason, "Unresolved identity changed: " + line.LineId);
        }
        Check(JsonSerializer.Serialize(fixture.PhysicalLines) == physicalBefore &&
              JsonSerializer.Serialize(fixture.CatalogPrintings) == catalogBefore,
              "Reconciliation modified an input object.");

        var matching = fixture.PhysicalLines[0];
        var nameOnly = matching with { PrintId = null };
        var onlyHint = PrintingReconciliation.Reconcile([nameOnly], fixture.CatalogPrintings);
        Check(onlyHint.ConfirmedCopies == 0 && onlyHint.UnresolvedCopies == matching.Quantity,
            "Name and set/collector hints were silently treated as confirmed.");
        var alteredName = matching with { Name = "Alias or alternative card-face name" };
        Check(PrintingReconciliation.Reconcile([alteredName], fixture.CatalogPrintings).ConfirmedCopies == matching.Quantity,
            "Canonical print ID should survive a legacy face/name alias.");

        Reject(() => PrintingReconciliation.Reconcile([matching with { Quantity = -1 }], fixture.CatalogPrintings),
            "Negative quantity");
        Reject(() => PrintingReconciliation.Reconcile([matching, matching], fixture.CatalogPrintings),
            "Duplicate stable line ID");
        Reject(() => PrintingReconciliation.Reconcile([matching with { LineId = Guid.Empty }], fixture.CatalogPrintings),
            "Empty line ID");
        Reject(() => PrintingReconciliation.Reconcile([matching with { PrintId = Guid.Empty }], fixture.CatalogPrintings),
            "Empty print ID");
        Reject(() => PrintingReconciliation.Reconcile([matching], [fixture.CatalogPrintings[0], fixture.CatalogPrintings[0]]),
            "Duplicate catalog ID");

        Console.WriteLine("PASS: Ownership v1 fixture: 13 copies (6 confirmed, 7 unresolved), 3 exact printings; no guesses; immutability and rejection checks");
    }
}
