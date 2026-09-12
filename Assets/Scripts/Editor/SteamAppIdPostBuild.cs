using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace RobEveryone.EditorTools
{
    // Steam requires steam_appid.txt to sit in the SAME folder as the
    // built .exe -- Assets/StreamingAssets (where the copy in source
    // control lives, for easy version control/reference) only lands one
    // level too deep, inside <Product>_Data/StreamingAssets, which
    // SteamAPI.Init() never looks in. Copying it out to the build root
    // automatically here means nobody has to remember a manual
    // post-build step (or forget it, and wonder why Steam init silently
    // fails on a fresh machine that never launched via steam:// once).
    public static class SteamAppIdPostBuild
    {
        [PostProcessBuild]
        public static void CopySteamAppId(BuildTarget target, string pathToBuiltProject)
        {
            string buildFolder = Path.GetDirectoryName(pathToBuiltProject);
            if (string.IsNullOrEmpty(buildFolder)) return;

            string source = Path.Combine(Application.streamingAssetsPath, "steam_appid.txt");
            string destination = Path.Combine(buildFolder, "steam_appid.txt");

            if (File.Exists(source)) File.Copy(source, destination, overwrite: true);
        }
    }
}
