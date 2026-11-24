using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.TrackImport;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Plugins.MatchThreshold;

/// <summary>
/// Copy of Lidarr's CloseAlbumMatchSpecification with the two hardcoded thresholds read from settings,
/// chosen by whether the download would replace files the album already has.
/// Lidarr's container registers this alongside the built-in check; BuiltInMatchSpecificationRemover then
/// takes the built-in one out, so this is the only album-match check left.
/// </summary>
public class ConfigurableCloseAlbumMatchSpecification : IImportDecisionEngineSpecification<LocalAlbumRelease>
{
    private static readonly List<string> IgnoredForExistingFiles = new() { "missing_tracks", "unmatched_tracks" };

    private readonly IMatchThresholdSettingsReader _settings;
    private readonly IMediaFileService _mediaFileService;
    private readonly Logger _logger;

    public ConfigurableCloseAlbumMatchSpecification(IMatchThresholdSettingsReader settings, IMediaFileService mediaFileService, Logger logger)
    {
        _settings = settings;
        _mediaFileService = mediaFileService;
        _logger = logger;
    }

    public Decision IsSatisfiedBy(LocalAlbumRelease item, DownloadClientItem downloadClientItem)
    {
        var (thresholds, suffix) = SelectThresholds(item);
        var albumThreshold = thresholds.AlbumDistance;
        var trackThreshold = thresholds.TrackDistance;
        var decision = Evaluate(item, albumThreshold, trackThreshold);

        return decision.Accepted || suffix.Length == 0 ? decision : Decision.Reject(decision.Reason + suffix);
    }

    /// <summary>
    /// A new download of an album that already has files would replace them, so it gets the "replacing" set.
    /// Files already in the library (rescans) and albums with no files get the "missing albums" set.
    /// The suffix names the set in rejection messages, only when the two sets differ.
    /// </summary>
    private (MatchThresholds Thresholds, string Suffix) SelectThresholds(LocalAlbumRelease item)
    {
        var profile = _settings.Current();
        if (!profile.SetsDiffer)
        {
            return (profile.MissingAlbum, string.Empty);
        }

        if (!item.NewDownload)
        {
            return (profile.MissingAlbum, string.Empty);
        }

        try
        {
            var albumId = item.AlbumRelease?.AlbumId ?? 0;
            if (albumId == 0)
            {
                return (profile.Strictest, " (album not identified, stricter setting used)");
            }

            return _mediaFileService.GetFilesByAlbum(albumId).Count > 0
                ? (profile.Replacing, " (album already has files, replacing-files setting used)")
                : (profile.MissingAlbum, " (missing album)");
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "Match Threshold: could not check the album's existing files, using the stricter setting");
            return (profile.Strictest, " (existing files not checked, stricter setting used)");
        }
    }

    private Decision Evaluate(LocalAlbumRelease item, double albumThreshold, double trackThreshold)
    {

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
