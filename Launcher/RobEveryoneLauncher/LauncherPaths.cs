namespace RobEveryoneLauncher;

// Everything the launcher reads/writes lives next to its own executable --
// deliberately not a per-user app-data folder, since this is meant to be a
// single self-contained "unzip and run" folder a player can put wherever
// they want (a USB drive, a shared LAN folder, etc.), same spirit as how
// the game's own zip releases work today.
public static class LauncherPaths
{
    public static string RootDir { get; } = AppContext.BaseDirectory;

    // "Rob Everyone", not the generic "Game" this used to be -- so what a
    // player actually sees next to the launcher .exe after the first run
    // is self-explanatory (RobEveryoneLauncher.exe + a "Rob Everyone"
    // folder), not an unlabeled "Game" folder.
    public static string GameDir => Path.Combine(RootDir, "Rob Everyone");

    public static string StateFilePath => Path.Combine(RootDir, "launcher-state.json");

    // The update-check failure path used to swallow the real exception
    // entirely (a bare `catch { }`), so a genuine failure and "you're
    // actually offline" looked identical on screen with zero way to tell
    // them apart. Written next to the exe for the same "self-contained
    // folder" reason as everything else here.
    public static string ErrorLogPath => Path.Combine(RootDir, "launcher-error.log");

    // Used while downloading/extracting so a crash or killed process mid-
    // update never leaves GameDir in a half-overwritten, unlaunchable state
    // -- UpdateService extracts here first, then atomically swaps it in.
    public static string StagingDir => Path.Combine(RootDir, "Rob Everyone.staging");
}
