using System.Runtime.InteropServices;

namespace RobEveryoneLauncher;

// Central place for "which OS am I on" -- ties into issue #43 (cross-platform
// builds): once release assets exist per-platform, this is what picks the
// right one. Until then, UpdateService falls back to "just one zip asset
// exists, use it" so the launcher still works against today's Windows-only
// releases.
public enum GamePlatform
{
    Windows,
    MacOS,
    Linux,
}

public static class PlatformInfo
{
    public static GamePlatform Current { get; } = Detect();

    private static GamePlatform Detect()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return GamePlatform.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return GamePlatform.MacOS;
        return GamePlatform.Linux;
    }

    // The release-asset naming convention this launcher expects once #43
    // ships per-platform builds: RobEveryone-<tag>-<suffix>.zip.
    public static string AssetSuffix => Current switch
    {
        GamePlatform.Windows => "win",
        GamePlatform.MacOS => "mac",
        GamePlatform.Linux => "linux",
        _ => "win",
    };

    // What to look for inside the extracted Game/ folder to actually launch
    // it. Unity's Standalone build layout differs per platform: Windows
    // drops a top-level <ProductName>.exe, macOS produces a <ProductName>.app
    // bundle (itself a directory), Linux drops an extensionless executable
    // with the executable bit set.
    public static bool IsCandidateExecutable(string fileName) => Current switch
    {
        GamePlatform.Windows => fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase),
        GamePlatform.MacOS => fileName.EndsWith(".app", StringComparison.OrdinalIgnoreCase),
        // No reliable extension on Linux -- caller additionally checks the
        // executable bit before treating a match as launchable.
        GamePlatform.Linux => !fileName.Contains('.'),
        _ => false,
    };
}
