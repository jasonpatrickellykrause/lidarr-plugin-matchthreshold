using Moq;
using NLog;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Plugins.MatchThreshold;
using Xunit;

namespace MatchThreshold.Test;

public class AcknowledgementTests
{
    [Fact]
    public void Lowered_value_without_acknowledgement_cannot_be_saved()
    {
        var result = new MatchThresholdSettings { AlbumMatchMinimum = 75 }.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(MatchThresholdSettings.AcknowledgementRequiredError, result.Errors.Select(e => e.ErrorMessage));
    }

    [Theory]
    [InlineData(80, 60, 80, 60)]   // Lidarr defaults
    [InlineData(90, 70, 90, 70)]   // stricter everywhere
    public void Default_or_stricter_values_save_without_acknowledgement(int album, int track, int replacingAlbum, int replacingTrack)
    {
        var result = new MatchThresholdSettings
        {
            AlbumMatchMinimum = album,
            TrackMatchMinimum = track,
            ReplacingAlbumMatchMinimum = replacingAlbum,
            ReplacingTrackMatchMinimum = replacingTrack
        }.Validate();

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Lowered_replacing_value_also_needs_acknowledgement()
    {
        var result = new MatchThresholdSettings { ReplacingTrackMatchMinimum = 55 }.Validate();

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Without_acknowledgement_lowered_values_are_raised_to_lidarr_defaults()
    {
        var settings = new MatchThresholdSettings { AlbumMatchMinimum = 75, TrackMatchMinimum = 50, ReplacingAlbumMatchMinimum = 90 };

        var profile = settings.ToProfile();

        Assert.Equal(MatchThresholds.LidarrDefaults, profile.MissingAlbum);
        Assert.Equal(MatchThresholds.FromPercentages(90, 60), profile.Replacing);
    }

    [Fact]
    public void With_acknowledgement_lowered_values_apply()
    {
        var settings = new MatchThresholdSettings { AlbumMatchMinimum = 75, AcknowledgeDataLossRisk = true };

        Assert.Equal(MatchThresholds.FromPercentages(75, 60), settings.ToProfile().MissingAlbum);
    }

    [Fact]
    public void Reader_logs_unacknowledged_fallback_once()
    {
        var target = new NLog.Targets.MemoryTarget { Layout = "${message}" };
        var config = new NLog.Config.LoggingConfiguration();
        config.AddRuleForAllLevels(target);
        var logger = new LogFactory { Configuration = config }.GetLogger("test");

        var factory = new Mock<IMetadataFactory>();
        factory.Setup(f => f.All()).Returns(new List<MetadataDefinition>
        {
            new()
            {
                Enable = true,
                Implementation = nameof(MatchThresholdMetadata),
                Settings = new MatchThresholdSettings { AlbumMatchMinimum = 75 }
            }
        });
        var reader = new MatchThresholdSettingsReader(new Lazy<IMetadataFactory>(() => factory.Object), logger);

        Assert.Equal(MatchThresholdProfile.LidarrDefaults, reader.Current());
        reader.Current();
        reader.Current();

        Assert.Single(target.Logs, l => l.Contains("not ticked"));
    }
}
