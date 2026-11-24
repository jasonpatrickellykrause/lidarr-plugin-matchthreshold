using System.Collections.Generic;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Extras.Metadata.Files;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins.MatchThreshold;

/// <summary>
/// Settings host only. Lidarr gives plugins a settings page through a provider type, and a metadata
/// provider is the one with no side effects: it appears under Settings > Metadata and writes no files.
/// Enable it to apply the thresholds; while it is disabled the plugin uses Lidarr's built-in values.
/// </summary>
public class MatchThresholdMetadata : MetadataBase<MatchThresholdSettings>
{
    public override string Name => MatchThresholdPlugin.DisplayName;

    public override MetadataFile FindMetadataFile(Artist artist, string path) => null!;

    public override MetadataFileResult ArtistMetadata(Artist artist) => null!;

    public override MetadataFileResult AlbumMetadata(Artist artist, Album album, string albumPath) => null!;

    public override MetadataFileResult TrackMetadata(Artist artist, TrackFile trackFile) => null!;

    public override List<ImageFileResult> ArtistImages(Artist artist) => new();

    public override List<ImageFileResult> AlbumImages(Artist artist, Album album, string albumPath) => new();

    public override List<ImageFileResult> TrackImages(Artist artist, TrackFile trackFile) => new();
}
