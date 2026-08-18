namespace Archlab.Desktop;

/// <summary>
/// The window icon. WinForms does not adopt the project's ApplicationIcon for a form, so the
/// title bar and Alt+Tab would show the generic default without this.
/// </summary>
internal static class AppIcon
{
    private static readonly Lazy<Icon?> Cached = new(() =>
    {
        try
        {
            // The app icon is a Win32 resource on the executable itself, so there is nothing
            // extra to ship and it stays in step with ApplicationIcon.
            return Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath);
        }
        catch (Exception)
        {
            // A missing or unreadable icon is not worth failing a sale over; the form falls
            // back to the WinForms default.
            return null;
        }
    });

    public static Icon? Load() => Cached.Value;
}
