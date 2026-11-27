namespace NzbDrone.Core.Plugins.MatchThreshold;

/// <summary>
/// Exported Lidarr plugin root. The assembly name is Lidarr.Plugin.MatchThreshold via MatchThreshold.csproj.
/// </summary>
public sealed class MatchThresholdPlugin : Plugin
{
    public const string DisplayName = "Match Threshold";
    public const string RepositoryOwner = "matrix-runout";
    public const string RepositoryUrl = "https://github.com/matrix-runout/lidarr-plugin-matchthreshold";

    public override string Name => DisplayName;

    public override string Owner => RepositoryOwner;

    public override string GithubUrl => RepositoryUrl;
}
