using System;
using System.Linq;
using NLog;
using NzbDrone.Core.Extras.Metadata;

namespace NzbDrone.Core.Plugins.MatchThreshold;

public interface IMatchThresholdSettingsReader
{
    MatchThresholdProfile Current();
}

/// <summary>
/// Reads the thresholds from the enabled Match Threshold metadata entry on every call, so a settings
/// change applies to the next import without a restart. One small query per import decision.
/// </summary>
public class MatchThresholdSettingsReader : IMatchThresholdSettingsReader
{
    // Lazy: resolving the metadata factory resolves every IMetadata, including other plugins' providers,
    // which can form a dependency cycle if done while the container is still building.
    private readonly Lazy<IMetadataFactory> _metadataFactory;
    private readonly Logger _logger;

    public MatchThresholdSettingsReader(Lazy<IMetadataFactory> metadataFactory, Logger logger)
    {
        _metadataFactory = metadataFactory;
        _logger = logger;
    }

    public MatchThresholdProfile Current()
    {
        try
        {
            var settings = _metadataFactory.Value.All()
                .Where(d => d.Enable && d.Implementation == nameof(MatchThresholdMetadata))
                .Select(d => d.Settings)
                .OfType<MatchThresholdSettings>()
                .FirstOrDefault();

            return settings?.ToProfile() ?? MatchThresholdProfile.LidarrDefaults;
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "Match Threshold: could not read settings, using Lidarr's built-in thresholds");
            return MatchThresholdProfile.LidarrDefaults;
        }
    }
}
