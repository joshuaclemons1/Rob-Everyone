using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace RobEveryoneLauncher;

public enum UpdatePhase { Checking, UpToDate, Downloading, Installing, Launching, Error }

public class UpdateProgress
{
    public UpdatePhase Phase { get; init; }
    public string Message { get; init; } = "";
    public double Fraction { get; init; } // 0..1, meaningful during Downloading/Installing
}

// The whole "check GitHub, download the right build, install it, hand back
// what to launch" flow -- MainWindow just reports UpdateProgress to the UI
// and does nothing else. Fails open the same way the old in-game
// UpdateChecker did: a broken network check should never block someone who
// already has a working local install from playing.
public class UpdateService
{
    // Deliberately NOT /releases/latest -- that endpoint excludes
    // prerelease/draft releases entirely, and every release this project
    // has published so far (all the alpha-vX.X.X tags) is marked
    // prerelease, so it 404s every single time. Confirmed live while
    // building this: the old in-game UpdateChecker used /releases/latest
    // too, which means it silently never found an update the whole time
    // it existed (it fails open on any non-success response, so nobody
    // would have noticed). /releases (the list endpoint) returns every
    // release including prereleases, newest first. per_page=10, not 1 --
    // GetLatestReleaseAsync below now has to skip past any release that
    // isn't actually a game release (e.g. the launcher's own), so it
    // needs more than just the single newest to search through.
    private const string ReleasesUrl = "https://api.github.com/repos/joshuaclemons1/Rob-Everyone/releases?per_page=10";

    // Every real game release is tagged alpha-vX.X.X. Confirmed real risk
    // otherwise: this repo also carries the launcher's own separate,
    // standalone release (see docs/stages/launcher-setup.md), and without
    // this filter, publishing literally anything else on the repo newer
    // than the last game release would make GetLatestReleaseAsync grab
    // that instead and try to install it as if it were a game update.
    private const string GameReleaseTagPrefix = "alpha-v";

    // Windows companion executables Unity's build drops alongside the real
    // game .exe -- never the thing we actually want to launch.
    private static readonly string[] NonGameWindowsExeNames =
    {
        "unitycrashhandler64.exe", "unitycrashhandler32.exe", "unitycrashhandler.exe",
    };

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        // Mandatory -- GitHub's API outright rejects any request with no
        // User-Agent header (403), authenticated or not. Same requirement
        // the old in-game UpdateChecker had to work around.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RobEveryoneLauncher");
        return client;
    }

    // Returns the path to launch, or null if there's genuinely nothing
    // playable (first run, offline, no build for this platform yet).
    public async Task<string?> RunAsync(IProgress<UpdateProgress> progress, CancellationToken ct)
    {
        LauncherState state = LauncherState.Load();

        progress.Report(new UpdateProgress { Phase = UpdatePhase.Checking, Message = "Checking for updates..." });

        GitHubRelease? release = null;
        try
        {
            release = await GetLatestReleaseAsync(ct);
        }
        catch
        {
            // Network hiccup, rate limit, malformed response -- fall through
            // to the "fails open" handling below rather than surfacing this.
        }

        if (release == null)
        {
            string? existing = FindGameExecutable();
            if (existing != null)
            {
                progress.Report(new UpdateProgress { Phase = UpdatePhase.Launching, Message = "Offline -- launching your current install..." });
                return existing;
            }
            progress.Report(new UpdateProgress
            {
                Phase = UpdatePhase.Error,
                Message = "Couldn't reach GitHub and no local install exists yet. Check your connection and try again.",
            });
            return null;
        }

        // "Already have this tag" is deliberately separate from "found
        // something launchable" below -- on a platform with no build
        // published yet, the tag we already downloaded will never produce
        // a launchable executable, and re-downloading the same unusable
        // zip every single run just to re-confirm that would be a waste.
        // Only re-download when the tag itself has actually changed.
        bool alreadyHaveThisTag = state.InstalledVersion == release.TagName;
        if (!alreadyHaveThisTag)
        {
            GitHubAsset? asset = SelectAssetForCurrentPlatform(release);
            if (asset == null)
            {
                string? existing = FindGameExecutable();
                if (existing != null)
                {
                    progress.Report(new UpdateProgress
                    {
                        Phase = UpdatePhase.Launching,
                        Message = $"No {PlatformInfo.Current} build in the latest release yet -- launching your current install...",
                    });
                    return existing;
                }
                progress.Report(new UpdateProgress
                {
                    Phase = UpdatePhase.Error,
                    Message = $"No {PlatformInfo.Current} build has been published yet.",
                });
                return null;
            }

            await DownloadAndInstallAsync(asset, progress, ct);
            state.InstalledVersion = release.TagName;
            state.Save();
        }

        string? exe = FindGameExecutable();
        if (exe == null)
        {
            // We have the latest tag's assets on disk, but nothing
            // launchable came out of them for this platform (its zip is
            // for a different OS, or something extracted wrong). Report
            // this as its own case rather than silently returning null --
            // and crucially, state.InstalledVersion is already saved, so
            // this doesn't re-download next run either.
            progress.Report(new UpdateProgress
            {
                Phase = UpdatePhase.Error,
                Message = $"{release.TagName} is downloaded, but has no {PlatformInfo.Current}-launchable build inside it.",
            });
            return null;
        }

        string message = alreadyHaveThisTag ? $"Up to date ({release.TagName}) -- launching..." : "Launching...";
        progress.Report(new UpdateProgress { Phase = UpdatePhase.Launching, Message = message, Fraction = 1.0 });
        return exe;
    }

    private static async Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await Http.GetAsync(ReleasesUrl, ct);
        if (!response.IsSuccessStatusCode) return null;
        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        List<GitHubRelease>? releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, cancellationToken: ct);
        return releases?.FirstOrDefault(r => r.TagName.StartsWith(GameReleaseTagPrefix, StringComparison.OrdinalIgnoreCase));
    }

    // Prefers an asset carrying this platform's suffix (RobEveryone-<tag>-win.zip
    // etc -- the convention issue #43 introduces); falls back to "there's
    // exactly one .zip asset" so this still works against today's
    // Windows-only, unsuffixed releases. Gives up (null) only if there are
    // multiple .zip assets and none match this platform.
    private static readonly string[] AllPlatformSuffixes = { "win", "mac", "linux" };

    internal static GitHubAsset? SelectAssetForCurrentPlatform(GitHubRelease release)
    {
        List<GitHubAsset> zips = release.Assets
            .Where(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .ToList();

        GitHubAsset? suffixed = zips.FirstOrDefault(a =>
            a.Name.Contains($"-{PlatformInfo.AssetSuffix}.zip", StringComparison.OrdinalIgnoreCase));
        if (suffixed != null) return suffixed;

        // No exact match for this platform. Only fall back to "there's
        // exactly one zip, grab it" when that asset carries no platform
        // marker at all -- today's actual releases are unsuffixed Windows
        // builds (RobEveryone-<tag>.zip), so this keeps working against
        // those. If every asset is marked for some *other* platform, there's
        // genuinely nothing to install here rather than guessing wrong and
        // downloading a build that can't run.
        bool IsUnmarked(GitHubAsset a) =>
            !AllPlatformSuffixes.Any(s => a.Name.Contains($"-{s}.zip", StringComparison.OrdinalIgnoreCase));

        return zips.Count == 1 && IsUnmarked(zips[0]) ? zips[0] : null;
    }

    private static async Task DownloadAndInstallAsync(GitHubAsset asset, IProgress<UpdateProgress> progress, CancellationToken ct)
    {
        string tempZip = Path.Combine(Path.GetTempPath(), $"robeveryone-update-{Guid.NewGuid():N}.zip");
        try
        {
            using (HttpResponseMessage response = await Http.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                long total = response.Content.Headers.ContentLength ?? asset.Size;
                await using Stream source = await response.Content.ReadAsStreamAsync(ct);
                await using FileStream dest = File.Create(tempZip);

                byte[] buffer = new byte[81920];
                long downloaded = 0;
                int read;
                // A read is one 80KB chunk -- reporting every single one is
                // thousands of UI updates a second on a fast connection, for
                // no visible benefit. Throttled to ~10/sec (plenty smooth
                // for a progress bar), with the 100%-of-download point
                // always reported regardless of timing so the bar visibly
                // reaches "download done" before Installing starts.
                var reportClock = Stopwatch.StartNew();
                TimeSpan reportInterval = TimeSpan.FromMilliseconds(100);
                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    await dest.WriteAsync(buffer.AsMemory(0, read), ct);
                    downloaded += read;

                    bool isLastChunk = total > 0 && downloaded >= total;
                    if (reportClock.Elapsed < reportInterval && !isLastChunk) continue;
                    reportClock.Restart();

                    // Download fills the first 90% of the bar, install the
                    // last 10% -- extraction has no natural progress signal
                    // of its own, so it isn't worth a finer split than that.
                    double fraction = total > 0 ? (double)downloaded / total * 0.9 : 0;
                    string totalLabel = total > 0 ? $"{total / 1_048_576}MB" : "?";
                    progress.Report(new UpdateProgress
                    {
                        Phase = UpdatePhase.Downloading,
                        Message = $"Downloading {asset.Name} ({downloaded / 1_048_576}MB / {totalLabel})...",
                        Fraction = fraction,
                    });
                }
            }

            progress.Report(new UpdateProgress { Phase = UpdatePhase.Installing, Message = "Installing...", Fraction = 0.9 });
            InstallFromZip(tempZip);
            progress.Report(new UpdateProgress { Phase = UpdatePhase.Installing, Message = "Installed.", Fraction = 1.0 });
        }
        finally
        {
            if (File.Exists(tempZip)) File.Delete(tempZip);
        }
    }

    // Extracts into a staging folder first and only swaps it in once that
    // fully succeeds -- a crash or killed process mid-update should never
    // leave Game/ half-overwritten and unplayable. The previous install is
    // kept as Game.old until the swap completes, then removed.
    private static void InstallFromZip(string zipPath)
    {
        if (Directory.Exists(LauncherPaths.StagingDir)) Directory.Delete(LauncherPaths.StagingDir, recursive: true);
        Directory.CreateDirectory(LauncherPaths.StagingDir);
        ZipFile.ExtractToDirectory(zipPath, LauncherPaths.StagingDir);

        string? oldBackup = null;
        if (Directory.Exists(LauncherPaths.GameDir))
        {
            oldBackup = LauncherPaths.GameDir + ".old";
            if (Directory.Exists(oldBackup)) Directory.Delete(oldBackup, recursive: true);
            Directory.Move(LauncherPaths.GameDir, oldBackup);
        }

        Directory.Move(LauncherPaths.StagingDir, LauncherPaths.GameDir);

        if (oldBackup != null) Directory.Delete(oldBackup, recursive: true);
    }

    // Walks Game/ looking for something launchable. Doesn't assume a fixed
    // nesting depth -- whether the release zip puts the build straight at
    // the zip root or one folder deep depends on how it was zipped.
    public static string? FindGameExecutable()
    {
        if (!Directory.Exists(LauncherPaths.GameDir)) return null;

        string? topLevel = FindExecutableIn(LauncherPaths.GameDir, SearchOption.TopDirectoryOnly);
        return topLevel ?? FindExecutableIn(LauncherPaths.GameDir, SearchOption.AllDirectories);
    }

    private static string? FindExecutableIn(string root, SearchOption searchOption)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(root, "*", searchOption))
        {
            string name = Path.GetFileName(path);
            if (!PlatformInfo.IsCandidateExecutable(name)) continue;

            switch (PlatformInfo.Current)
            {
                case GamePlatform.Windows when File.Exists(path):
                    if (NonGameWindowsExeNames.Contains(name.ToLowerInvariant())) continue;
                    return path;
                case GamePlatform.MacOS when Directory.Exists(path):
                    return path; // .app bundle
                case GamePlatform.Linux when File.Exists(path) && HasExecuteBit(path):
                    return path;
            }
        }
        return null;
    }

    // GetUnixFileMode has no meaning on Windows -- guarded internally so
    // this is safe to call from any platform regardless of caller
    // discipline, rather than trusting every call site to only reach here
    // on Linux/macOS.
    private static bool HasExecuteBit(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return false;
        return (File.GetUnixFileMode(path) & UnixFileMode.UserExecute) != 0;
    }

    public static void LaunchGame(string executablePath)
    {
        var startInfo = new ProcessStartInfo();

        if (PlatformInfo.Current == GamePlatform.MacOS && executablePath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            // A .app is a bundle/directory, not directly executable -- `open`
            // is the standard way to launch one, same as double-clicking it
            // in Finder. Needs the shell (`open` is a separate helper
            // process Steam has no reason to know about either way, so
            // this path doesn't affect the overlay-handoff reasoning below).
            startInfo.UseShellExecute = true;
            startInfo.FileName = "open";
            startInfo.ArgumentList.Add(executablePath);
        }
        else
        {
            // UseShellExecute = false (the default) -- launches the game as
            // a direct child process (CreateProcess) instead of routing
            // through the OS shell. Matters for the Steam overlay: if a
            // player adds this launcher (not the game .exe) as a Non-Steam
            // Game so the auto-update check actually runs, Steam's overlay
            // hook only has a chance of extending to the game window if it
            // can recognize the game as this launcher's own child process --
            // going through the shell adds a layer that risks obscuring
            // that relationship. See issue tracking the overlay-handoff
            // question for the full reasoning.
            startInfo.UseShellExecute = false;
            startInfo.FileName = executablePath;
            startInfo.WorkingDirectory = Path.GetDirectoryName(executablePath) ?? LauncherPaths.GameDir;

            // Confirmed real bug: SteamAPI_Init() failed specifically when
            // launched Steam -> this launcher -> the game (a grandchild of
            // Steam, not a direct child), even though the game's own
            // steam_appid.txt sits right next to it and WorkingDirectory
            // above is set correctly. steam_appid.txt's file-based lookup
            // is the *fallback* Valve documents for "running independently
            // of Steam" -- setting the SteamAppId environment variable
            // directly on the child process is the more robust mechanism
            // Valve recommends specifically for a launcher-spawns-game
            // process chain like this one, and doesn't depend on Steam
            // correctly resolving anything through however it invoked
            // this launcher (e.g. as a Non-Steam Game shortcut). Read
            // from the game's own steam_appid.txt rather than
            // hardcoding 480 here too, so this doesn't need its own code
            // change whenever the real AppID replaces the test one.
            string appIdFile = Path.Combine(startInfo.WorkingDirectory, "steam_appid.txt");
            if (File.Exists(appIdFile))
            {
                string appId = File.ReadAllText(appIdFile).Trim();
                if (appId.Length > 0) startInfo.EnvironmentVariables["SteamAppId"] = appId;
            }
        }

        Process.Start(startInfo);
    }
}
