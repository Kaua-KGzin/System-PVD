using System.Text.Json;

namespace Archlab.Desktop;

/// <summary>
/// The window mode survives a restart. A till set to fullscreen has to come back up that way
/// after a reboot, without someone pressing F11 every morning.
/// </summary>
internal sealed class UiPreferences
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string FilePath => Path.Combine(Program.DataDirectory, "ui.json");

    public bool FullScreen { get; set; }

    public static UiPreferences Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(FilePath))
                    ?? new UiPreferences();
            }
        }
        catch (Exception)
        {
            // A corrupt preference file is not a reason to refuse to open the till; the window
            // just starts in its default mode.
        }

        return new UiPreferences();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception)
        {
            // Losing the preference costs one keypress next launch. Failing here would cost a sale.
        }
    }
}
