using Moq;
using NLog;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.TrackImport.Identification;
using NzbDrone.Core.Music;
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

    // Missing albums at 75/60, replacing files at Lidarr's 80/60.
    private static readonly MatchThresholdProfile Split = new(
        MatchThresholds.FromPercentages(75, 60),
        MatchThresholds.LidarrDefaults);

    [Fact]
    public void Missing_album_uses_missing_album_setting()
    {
        var spec = Spec(Split, existingFiles: 0);

        Assert.True(spec.IsSatisfiedBy(Release(0.212, 0.1, newDownload: true), null!).Accepted);
    }

    [Fact]
    public void Album_with_files_uses_replacing_setting_and_says_so()
    {
        var spec = Spec(Split, existingFiles: 3);

        var decision = spec.IsSatisfiedBy(Release(0.212, 0.1, newDownload: true), null!);

        Assert.False(decision.Accepted);
        Assert.Contains($"vs {0.80:P0}", decision.Reason);
        Assert.EndsWith("(album already has files, replacing-files setting used)", decision.Reason);
    }

    [Fact]
    public void Library_rescan_uses_missing_album_setting_without_file_lookup()
    {
        var mediaFiles = new Mock<IMediaFileService>(MockBehavior.Strict);
        var spec = Spec(Split, mediaFiles.Object);

        Assert.True(spec.IsSatisfiedBy(Release(0.212, 0.1, newDownload: false), null!).Accepted);
    }

    [Fact]
    public void Equal_sets_skip_file_lookup()
    {
        var mediaFiles = new Mock<IMediaFileService>(MockBehavior.Strict);
        var spec = Spec(new MatchThresholdProfile(MatchThresholds.FromPercentages(75, 60), MatchThresholds.FromPercentages(75, 60)), mediaFiles.Object);

        Assert.True(spec.IsSatisfiedBy(Release(0.212, 0.1, newDownload: true), null!).Accepted);
    }

    [Fact]
    public void File_lookup_failure_uses_stricter_setting()
    {
        var mediaFiles = new Mock<IMediaFileService>();
        mediaFiles.Setup(m => m.GetFilesByAlbum(It.IsAny<int>())).Throws(new InvalidOperationException("database unavailable"));
        var spec = Spec(Split, mediaFiles.Object);

        var decision = spec.IsSatisfiedBy(Release(0.212, 0.1, newDownload: true), null!);

        Assert.False(decision.Accepted);
        Assert.EndsWith("(existing files not checked, stricter setting used)", decision.Reason);
    }

    [Fact]
    public void Stricter_replacing_track_setting_applies_to_albums_with_files()
    {
        var profile = new MatchThresholdProfile(MatchThresholds.FromPercentages(80, 50), MatchThresholds.FromPercentages(80, 60));

        Assert.True(Spec(profile, existingFiles: 0).IsSatisfiedBy(Release(0.1, 0.45, newDownload: true), null!).Accepted);
        Assert.False(Spec(profile, existingFiles: 2).IsSatisfiedBy(Release(0.1, 0.45, newDownload: true), null!).Accepted);
    }

    private static ConfigurableCloseAlbumMatchSpecification Spec(MatchThresholds thresholds)
    {
        return Spec(new MatchThresholdProfile(thresholds, thresholds), new Mock<IMediaFileService>(MockBehavior.Strict).Object);
    }

    private static ConfigurableCloseAlbumMatchSpecification Spec(MatchThresholdProfile profile, int existingFiles)
    {
        var mediaFiles = new Mock<IMediaFileService>();
        mediaFiles.Setup(m => m.GetFilesByAlbum(AlbumId))
            .Returns(Enumerable.Range(0, existingFiles).Select(_ => new TrackFile()).ToList());
        return Spec(profile, mediaFiles.Object);
    }

    private static ConfigurableCloseAlbumMatchSpecification Spec(MatchThresholdProfile profile, IMediaFileService mediaFiles)
    {
        var reader = new Mock<IMatchThresholdSettingsReader>();
        reader.Setup(r => r.Current()).Returns(profile);
        return new ConfigurableCloseAlbumMatchSpecification(reader.Object, mediaFiles, Logger);
    }

    private const int AlbumId = 7;

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
            NewDownload = newDownload,
            AlbumRelease = new AlbumRelease { AlbumId = AlbumId }
        };
    }
}
