using System.Text;
using System.Text.RegularExpressions;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Loss-aware MTG Arena/plain-text decklist interchange. Only known editable
/// sections are accepted. Sideboards and multiple commanders are rejected
/// rather than being silently dropped from the native schema-2 deck library.
/// Import never changes owned cards or any database before explicit Save.
/// </summary>
public static class NativeDeckTextExchange
{
    public sealed record Imported(
        string MainboardText, string Commander, int CardNames,
        int MainboardCopies, bool RemovedPrintingAnnotations,
        string SourceKind);

    private enum Section { Main, Commander, Unsupported }

    private static readonly Regex Counted = new(
        @"^(?<qty>[0-9]{1,4})\s+(?<name>.+)$", RegexOptions.Compiled |
        RegexOptions.CultureInvariant);
    private static readonly Regex ArenaPrinting = new(
        @"^(?<name>.+?)\s+\((?<set>[a-zA-Z0-9]{2,8})\)\s+(?<number>[0-9]{1,6}[a-zA-Z]?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static Imported Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0 || text.Length > 64_000)
            throw new InvalidDataException("Deck import text must be 1–64,000 characters.");

        var section = Section.Main;
        bool foundHeader = false, sawPrinting = false;
        var main = new List<string>();
        var commanders = new List<string>();
        int lines = 0;
        foreach (string raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            string line = raw.Trim().TrimStart('\uFEFF');
            if (line.Length == 0) continue;
            if (++lines > 550)
                throw new InvalidDataException("Deck import exceeds 550 nonempty rows.");

            switch (line.ToLowerInvariant())
            {
                case "deck":
                case "mainboard":
                case "main deck":
                    section = Section.Main;
                    foundHeader = true;
                    continue;
                case "commander":
                case "commanders":
                    section = Section.Commander;
                    foundHeader = true;
                    continue;
                case "sideboard":
                case "side":
                case "companion":
                case "maybeboard":
                case "considering":
                case "wishboard":
                    section = Section.Unsupported;
                    foundHeader = true;
                    continue;
            }

            if (section == Section.Unsupported)
                throw new InvalidDataException(
                    "This export contains sideboard, companion or maybeboard cards. " +
                    "The native editor cannot preserve those sections yet; import was canceled.");
            var match = Counted.Match(line);
            if (!match.Success || !int.TryParse(match.Groups["qty"].Value, out int count) ||
                count is < 1 or > 250)
                throw new InvalidDataException(
                    "Unsupported decklist line " + lines + ": " +
                    line[..Math.Min(line.Length, 96)]);
            string name = match.Groups["name"].Value.Trim();
            var arena = ArenaPrinting.Match(name);
            if (arena.Success)
            {
                name = arena.Groups["name"].Value.Trim();
                sawPrinting = true;
            }
            if (name.Length is < 1 or > 255 || name.Any(char.IsControl))
                throw new InvalidDataException("Imported deck has an invalid card name.");
            if (section == Section.Commander)
            {
                if (count != 1)
                    throw new InvalidDataException(
                        "A Commander section must have exactly one copy of its commander.");
                commanders.Add(name);
            }
            else
                main.Add(count + " " + name);
        }

        if (commanders.Count > 1)
            throw new InvalidDataException(
                "This exported deck has multiple commanders. The native editor " +
                "does not yet support Partners or other multi-commander configurations; " +
                "no cards have been imported.");
        if (main.Count == 0)
            throw new InvalidDataException("No mainboard cards were found in the decklist.");

        // Use the same constraints and duplicate-merging as native deck save.
        var entries = NativeDeckEditing.ParseMainboard(string.Join("\n", main));
        return new Imported(
            string.Join("\n", entries.Select(x => x.Quantity + " " + x.Name)),
            commanders.SingleOrDefault() ?? "", entries.Count,
            entries.Sum(x => x.Quantity), sawPrinting,
            foundHeader ? "Sectioned Arena text" : "Plain count/name text");
    }

    /// <summary>
    /// Export an Arena-import-compatible name/quantity text list. Deliberately
    /// does not claim to preserve imported printing IDs, set codes, sideboards,
    /// deck revisions, folders or favorites in the external interchange format.
    /// </summary>
    public static string Export(string mainboardText, string commander)
    {
        ArgumentNullException.ThrowIfNull(commander);
        if (commander.Length > 255 || commander.Any(char.IsControl))
            throw new InvalidDataException("Cannot export an invalid commander name.");
        var main = NativeDeckEditing.ParseMainboard(mainboardText);
        if (main.Count == 0)
            throw new InvalidDataException("Cannot export an empty deck.");
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(commander))
        {
            builder.Append("Commander\n1 ").Append(commander.Trim()).Append("\n\n");
        }
        builder.Append("Deck\n");
        foreach (var entry in main)
            builder.Append(entry.Quantity).Append(' ').Append(entry.Name).Append('\n');
        return builder.ToString();
    }
}
