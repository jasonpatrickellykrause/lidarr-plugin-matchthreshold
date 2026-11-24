using System;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Plugins.MatchThreshold;

/// <summary>
/// Settings shown under Settings > Metadata > Match Threshold.
/// Values are minimum match percentages, the same numbers Lidarr prints in its rejection messages.
/// </summary>
public class MatchThresholdSettings : IProviderConfig
{
    public const int LidarrAlbumMatchMinimum = 80;
    public const int LidarrTrackMatchMinimum = 60;

    internal const string AlbumFieldWarning =
        "Lowering this lets Lidarr import files it matched to the wrong release, such as another edition, a regional release, or a different album with a similar title. " +
        "A wrong match is renamed and moved into that album's folder, can replace files you already have, and marks the album as downloaded so Lidarr stops searching for the right one. " +
        "It also applies when Lidarr rescans files already in your library.";

    internal const string TrackFieldWarning =
        "Lowering this lets a file import as the wrong track, for example a live or remixed version in place of the album version, and replace the file you already have for that track.";

    internal const string AlbumBelowDefaultWarning =
        "Below Lidarr's 80%, more wrong matches will import without asking. Check Activity > History after imports, and use Manual Import for one-off albums instead if you can.";

    internal const string AlbumFarBelowDefaultWarning =
        "Below 70%, releases that differ in several fields at once, such as album title, year, track count and country, can still import. Expect wrong imports.";

    internal const string TrackBelowDefaultWarning =
        "Below Lidarr's 60%, files with a different title or length can import as the wrong track.";

    private static readonly MatchThresholdSettingsValidator Validator = new();

    [FieldDefinition(0, Label = "Minimum album match (%)", Type = FieldType.Number, Section = MetadataSectionType.Metadata, HelpText = "Imports scoring below this are rejected with \"Album match is not close enough\". Lidarr's built-in value is 80.", HelpTextWarning = AlbumFieldWarning)]
    public int AlbumMatchMinimum { get; set; } = LidarrAlbumMatchMinimum;

    [FieldDefinition(1, Label = "Minimum track match (%)", Type = FieldType.Number, Section = MetadataSectionType.Metadata, HelpText = "New downloads are rejected with \"Worst track match\" when any track scores below this. Lidarr's built-in value is 60.", HelpTextWarning = TrackFieldWarning)]
    public int TrackMatchMinimum { get; set; } = LidarrTrackMatchMinimum;

    public MatchThresholds ToThresholds() => MatchThresholds.FromPercentages(AlbumMatchMinimum, TrackMatchMinimum);

    public NzbDroneValidationResult Validate() => new(Validator.Validate(this));
}

public class MatchThresholdSettingsValidator : AbstractValidator<MatchThresholdSettings>
{
    public MatchThresholdSettingsValidator()
    {
        RuleFor(c => c.AlbumMatchMinimum).InclusiveBetween(1, 100)
            .WithMessage("Minimum album match must be between 1 and 100");
        RuleFor(c => c.TrackMatchMinimum).InclusiveBetween(1, 100)
            .WithMessage("Minimum track match must be between 1 and 100");

        // Warnings, not errors: Lidarr shows them when saving and saves only after Save is clicked again.
        RuleFor(c => c.AlbumMatchMinimum)
            .GreaterThanOrEqualTo(70)
            .When(c => c.AlbumMatchMinimum >= 1)
            .WithMessage(MatchThresholdSettings.AlbumFarBelowDefaultWarning)
            .AsWarning();
        RuleFor(c => c.AlbumMatchMinimum)
            .GreaterThanOrEqualTo(MatchThresholdSettings.LidarrAlbumMatchMinimum)
            .When(c => c.AlbumMatchMinimum >= 70)
            .WithMessage(MatchThresholdSettings.AlbumBelowDefaultWarning)
            .AsWarning();
        RuleFor(c => c.TrackMatchMinimum)
            .GreaterThanOrEqualTo(MatchThresholdSettings.LidarrTrackMatchMinimum)
            .When(c => c.TrackMatchMinimum >= 1)
            .WithMessage(MatchThresholdSettings.TrackBelowDefaultWarning)
            .AsWarning();
    }
}

/// <summary>
/// Maximum allowed distances, in Lidarr's internal form: 0.20 distance is an 80% match.
/// </summary>
public sealed record MatchThresholds(double AlbumDistance, double TrackDistance)
{
    public static readonly MatchThresholds LidarrDefaults = FromPercentages(
        MatchThresholdSettings.LidarrAlbumMatchMinimum,
        MatchThresholdSettings.LidarrTrackMatchMinimum);

    public static MatchThresholds FromPercentages(int albumMatchMinimum, int trackMatchMinimum)
    {
        return new MatchThresholds(ToDistance(albumMatchMinimum), ToDistance(trackMatchMinimum));
    }

    private static double ToDistance(int percent)
    {
        // (100 - percent) / 100.0 gives exactly 0.2 for 80, matching Lidarr's constant;
        // 1.0 - 0.8 would give 0.19999999999999996 and reject a match Lidarr accepts.
        return (100 - Math.Clamp(percent, 1, 100)) / 100.0;
    }
}
