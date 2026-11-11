using System.Xml.Linq;

internal static class NativeReleaseNotesChecks
{
    public static void Run()
    {
        // CI has already compiled the native desktop and its source checkout.
        // Catch an invalid versioned release note before packaging/uploading
        // a 64 MB self-contained artifact.
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        string? root = null;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName,
                "ArcaneVault.Next.Desktop", "ArcaneVault.Next.Desktop.csproj")))
            {
                root = current.FullName;
                break;
            }
            current = current.Parent;
        }
        if (root is null)
            throw new InvalidOperationException(
                "Native release notes contract: checkout root was not found.");

        var project = XDocument.Load(Path.Combine(root,
            "ArcaneVault.Next.Desktop", "ArcaneVault.Next.Desktop.csproj"));
        string version = project.Descendants("Version").Select(x => x.Value)
            .FirstOrDefault() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(
            version, @"^\d+\.\d+\.\d+$"))
            throw new InvalidOperationException("Native release notes contract: invalid version.");

        string filename = Path.Combine(root, "releases", $"native-v{version}.md");
        if (!File.Exists(filename))
            throw new InvalidOperationException(
                "Native release notes contract: no notes for " + version);
        var headings = File.ReadAllLines(filename)
            .Select(x => x.TrimEnd('\r', ' ', '\t'))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var section in new[] { "Added", "Changed", "Fixed", "Removed" })
            if (!headings.Contains("## " + section))
                throw new InvalidOperationException(
                    $"Native release notes contract: missing ## {section} for {version}.");

        Console.WriteLine($"PASS: native v{version} release notes include Added / Changed / Fixed / Removed");
    }
}
