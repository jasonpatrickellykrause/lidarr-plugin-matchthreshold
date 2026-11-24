using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles.TrackImport;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Plugins.MatchThreshold;

/// <summary>
/// Copy of Lidarr's CloseAlbumMatchSpecification with the two hardcoded thresholds read from settings.
/// Lidarr's container registers this alongside the built-in check; BuiltInMatchSpecificationRemover then
/// takes the built-in one out, so this is the only album-match check left.
/// </summary>
public class ConfigurableCloseAlbumMatchSpecification : IImportDecisionEngineSpecification<LocalAlbumRelease>
{
    private static readonly List<string> IgnoredForExistingFiles = new() { "missing_tracks", "unmatched_tracks" };

    private readonly IMatchThresholdSettingsReader _settings;
    private readonly Logger _logger;

    public ConfigurableCloseAlbumMatchSpecification(IMatchThresholdSettingsReader settings, Logger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public Decision IsSatisfiedBy(LocalAlbumRelease item, DownloadClientItem downloadClientItem)
    {
        var thresholds = _settings.Current();
        var albumThreshold = thresholds.AlbumDistance;
        var trackThreshold = thresholds.TrackDistance;

        double dist;
        string reasons;

        // strict when a new download
        if (item.NewDownload)
        {
            dist = item.Distance.NormalizedDistance();
            reasons = item.Distance.Reasons;
            if (dist > albumThreshold)
            {
                _logger.Debug($"Album match is not close enough: {dist} vs {albumThreshold} {reasons}. Skipping {item}");
                return Decision.Reject($"Album match is not close enough: {1 - dist:P1} vs {1 - albumThreshold:P0} {reasons}");
            }

            var worstTrackMatch = item.LocalTracks.Where(x => x.Distance != null).MaxBy(x => x.Distance.NormalizedDistance());
            if (worstTrackMatch == null)
            {
                _logger.Debug("No tracks matched");
                return Decision.Reject("No tracks matched");
            }

            var maxTrackDist = worstTrackMatch.Distance.NormalizedDistance();
            var trackReasons = worstTrackMatch.Distance.Reasons;
            if (maxTrackDist > trackThreshold)
            {
                _logger.Debug($"Worst track match: {maxTrackDist} vs {trackThreshold} {trackReasons}. Skipping {item}");
                return Decision.Reject($"Worst track match: {1 - maxTrackDist:P1} vs {1 - trackThreshold:P0} {trackReasons}");
            }
        }

        // otherwise importing existing files in library
        else
        {
            // get album distance ignoring whether tracks are missing
            dist = item.Distance.NormalizedDistanceExcluding(IgnoredForExistingFiles);
            reasons = item.Distance.Reasons;
            if (dist > albumThreshold)
            {
                _logger.Debug($"Album match is not close enough: {dist} vs {albumThreshold} {reasons}. Skipping {item}");
                return Decision.Reject($"Album match is not close enough: {1 - dist:P1} vs {1 - albumThreshold:P0} {reasons}");
            }
        }

        _logger.Debug($"Accepting release {item}: dist {dist} vs {albumThreshold} {reasons}");
        return Decision.Accept();
    }
}
