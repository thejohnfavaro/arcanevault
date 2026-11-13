namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Layout-only math in WPF device-independent pixels. Tests exercise both
/// desktop and smaller/laptop work areas without needing a display server.
/// </summary>
public static class NativeCardDetailsLayout
{
    public readonly record struct Size(double Width, double Height, double MinWidth, double MinHeight);

    public static Size InitialSize(double availableWidth, double availableHeight)
    {
        if (!double.IsFinite(availableWidth) || availableWidth <= 0 ||
            !double.IsFinite(availableHeight) || availableHeight <= 0)
            return new Size(1060, 755, 720, 500);

        // Leave clearance for window frame/taskbar and allow flexible
        // independent scrolling of ownership, metadata and Oracle text.
        double width = Math.Min(1160, Math.Max(450, availableWidth - 36));
        double height = Math.Min(920, Math.Max(420, availableHeight - 34));
        return new Size(width, height,
            Math.Min(780, width), Math.Min(560, height));
    }

    public static double ArtworkHeight(double contentHeight)
    {
        if (!double.IsFinite(contentHeight) || contentHeight <= 0)
            return 300;
        // Reserve ownership and artwork status space in the left pane so
        // opening Card Details never hides those under a large fixed picture.
        return Math.Clamp(contentHeight - 150, 185, 445);
    }
}
