namespace CapsLockRemap;

internal static class AppIcon
{
    // Caller owns the returned icon. Shared source for windows and the future tray host.
    public static Icon Load()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("CapsLockRemap.AppIcon.ico")
            ?? throw new InvalidOperationException("Application icon resource is missing.");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}
