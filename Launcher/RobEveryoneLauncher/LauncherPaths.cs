namespace RobEveryoneLauncher;

// Everything the launcher reads/writes lives next to its own executable --
// deliberately not a per-user app-data folder, since this is meant to be a
// single self-contained "unzip and run" folder a player can put wherever
// they want (a USB drive, a shared LAN folder, etc.), same spirit as how
// the game's own zip releases work today.
public static class LauncherPaths
{
    public static string RootDir { get; } = AppContext.BaseDirectory;

    public static string GameDir => Path.Combine(RootDir, "Game");

    public static string StateFilePath => Path.Combine(RootDir, "launcher-state.json");

    // Used while downloading/extracting so a crash or killed process mid-
    // update never leaves Game/ in a half-overwritten, unlaunchable state --
    // UpdateService extracts here first, then atomically swaps it in.
    public static string StagingDir => Path.Combine(RootDir, "Game.staging");
}
