using System.Text.Json.Serialization;

namespace RobEveryoneLauncher;

// Only the fields this launcher actually reads -- System.Text.Json ignores
// everything else GitHub's much larger response object carries, same
// "don't mirror the whole schema" approach the old in-game UpdateChecker
// used with JsonUtility.
public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
