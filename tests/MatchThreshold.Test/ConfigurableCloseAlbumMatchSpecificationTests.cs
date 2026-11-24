using Moq;
using NLog;
using NzbDrone.Core.MediaFiles.TrackImport.Identification;
using NzbDrone.Core.MediaFiles.TrackImport.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Plugins.MatchThreshold;
using Xunit;

namespace MatchThreshold.Test;

public class ConfigurableCloseAlbumMatchSpecificationTests
{
    private static readonly Logger Logger = LogManager.CreateNullLogger();

    [Fact]
    public void Default_thresholds_equal_lidarr_constants_exactly()
    {
        Assert.Equal(0.20, MatchThresholds.LidarrDefaults.AlbumDistance);
        Assert.Equal(0.40, MatchThresholds.LidarrDefaults.TrackDistance);
    }

    // expectRejected is for a new download; existing files skip the track check.
    [Theory]
    [InlineData(0.212, 0.10, true)]   // 78.8%, the case from the issue
    [InlineData(0.19, 0.10, false)]   // 81%
    [InlineData(0.10, 0.10, false)]
    [InlineData(0.45, 0.10, true)]
    [InlineData(0.10, 0.39, false)]   // worst track 61%
    [InlineData(0.10, 0.45, true)]    // worst track 55%
    public void Lidarr_defaults_match_built_in_check(double albumDistance, double trackDistance, bool expectRejected)
    {
        foreach (var newDownload in new[] { true, false })
        {
            var release = Release(albumDistance, trackDistance, newDownload);

            var builtIn = new CloseAlbumMatchSpecification(Logger).IsSatisfiedBy(release, null!);
            var configurable = Spec(MatchThresholds.LidarrDefaults).IsSatisfiedBy(release, null!);

            Assert.Equal(builtIn.Accepted, configurable.Accepted);
            Assert.Equal(builtIn.Reason, configurable.Reason);
            if (newDownload)
            {
                Assert.Equal(expectRejected, !configurable.Accepted);
            }
        }
    }

    [Fact]
    public void Lowered_album_threshold_accepts_78_8_percent_match()
    {
        var release = Release(albumDistance: 0.212, trackDistance: 0.1, newDownload: true);

        var decision = Spec(MatchThresholds.FromPercentages(75, 60)).IsSatisfiedBy(release, null!);

        Assert.True(decision.Accepted);
    }

    [Fact]
    public void Rejection_message_shows_configured_threshold()
    {
        var release = Release(albumDistance: 0.30, trackDistance: 0.1, newDownload: true);

        var decision = Spec(MatchThresholds.FromPercentages(75, 60)).IsSatisfiedBy(release, null!);

        Assert.False(decision.Accepted);
        // Percent formatting is culture-dependent ("70.0%" on Windows, "70.0 %" with invariant culture), as in Lidarr.
        Assert.StartsWith($"Album match is not close enough: {0.70:P1} vs {0.75:P0}", decision.Reason);
    }

    [Fact]
    public void Track_threshold_applies_to_new_downloads()
    {
        var release = Release(albumDistance: 0.1, trackDistance: 0.45, newDownload: true);

        Assert.False(Spec(MatchThresholds.FromPercentages(80, 60)).IsSatisfiedBy(release, null!).Accepted);
        Assert.True(Spec(MatchThresholds.FromPercentages(80, 50)).IsSatisfiedBy(release, null!).Accepted);
    }

    [Fact]
    public void Existing_files_ignore_missing_tracks_penalty()
    {
        var distance = new Distance();
        distance.Add("album", 0.1);
        distance.Add("missing_tracks", 1.0);
        var release = new LocalAlbumRelease(new List<LocalTrack>()) { Distance = distance, NewDownload = false };

        Assert.True(Spec(MatchThresholds.LidarrDefaults).IsSatisfiedBy(release, null!).Accepted);
    }

    private static ConfigurableCloseAlbumMatchSpecification Spec(MatchThresholds thresholds)
    {
        var reader = new Mock<IMatchThresholdSettingsReader>();
        reader.Setup(r => r.Current()).Returns(thresholds);
        return new ConfigurableCloseAlbumMatchSpecification(reader.Object, Logger);
    }

    // A single penalty key makes NormalizedDistance equal to the value added.
    private static LocalAlbumRelease Release(double albumDistance, double trackDistance, bool newDownload)
    {
        var albumDist = new Distance();
        albumDist.Add("album", albumDistance);

        var trackDist = new Distance();
        trackDist.Add("track_title", trackDistance);

        return new LocalAlbumRelease(new List<LocalTrack> { new() { Distance = trackDist } })
        {
            Distance = albumDist,
            NewDownload = newDownload
        };
    }
}
