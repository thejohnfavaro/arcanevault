using System.Text.RegularExpressions;

namespace ArcaneVault.Next.Domain;

/// <summary>
/// Only explicit, full card references in an AI answer may become interactive.
/// Keep the compiled pattern in a non-UI assembly so CI executes it rather
/// than only compiling a WPF type whose initializer may fail at runtime.
/// </summary>
public static class AiReviewCardReferenceSyntax
{
    public static Regex Pattern { get; } = new(
        @"\[\[card:([^\[\]\r\n]{2,150})\]\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
