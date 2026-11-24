using Moq;
using NLog;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Plugins.MatchThreshold;
using Xunit;

namespace MatchThreshold.Test;

public class MatchThresholdSettingsReaderTests
{
    [Fact]
    public void Enabled_entry_supplies_thresholds()
    {
        var reader = Reader(Definition(enable: true, album: 75, track: 50));

        Assert.Equal(MatchThresholds.FromPercentages(75, 50), reader.Current().MissingAlbum);
        Assert.Equal(0.25, reader.Current().MissingAlbum.AlbumDistance);
        Assert.Equal(MatchThresholds.LidarrDefaults, reader.Current().Replacing);
    }

    [Fact]
    public void Disabled_entry_falls_back_to_lidarr_defaults()
    {
        var reader = Reader(Definition(enable: false, album: 75, track: 50));

        Assert.Equal(MatchThresholdProfile.LidarrDefaults, reader.Current());
    }

    [Fact]
    public void Read_failure_falls_back_to_lidarr_defaults()
    {
        var factory = new Mock<IMetadataFactory>();
        factory.Setup(f => f.All()).Throws(new InvalidOperationException("database unavailable"));
        var reader = new MatchThresholdSettingsReader(new Lazy<IMetadataFactory>(() => factory.Object), LogManager.CreateNullLogger());

        Assert.Equal(MatchThresholdProfile.LidarrDefaults, reader.Current());
    }

    private static MatchThresholdSettingsReader Reader(params MetadataDefinition[] definitions)
    {
        var factory = new Mock<IMetadataFactory>();
        factory.Setup(f => f.All()).Returns(definitions.ToList());
        return new MatchThresholdSettingsReader(new Lazy<IMetadataFactory>(() => factory.Object), LogManager.CreateNullLogger());
    }

    private static MetadataDefinition Definition(bool enable, int album, int track, bool acknowledged = true)
    {
        return new MetadataDefinition
        {
            Enable = enable,
            Implementation = nameof(MatchThresholdMetadata),
            Settings = new MatchThresholdSettings { AlbumMatchMinimum = album, TrackMatchMinimum = track, AcknowledgeDataLossRisk = acknowledged }
        };
    }
}
