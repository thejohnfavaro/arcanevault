using ArcaneVault.Next.Domain;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Repeatable CSV/XLSX snapshot reconciliation. Imported quantities are the
/// target for matched identities, not an additive increment. Local-only rows
/// are always preserved. The preview and committed transformation use this
/// same pure implementation, with a caller-enforced stale-backup SHA gate.
/// </summary>
public static class NativeCollectionReconciliation
{
    public sealed record Line(string Card, string Identity,
        int Current, int Imported, int Delta, string Action, string Verification)
    {
        public string ChangeLabel => Delta > 0 ? "+" + Delta : Delta.ToString();
    }

    /// <summary>
    /// Audit of the source snapshot and the proposed resulting inventory.
    /// A quantity match is not proof of catalog verification: unresolved rows
    /// are explicitly reported separately. All numbers derive from the exact
    /// normalized snapshot used to create the candidate bytes.
    /// </summary>
    public sealed record ImportAudit(
        int SourceRows, int DistinctIdentities, int CollapsedDuplicateRows,
        long SourceCopies, int UnresolvedRows, long UnresolvedCopies,
        int AmbiguousRows, int ZeroQuantityRows,
        int LocalOnlyRows, long LocalOnlyCopies,
        long ProposedScopeCopies, bool TargetQuantitiesMatch)
    {
        public bool EntireScopeMatchesSnapshot =>
            TargetQuantitiesMatch && LocalOnlyRows == 0 &&
            ProposedScopeCopies == SourceCopies;

        public string Display =>
            $"SOURCE AUDIT: {SourceRows:N0} rows / {SourceCopies:N0} copies; " +
            $"{DistinctIdentities:N0} distinct identities " +
            $"({CollapsedDuplicateRows:N0} repeat rows consolidated).\n" +
            $"PRINTING VERIFICATION: {UnresolvedRows:N0} unresolved source rows / " +
            $"{UnresolvedCopies:N0} copies; {AmbiguousRows:N0} ambiguous rows; " +
            $"{ZeroQuantityRows:N0} explicit zero rows.\n" +
            $"PROJECTED INVENTORY: {ProposedScopeCopies:N0} copies after apply; " +
            $"{LocalOnlyRows:N0} local-only rows / {LocalOnlyCopies:N0} copies preserved.\n" +
            (EntireScopeMatchesSnapshot
                ? "SNAPSHOT COVERAGE: all scope quantities will match the imported source."
                : TargetQuantitiesMatch
                    ? "SNAPSHOT COVERAGE: imported targets will match, but local-only holdings remain."
                    : "SNAPSHOT COVERAGE: target quantities could not be verified.");
    }

    public sealed record Result(byte[] Candidate, IReadOnlyList<Line> Lines,
        int LocalOnlyRows, string CollectionKind, ImportAudit Audit)
    {
        public bool HasChanges => Lines.Any(x => x.Delta != 0);
        public int AddedRows => Lines.Count(x => x.Action == "New");
        public int AdjustedRows => Lines.Count(x => x.Action == "Adjusted");
        public int RemovedRows => Lines.Count(x => x.Action == "Removed");
        public int UnchangedRows => Lines.Count(x => x.Action == "Unchanged");
        public long NetCopies => Lines.Sum(x => (long)x.Delta);
        public long IncreasingCopies => Lines.Where(x => x.Delta > 0).Sum(x => (long)x.Delta);
        public long DecreasingCopies => Lines.Where(x => x.Delta < 0).Sum(x => -(long)x.Delta);
        public string CandidateSha256 =>
            Convert.ToHexString(SHA256.HashData(Candidate));
        public string Summary =>
            $"{CollectionKind} reconciliation (imported quantities are the target)\n" +
            $"New: {AddedRows:N0} · adjusted: {AdjustedRows:N0} · " +
            $"removed by explicit zero: {RemovedRows:N0} · " +
            $"unchanged: {UnchangedRows:N0}\n" +
            $"Copies increased: +{IncreasingCopies:N0} · " +
            $"decreased: −{DecreasingCopies:N0} · net: {NetCopies:+#;-#;0}\n" +
            $"{LocalOnlyRows:N0} local-only rows are preserved (missing from file never means delete).";
    }

    private sealed record Desired(string Key, string Name, string Set,
        string Collector, string Finish, string Id, bool Verified, int Quantity,
        string Verification);

    private static string Part(string text) => text.Length + ":" + text;

    private static string PhysicalKey(string name, string set,
        string collector, string finish, string id)
    {
        if (Guid.TryParse(id, out var guid) && guid != Guid.Empty)
            return "P:" + guid.ToString("D") + ":" + Part(finish);
        // Unknown printings do not merge with verified copies of the
        // same card name. Identity includes the full unresolved annotation.
        return "U:" + Part(name.Trim()) + Part(set.Trim()) +
            Part(collector.Trim()) + Part(finish.Trim());
    }

    private static JsonObject Root(byte[] source, string arrayName,
        out JsonArray array, out NativeBackupView before)
    {
        before = NativeBackupReader.Read(source);
        var root = JsonNode.Parse(source) as JsonObject ??
            throw new InvalidDataException("Native source backup is not a JSON object.");
        array = root[arrayName] as JsonArray ??
            throw new InvalidDataException("Native backup is missing " + arrayName + ".");
        return root;
    }

    private static void ValidateConservation(byte[] updated,
        NativeBackupView before, string kind, long net)
    {
        var after = NativeBackupReader.Read(updated);
        bool preserved = before.Decks == after.Decks &&
            before.DeckRevisions == after.DeckRevisions &&
            before.PreferredArtworks == after.PreferredArtworks &&
            (kind == "Physical"
                ? after.PhysicalCopies == checked(before.PhysicalCopies + net) &&
                  before.ArenaCards.SequenceEqual(after.ArenaCards)
                : after.ArenaCopies == checked(before.ArenaCopies + net) &&
                  before.PhysicalCards.SequenceEqual(after.PhysicalCards));
        if (!preserved)
            throw new InvalidDataException(
                "Reconciliation changed unrelated native collection sections.");
    }

    public static Result Physical(byte[] source,
        NativeExcelCollectionImport.Preview prepared)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(prepared);
        var root = Root(source, "physicalCards", out var target, out var before);
        var existing = new Dictionary<string, List<int>>(
            StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < before.PhysicalCards.Count; i++)
        {
            var row = before.PhysicalCards[i];
            string key = PhysicalKey(row.Name, row.SetCode,
                row.CollectorNumber, row.Finish, row.ScryfallId);
            if (!existing.TryGetValue(key, out var slots))
                existing[key] = slots = new List<int>();
            slots.Add(i);
        }

        var imported = new Dictionary<string, Desired>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var item in prepared.Items)
        {
            if (item.Source.Quantity is < 0 or > 99_999)
                throw new InvalidDataException("Import quantity is out of range.");
            string key = PhysicalKey(item.Name, item.SetCode,
                item.CollectorNumber, item.Finish, item.ScryfallId);
            if (imported.TryGetValue(key, out var prior))
            {
                if (!prior.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase) ||
                    !prior.Set.Equals(item.SetCode, StringComparison.OrdinalIgnoreCase) ||
                    !prior.Collector.Equals(item.CollectorNumber, StringComparison.OrdinalIgnoreCase) ||
                    !prior.Finish.Equals(item.Finish, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "Imported rows use the same printing ID but conflicting card metadata.");
                int newQuantity = checked(prior.Quantity + item.Source.Quantity);
                if (newQuantity > 99_999)
                    throw new InvalidDataException("Repeated import printing exceeds 99,999 copies.");
                imported[key] = prior with
                {
                    Quantity = newQuantity,
                    Verified = prior.Verified && item.Verified,
                    Verification = prior.Verified ? item.Status : prior.Verification
                };
            }
            else
                imported.Add(key, new Desired(key, item.Name, item.SetCode,
                    item.CollectorNumber, item.Finish, item.ScryfallId,
                    item.Verified, item.Source.Quantity, item.Status));
        }

        var changes = new List<Line>(imported.Count);
        var removed = new List<int>();
        int matchedExisting = 0;
        foreach (var desired in imported.Values.OrderBy(x => x.Name,
                     StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Key,
                     StringComparer.OrdinalIgnoreCase))
        {
            existing.TryGetValue(desired.Key, out var matches);
            if (matches is { Count: > 1 })
                throw new InvalidDataException("More than one local physical row " +
                    $"matches '{desired.Name}'. Resolve duplicates before reconciling.");
            int current = matches is { Count: 1 }
                ? before.PhysicalCards[matches[0]].Quantity : 0;
            if (matches is { Count: 1 })
            {
                matchedExisting++;
                var local = before.PhysicalCards[matches[0]];
                if (!local.Name.Equals(desired.Name, StringComparison.OrdinalIgnoreCase) ||
                    (desired.Set.Length > 0 && local.SetCode.Length > 0 &&
                     !local.SetCode.Equals(desired.Set, StringComparison.OrdinalIgnoreCase)) ||
                    (desired.Collector.Length > 0 && local.CollectorNumber.Length > 0 &&
                     !local.CollectorNumber.Equals(desired.Collector, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException(
                        "Physical printing metadata conflicts with local record: " + desired.Name);
                if (current != desired.Quantity)
                {
                    if (desired.Quantity == 0) removed.Add(matches[0]);
                    else ((JsonObject)target[matches[0]]!)["qty"] = desired.Quantity;
                }
            }
            else if (desired.Quantity > 0)
            {
                if (target.Count >= 150_000)
                    throw new InvalidDataException("Physical inventory row capacity exceeded.");
                target.Add(new JsonObject
                {
                    ["name"] = desired.Name,
                    ["qty"] = desired.Quantity,
                    ["set"] = desired.Set,
                    ["number"] = desired.Collector,
                    ["finish"] = desired.Finish,
                    ["scryfallId"] = desired.Id
                });
            }

            int delta = desired.Quantity - current;
            string status = delta == 0 ? "Unchanged" :
                desired.Quantity == 0 ? "Removed" :
                current == 0 ? "New" : "Adjusted";
            string print = desired.Set.Length > 0 || desired.Collector.Length > 0
                ? desired.Set.ToUpperInvariant() + " #" + desired.Collector
                : "unspecified printing";
            changes.Add(new Line(desired.Name,
                print + " · " + desired.Finish, current,
                desired.Quantity, delta, status,
                desired.Verified ? "Catalog-verified printing" :
                    desired.Verification + " (not counted for verified set progress)"));
        }
        foreach (int i in removed.OrderByDescending(x => x))
            target.RemoveAt(i);
        bool changed = changes.Any(x => x.Delta != 0);
        byte[] updated = changed ? JsonSerializer.SerializeToUtf8Bytes(root) : source;
        if (changed)
            ValidateConservation(updated, before, "Physical",
                changes.Sum(x => (long)x.Delta));
        // Validate each target against the actual candidate representation,
        // not merely the net-copy delta. This also catches accidental
        // cross-printing merges and duplicate candidate identities.
        var after = NativeBackupReader.Read(updated);
        var candidateRows = new Dictionary<string, (int Count, long Copies)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var row in after.PhysicalCards)
        {
            string key = PhysicalKey(row.Name, row.SetCode,
                row.CollectorNumber, row.Finish, row.ScryfallId);
            candidateRows.TryGetValue(key, out var previous);
            candidateRows[key] = (previous.Count + 1,
                checked(previous.Copies + row.Quantity));
        }
        foreach (var entry in imported.Values)
        {
            candidateRows.TryGetValue(entry.Key, out var result);
            if (result.Count > 1 || result.Copies != entry.Quantity)
                throw new InvalidDataException(
                    "Proposed physical collection does not match imported printing targets.");
        }
        int localOnlyRows = before.PhysicalCards.Count - matchedExisting;
        long localOnlyCopies = before.PhysicalCards
            .Where(x => !imported.ContainsKey(PhysicalKey(x.Name, x.SetCode,
                x.CollectorNumber, x.Finish, x.ScryfallId)))
            .Sum(x => (long)x.Quantity);
        var audit = new ImportAudit(prepared.Items.Count, imported.Count,
            prepared.Items.Count - imported.Count, prepared.Copies,
            prepared.UnresolvedRows, prepared.UnresolvedCopies,
            prepared.Items.Count(x => x.Status.Contains("ambiguous",
                StringComparison.OrdinalIgnoreCase)),
            prepared.Items.Count(x => x.Source.Quantity == 0),
            localOnlyRows, localOnlyCopies, after.PhysicalCopies, true);
        return new Result(updated, changes.AsReadOnly(),
            localOnlyRows, "Physical", audit);
    }

    public static Result Arena(byte[] source,
        IReadOnlyList<NativeExcelCollectionImport.Row> rows)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count is < 1 or > 60_000)
            throw new InvalidDataException("Arena import requires 1–60,000 rows.");
        var root = Root(source, "cards", out var target, out var before);
        var desired = new Dictionary<string, (string Name, int Quantity)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            string name = row.Name.Trim();
            if (name.Length is < 1 or > 255 || name.Any(char.IsControl) ||
                row.Quantity is < 0 or > 99_999)
                throw new InvalidDataException("Invalid Arena import name or quantity.");
            int sum = checked(desired.GetValueOrDefault(name).Quantity + row.Quantity);
            if (sum > 99_999)
                throw new InvalidDataException("Repeated imported Arena card exceeds 99,999 copies.");
            desired[name] = (desired.TryGetValue(name, out var prev)
                ? prev.Name : name, sum);
        }
        var local = new Dictionary<string, List<int>>(
            StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < before.ArenaCards.Count; i++)
        {
            string name = before.ArenaCards[i].Name;
            if (!local.TryGetValue(name, out var slots))
                local[name] = slots = new List<int>();
            slots.Add(i);
        }
        var changes = new List<Line>(desired.Count);
        var removed = new List<int>();
        int matchedExisting = 0;
        foreach (var pair in desired.OrderBy(x => x.Key,
                     StringComparer.OrdinalIgnoreCase))
        {
            local.TryGetValue(pair.Key, out var slots);
            if (slots is { Count: > 1 })
                throw new InvalidDataException(
                    "Duplicate local Arena names targeted by import: " + pair.Key);
            int current = slots is { Count: 1 }
                ? before.ArenaCards[slots[0]].Quantity : 0;
            int imported = pair.Value.Quantity;
            if (slots is { Count: 1 })
            {
                matchedExisting++;
                if (current != imported)
                {
                    if (imported == 0) removed.Add(slots[0]);
                    else ((JsonObject)target[slots[0]]!)["qty"] = imported;
                }
            }
            else if (imported > 0)
            {
                if (target.Count >= 150_000)
                    throw new InvalidDataException("Arena inventory row capacity exceeded.");
                target.Add(new JsonObject
                { ["name"] = pair.Value.Name, ["qty"] = imported });
            }
            int delta = imported - current;
            changes.Add(new Line(pair.Value.Name, "Arena · card name",
                current, imported, delta,
                delta == 0 ? "Unchanged" : imported == 0
                    ? "Removed" : current == 0 ? "New" : "Adjusted",
                "Arena quantity (no physical printing implied)"));
        }
        foreach (int i in removed.OrderByDescending(x => x))
            target.RemoveAt(i);
        bool changed = changes.Any(x => x.Delta != 0);
        byte[] updated = changed ? JsonSerializer.SerializeToUtf8Bytes(root) : source;
        if (changed)
            ValidateConservation(updated, before, "Arena",
                changes.Sum(x => (long)x.Delta));
        // Audit the normalized name targets against the proposed native
        // collection; never mistake extra local-only names for imported rows.
        var after = NativeBackupReader.Read(updated);
        var candidateRows = new Dictionary<string, (int Count, long Copies)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var row in after.ArenaCards)
        {
            candidateRows.TryGetValue(row.Name, out var previous);
            candidateRows[row.Name] = (previous.Count + 1,
                checked(previous.Copies + row.Quantity));
        }
        foreach (var entry in desired)
        {
            candidateRows.TryGetValue(entry.Key, out var result);
            if (result.Count > 1 || result.Copies != entry.Value.Quantity)
                throw new InvalidDataException(
                    "Proposed Arena collection does not match imported name targets.");
        }
        int localOnlyRows = before.ArenaCards.Count - matchedExisting;
        long localOnlyCopies = before.ArenaCards
            .Where(x => !desired.ContainsKey(x.Name))
            .Sum(x => (long)x.Quantity);
        var audit = new ImportAudit(rows.Count, desired.Count,
            rows.Count - desired.Count, rows.Sum(x => (long)x.Quantity),
            0, 0, 0, rows.Count(x => x.Quantity == 0),
            localOnlyRows, localOnlyCopies, after.ArenaCopies, true);
        return new Result(updated, changes.AsReadOnly(),
            localOnlyRows, "Arena", audit);
    }
}
