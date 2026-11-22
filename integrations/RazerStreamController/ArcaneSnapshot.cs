using System.Text.Json;

namespace Loupedeck.ArcaneVaultRazer;

/// <summary>
/// Read-only projection of Arcane Vault's existing Xeneon/Stream Deck snapshot.
/// Missing/partial updates are normal: the native exporter replaces the file
/// atomically and may publish records before image metadata is available.
/// No user collection, game log or match-history database is opened here.
/// </summary>
internal static class ArcaneSnapshot
{
    internal sealed record Deck(string Id, string Name, int Wins, int Losses, string Format,
        string ImagePath);
    internal sealed record State(IReadOnlyList<Deck> Decks, string Rank, string Value,
        string Updated, string Status);

    private static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ArcaneVault");

    internal static string SnapshotPath => Path.Combine(Root, "streamdeck.json");

    private static readonly object Gate = new();
    private static string selectedDeckId = "";

    internal static State Read()
    {
        if (!File.Exists(SnapshotPath))
            return new(Array.Empty<Deck>(), "—", "—", "—",
                "Arcane Vault snapshot not found. Start Arcane Vault.");
        try
        {
            using var data = JsonDocument.Parse(File.ReadAllText(SnapshotPath));
            var root = data.RootElement;
            var decks = new List<Deck>();
            if (root.TryGetProperty("decks", out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var deck in arr.EnumerateArray().Take(1000))
                {
                    string id = GetString(deck, "id");
                    string name = GetString(deck, "name");
                    if (id.Length == 0 || name.Length == 0) continue;
                    decks.Add(new Deck(id, name, GetInt(deck, "wins"), GetInt(deck, "losses"),
                        GetString(deck, "format"), GetString(deck, "keyImagePath")));
                }
            }
            string rank = "—";
            if (root.TryGetProperty("rank", out var r) && r.ValueKind == JsonValueKind.Object)
            {
                string cls = GetString(r, "constructedClass");
                int level = GetInt(r, "constructedLevel");
                rank = cls.Length == 0 ? "—" : cls + (level > 0 ? " " + level : "");
            }
            string value = "—";
            if (root.TryGetProperty("collection", out var c) &&
                c.ValueKind == JsonValueKind.Object &&
                c.TryGetProperty("value", out var val) &&
                val.ValueKind == JsonValueKind.Number)
                value = "$" + val.GetDecimal().ToString("N0");
            return new(decks, rank, value, GetString(root, "publishedUtc"), "Connected");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            JsonException or FormatException or OverflowException)
        {
            return new(Array.Empty<Deck>(), "—", "—", "—",
                "Snapshot unavailable: " + ex.Message);
        }
    }

    private static string GetString(JsonElement obj, string key) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static int GetInt(JsonElement obj, string key) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)
            ? number : 0;

    internal static Deck? Selected(State state)
    {
        lock (Gate)
        {
            return state.Decks.FirstOrDefault(d => d.Id == selectedDeckId) ??
                state.Decks.FirstOrDefault();
        }
    }

    internal static Deck? Move(int diff)
    {
        lock (Gate)
        {
            var state = Read();
            if (state.Decks.Count == 0) return null;
            int index = state.Decks.ToList().FindIndex(d => d.Id == selectedDeckId);
            if (index < 0) index = 0;
            else index = ((index + diff) % state.Decks.Count + state.Decks.Count) %
                state.Decks.Count;
            selectedDeckId = state.Decks[index].Id;
            return state.Decks[index];
        }
    }

    internal static void ResetSelection()
    {
        lock (Gate) selectedDeckId = "";
    }

    internal static void RequestRefresh()
    {
        // This request is consumed by the existing native exporter. It does
        // not touch match records or persist any selection in the user workspace.
        Directory.CreateDirectory(Root);
        File.WriteAllText(Path.Combine(Root, "streamdeck-refresh.request"),
            DateTimeOffset.UtcNow.ToString("O"));
    }
}
