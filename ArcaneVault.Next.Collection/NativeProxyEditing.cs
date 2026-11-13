using ArcaneVault.Next.Domain;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// User-declared proxy/third-party copies are independent of verified physical
/// printings and Arena holdings. The complete native backup contains an optional
/// proxyCards array; no proxy is ever written to physicalCards, so valuation,
/// set progress, and owned-printing counts are unaffected by construction.
/// </summary>
public static class NativeProxyEditing
{
    public sealed record ProxyRow(string Name, int Quantity)
    {
        public string Kind => "Proxy";
        public string Valuation => "Excluded — no price lookup";
    }

    private const int MaxRows = 50_000;
    private const int MaxQuantity = 99_999;

    public static IReadOnlyList<ProxyRow> Read(byte[] completeBackup)
    {
        ArgumentNullException.ThrowIfNull(completeBackup);
        _ = NativeBackupReader.Read(completeBackup);
        using var document = JsonDocument.Parse(completeBackup);
        var root = document.RootElement;
        if (!root.TryGetProperty("proxyCards", out var cards))
            return Array.Empty<ProxyRow>();
        if (cards.ValueKind != JsonValueKind.Array ||
            cards.GetArrayLength() > MaxRows)
            throw new InvalidDataException("Invalid proxy inventory section.");
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<ProxyRow>(cards.GetArrayLength());
        foreach (var item in cards.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("name", out var nameValue) ||
                nameValue.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("qty", out var qtyValue) ||
                qtyValue.ValueKind != JsonValueKind.Number ||
                !qtyValue.TryGetInt32(out int quantity) ||
                quantity is < 1 or > MaxQuantity ||
                !item.TryGetProperty("kind", out var kindValue) ||
                kindValue.ValueKind != JsonValueKind.String ||
                kindValue.GetString() != "Proxy")
                throw new InvalidDataException("Invalid proxy row. Proxies require name, qty, and kind=Proxy.");
            string name = nameValue.GetString()!.Trim();
            ValidateName(name);
            if (!found.Add(name))
                throw new InvalidDataException("Duplicate proxy card name; no changes were applied.");
            rows.Add(new ProxyRow(name, quantity));
        }
        return rows.AsReadOnly();
    }

    /// <summary>
    /// Legacy/older full exports have no proxyCards field. Retain the user's
    /// current proxy section when importing one of those snapshots, rather
    /// than silently erasing it. An explicit proxyCards array remains
    /// authoritative, and an explicit whole-workspace fresh start may clear it.
    /// </summary>
    public static byte[] CarryForwardIfUnspecified(
        byte[] currentBackup, byte[] incomingBackup)
    {
        ArgumentNullException.ThrowIfNull(currentBackup);
        ArgumentNullException.ThrowIfNull(incomingBackup);
        using var current = JsonDocument.Parse(currentBackup);
        using var incoming = JsonDocument.Parse(incomingBackup);
        if (!current.RootElement.TryGetProperty("proxyCards", out var oldRows) ||
            incoming.RootElement.TryGetProperty("proxyCards", out _) ||
            oldRows.ValueKind != JsonValueKind.Array ||
            oldRows.GetArrayLength() == 0)
            return incomingBackup;
        // Fail closed if the existing ledger is malformed before copying it.
        _ = Read(currentBackup);
        var node = JsonNode.Parse(incomingBackup) as JsonObject ??
            throw new InvalidDataException("Invalid imported full backup.");
        node["proxyCards"] = JsonNode.Parse(oldRows.GetRawText());
        byte[] merged = Encoding.UTF8.GetBytes(node.ToJsonString());
        _ = Read(merged);
        return merged;
    }

    public static byte[] SetQuantity(byte[] completeBackup, string cardName,
        int newQuantity)
    {
        ArgumentNullException.ThrowIfNull(completeBackup);
        string name = (cardName ?? "").Trim();
        ValidateName(name);
        if (newQuantity is < 0 or > MaxQuantity)
            throw new ArgumentOutOfRangeException(nameof(newQuantity),
                "Proxy quantity must be 0–99,999 (0 removes it).");
        var previous = NativeBackupReader.Read(completeBackup);
        var oldProxies = Read(completeBackup);
        var old = oldProxies.FirstOrDefault(x =>
            x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (old is null && newQuantity == 0)
            throw new InvalidDataException("That proxy is not recorded.");
        if (old?.Quantity == newQuantity)
            throw new InvalidDataException("That proxy quantity is already recorded.");
        if (old is null && oldProxies.Count >= MaxRows)
            throw new InvalidDataException("Proxy inventory row limit reached.");

        var root = JsonNode.Parse(completeBackup) as JsonObject ??
            throw new InvalidDataException("Invalid complete native backup.");
        var proxies = root["proxyCards"] as JsonArray ?? new JsonArray();
        var updated = new JsonArray();
        bool replaced = false;
        foreach (var item in proxies)
        {
            string storedName = item?["name"]?.GetValue<string>() ?? "";
            if (storedName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                replaced = true;
                if (newQuantity > 0)
                    updated.Add(new JsonObject
                    {
                        ["name"] = old!.Name,
                        ["qty"] = newQuantity,
                        ["kind"] = "Proxy"
                    });
            }
            else
            {
                updated.Add(item!.DeepClone());
            }
        }
        if (!replaced && newQuantity > 0)
            updated.Add(new JsonObject
            {
                ["name"] = name,
                ["qty"] = newQuantity,
                ["kind"] = "Proxy"
            });
        root["proxyCards"] = updated;
        byte[] output = Encoding.UTF8.GetBytes(root.ToJsonString());
        var after = NativeBackupReader.Read(output);
        var resultProxies = Read(output);

        if (!previous.PhysicalCards.SequenceEqual(after.PhysicalCards) ||
            !previous.ArenaCards.SequenceEqual(after.ArenaCards) ||
            previous.Decks != after.Decks ||
            previous.DeckRevisions != after.DeckRevisions ||
            previous.PreferredArtworks != after.PreferredArtworks ||
            oldProxies.Count + (old is null ? 1 : newQuantity == 0 ? -1 : 0) !=
                resultProxies.Count ||
            resultProxies.FirstOrDefault(x =>
                x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Quantity !=
                (newQuantity == 0 ? null : newQuantity))
            throw new InvalidDataException(
                "Proxy edit failed collection conservation checks. No changes were applied.");
        foreach (var item in oldProxies.Where(x =>
            !x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            if (!resultProxies.Contains(item))
                throw new InvalidDataException(
                    "Proxy edit changed another record. No changes were applied.");
        return output;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 ||
            name.Any(char.IsControl))
            throw new ArgumentException(
                "Proxy name must be 1–255 visible characters.", nameof(name));
    }
}
