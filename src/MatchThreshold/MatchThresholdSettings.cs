using System;
using FluentValidation;
using NzbDrone.Core.Annotations;
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

    private static readonly MatchThresholdSettingsValidator Validator = new();

    [FieldDefinition(0, Label = "Minimum album match (%)", Type = FieldType.Number, HelpText = "Imports scoring below this are rejected with \"Album match is not close enough\". Lidarr's built-in value is 80. Lower values import more albums automatically, including more wrong matches.")]
    public int AlbumMatchMinimum { get; set; } = LidarrAlbumMatchMinimum;

    [FieldDefinition(1, Label = "Minimum track match (%)", Type = FieldType.Number, HelpText = "New downloads are rejected with \"Worst track match\" when any track scores below this. Lidarr's built-in value is 60.")]
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
