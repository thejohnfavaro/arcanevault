using ArcaneVault.Next.Migration;
using System.Text.Json;

if (args.Length is not (2 or 4) || args[0] != "--backup" ||
    (args.Length == 4 && args[2] != "--catalog"))
{
    Console.Error.WriteLine("Usage: dotnet run --project ArcaneVault.Next.Migration -- --backup <export.json> [--catalog <scryfall-catalog.sqlite>]");
    return 2;
}

try
{
    var report = MigrationPreview.Analyze(args[1], args.Length == 4 ? args[3] : null);
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    }));
    // Warn/unresolved entries are a report, not a mutation or parse failure.
    return 0;
}
catch
{
    // Never echo source names, file paths, database records or raw exception
    // messages; these can contain user collection details.
    Console.Error.WriteLine("Migration preview failed: unsupported, invalid, or unreadable input. No data changed.");
    return 2;
}
