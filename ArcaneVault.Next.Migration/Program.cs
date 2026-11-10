using ArcaneVault.Next.Migration;
using System.Text.Json;

static void Print(object data) => Console.WriteLine(JsonSerializer.Serialize(data,
    new JsonSerializerOptions {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    }));

try
{
    if (args.Length == 4 && args[0] == "--stage-backup" && args[2] == "--output")
    {
        // Explicit user-initiated staging output. Never writes installed data.
        Print(OfflineStaging.Create(args[1], args[3]));
        return 0;
    }
    if (args.Length == 2 && args[0] == "--verify-stage")
    {
        Print(OfflineStaging.Verify(args[1]));
        return 0;
    }
    if ((args.Length == 2 || args.Length == 4) && args[0] == "--backup" &&
        (args.Length == 2 || args[2] == "--catalog"))
    {
        Print(MigrationPreview.Analyze(args[1], args.Length == 4 ? args[3] : null));
        return 0;
    }

    Console.Error.WriteLine("Usage: --backup <export.json> [--catalog <catalog.sqlite>] OR --stage-backup <full-export.json> --output <NEW.sqlite> OR --verify-stage <staged.sqlite>");
    return 2;
}
catch
{
    // Never echo backup contents, card names, deck names, file paths or exception
    // messages. A failed staging job cannot replace an existing destination.
    Console.Error.WriteLine("Migration diagnostics failed: unsupported or invalid input, staging failure, or verification error. The installed Arcane Vault data was not changed.");
    return 2;
}
