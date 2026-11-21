using System.Runtime.CompilerServices;

// Only the dedicated regression executable can arm fault-injection callbacks.
// Production code has no public switch, environment variable or UI access to them.
[assembly: InternalsVisibleTo("ArcaneVault.Next.Tests")]
