using System.Text.Json;
using System.Text.Json.Serialization;

namespace RobEveryoneLauncher;

// The launcher's own tiny persisted record of what's currently installed --
// separate from the game's own Application.version, since the launcher has
// to know this *before* the game (or Unity) is even running. Convention:
// InstalledVersion is always the exact GitHub release tag (e.g.
// "v1.0.3-alpha"), matching how the game's own Player Settings Version is
// kept in sync with each release tag.
public class LauncherState
{
    [JsonPropertyName("installedVersion")]
    public string? InstalledVersion { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static LauncherState Load()
    {
        try
        {
            if (!File.Exists(LauncherPaths.StateFilePath)) return new LauncherState();
            string json = File.ReadAllText(LauncherPaths.StateFilePath);
            return JsonSerializer.Deserialize<LauncherState>(json) ?? new LauncherState();
        }
        catch
        {
            // A corrupt/unreadable state file should never block launching --
            // worst case this just re-downloads a version we already have.
            return new LauncherState();
        }
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(LauncherPaths.StateFilePath, json);
    }
}
