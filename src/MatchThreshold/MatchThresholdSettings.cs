using System;
using System.Linq.Expressions;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Plugins.MatchThreshold;

/// <summary>
/// Settings shown under Settings > Metadata > Match Threshold.
/// Values are minimum match percentages, the same numbers Lidarr prints in its rejection messages.
///
/// Two sets: one for downloads of albums that have no files yet, one for downloads that would replace
/// files already in the library. The first two property names predate the split and are kept so saved
/// settings carry over.
/// </summary>
public class MatchThresholdSettings : IProviderConfig
{
    public const int LidarrAlbumMatchMinimum = 80;
    public const int LidarrTrackMatchMinimum = 60;

    // Floors. Below about 60% an album match is a different album that shares the artist; below about 40% a track
    // match is a different song. Lower values can't be saved, and older saved values are raised to these.
    public const int AlbumMatchFloor = 60;
    public const int TrackMatchFloor = 40;

    // Per-field warnings are one line each so the dialog stays compact; the full risk is on the checkbox.
    internal const string MissingAlbumFieldWarning = "Lower values can import the wrong release. Also applies to library rescans.";
    internal const string MissingTrackFieldWarning = "Lower values can import the wrong version of a track.";
    internal const string ReplacingAlbumFieldWarning = "Lower values let a wrong release replace files you have.";
    internal const string ReplacingTrackFieldWarning = "Lower values let a wrong version replace a track you have.";

    // Shown when saving. Lidarr lists each one under its field, so they're short too.
    internal const string BelowDefaultWarning = "Below Lidarr's default. Check Activity > History after imports.";
    internal const string AlbumFarBelowDefaultWarning = "Below 70%, albums that differ in title, year, track count and country can import. Expect wrong imports.";
    internal const string ReplacingLooserThanMissingWarning = "Looser than missing albums. A bad replacement removes music you had.";

    private static readonly MatchThresholdSettingsValidator Validator = new();

    [FieldDefinition(0, Label = "Missing albums: minimum album match (%)", Type = FieldType.Number, Section = MetadataSectionType.Metadata, HelpText = "For albums with no files yet. Lidarr's default is 80, lowest allowed is 60.", HelpTextWarning = MissingAlbumFieldWarning)]
    public int AlbumMatchMinimum { get; set; } = LidarrAlbumMatchMinimum;

    [FieldDefinition(1, Label = "Missing albums: minimum track match (%)", Type = FieldType.Number, Section = MetadataSectionType.Metadata, HelpText = "For albums with no files yet. Lidarr's default is 60, lowest allowed is 40.", HelpTextWarning = MissingTrackFieldWarning)]
    public int TrackMatchMinimum { get; set; } = LidarrTrackMatchMinimum;

    [FieldDefinition(2, Label = "Replacing files: minimum album match (%)", Type = FieldType.Number, Section = MetadataSectionType.Metadata, HelpText = "For albums that already have files, such as upgrades. Lidarr's default is 80, lowest allowed is 60.", HelpTextWarning = ReplacingAlbumFieldWarning)]
    public int ReplacingAlbumMatchMinimum { get; set; } = LidarrAlbumMatchMinimum;

    [FieldDefinition(3, Label = "Replacing files: minimum track match (%)", Type = FieldType.Number, Section = MetadataSectionType.Metadata, HelpText = "For albums that already have files. Lidarr's default is 60, lowest allowed is 40.", HelpTextWarning = ReplacingTrackFieldWarning)]
    public int ReplacingTrackMatchMinimum { get; set; } = LidarrTrackMatchMinimum;

    [FieldDefinition(4, Label = "Backed up and accept data-loss risk", Type = FieldType.Checkbox, Section = MetadataSectionType.Metadata, HelpText = AcknowledgementText, HelpTextWarning = AcknowledgementWarning)]
    public bool AcknowledgeDataLossRisk { get; set; }

    internal const string AcknowledgementText =
        "I have a current backup of my music and Lidarr database, and accept that lower values can import wrong releases and replace or remove files I have.";

    internal const string AcknowledgementWarning =
        "Required for values below 80% album or 60% track. Until ticked, Lidarr's defaults are used.";

    internal const string AcknowledgementRequiredError =
        "Tick \"Backed up and accept data-loss risk\" to save values below 80% album or 60% track.";

    /// <summary>True when any minimum is below Lidarr's built-in value, which is what can cause data loss.</summary>
    public bool LowersAnyThreshold() =>
        AlbumMatchMinimum < LidarrAlbumMatchMinimum ||
        TrackMatchMinimum < LidarrTrackMatchMinimum ||
        ReplacingAlbumMatchMinimum < LidarrAlbumMatchMinimum ||
        ReplacingTrackMatchMinimum < LidarrTrackMatchMinimum;

    /// <summary>
    /// The thresholds imports use. Without the acknowledgement, each minimum is raised to at least
    /// Lidarr's default, so settings saved before the checkbox existed can't lower anything.
    /// </summary>
    public MatchThresholdProfile ToProfile()
    {
        var album = Effective(AlbumMatchMinimum, LidarrAlbumMatchMinimum);
        var track = Effective(TrackMatchMinimum, LidarrTrackMatchMinimum);
        var replacingAlbum = Effective(ReplacingAlbumMatchMinimum, LidarrAlbumMatchMinimum);
        var replacingTrack = Effective(ReplacingTrackMatchMinimum, LidarrTrackMatchMinimum);

        return new MatchThresholdProfile(
            MatchThresholds.FromPercentages(album, track),
            MatchThresholds.FromPercentages(replacingAlbum, replacingTrack));
    }

    private int Effective(int value, int lidarrDefault)
    {
        return AcknowledgeDataLossRisk ? value : Math.Max(value, lidarrDefault);
    }

    public NzbDroneValidationResult Validate() => new(Validator.Validate(this));
}

public class MatchThresholdSettingsValidator : AbstractValidator<MatchThresholdSettings>
{
    public MatchThresholdSettingsValidator()
    {
        Range(c => c.AlbumMatchMinimum, MatchThresholdSettings.AlbumMatchFloor);
        Range(c => c.TrackMatchMinimum, MatchThresholdSettings.TrackMatchFloor);
        Range(c => c.ReplacingAlbumMatchMinimum, MatchThresholdSettings.AlbumMatchFloor);
        Range(c => c.ReplacingTrackMatchMinimum, MatchThresholdSettings.TrackMatchFloor);

        // An error, not a warning: lowered values can't be saved without the acknowledgement.
        RuleFor(c => c.AcknowledgeDataLossRisk)
            .Equal(true)
            .When(c => c.LowersAnyThreshold())
            .WithMessage(MatchThresholdSettings.AcknowledgementRequiredError);

        // Warnings, not errors: Lidarr shows them when saving and saves only after Save is clicked again.
        // At most one per field, and none on a value that already failed the range check.
        AlbumWarning(c => c.AlbumMatchMinimum);
        TrackWarning(c => c.TrackMatchMinimum);
        AlbumWarning(c => c.ReplacingAlbumMatchMinimum);
        TrackWarning(c => c.ReplacingTrackMatchMinimum);

        RuleFor(c => c.ReplacingAlbumMatchMinimum)
            .Must((c, v) => v >= c.AlbumMatchMinimum && c.ReplacingTrackMatchMinimum >= c.TrackMatchMinimum)
            .When(c => InRange(c.ReplacingAlbumMatchMinimum, MatchThresholdSettings.AlbumMatchFloor))
            .WithMessage(MatchThresholdSettings.ReplacingLooserThanMissingWarning)
            .AsWarning();
    }

    private static bool InRange(int value, int floor) => value >= floor && value <= 100;

    private void Range(Expression<Func<MatchThresholdSettings, int>> field, int floor)
    {
        RuleFor(field).InclusiveBetween(floor, 100).WithMessage($"Must be between {floor} and 100");
    }

    private void AlbumWarning(Expression<Func<MatchThresholdSettings, int>> field)
    {
        var value = field.Compile();
        RuleFor(field)
            .GreaterThanOrEqualTo(70)
            .When(c => InRange(value(c), MatchThresholdSettings.AlbumMatchFloor))
            .WithMessage(MatchThresholdSettings.AlbumFarBelowDefaultWarning)
            .AsWarning();
        RuleFor(field)
            .GreaterThanOrEqualTo(MatchThresholdSettings.LidarrAlbumMatchMinimum)
            .When(c => value(c) >= 70)
            .WithMessage(MatchThresholdSettings.BelowDefaultWarning)
            .AsWarning();
    }

    private void TrackWarning(Expression<Func<MatchThresholdSettings, int>> field)
    {
        var value = field.Compile();
        RuleFor(field)
            .GreaterThanOrEqualTo(MatchThresholdSettings.LidarrTrackMatchMinimum)
            .When(c => InRange(value(c), MatchThresholdSettings.TrackMatchFloor))
            .WithMessage(MatchThresholdSettings.BelowDefaultWarning)
            .AsWarning();
    }
}

/// <summary>
/// The two threshold sets. When they are equal, the check skips looking up the album's existing files.
/// </summary>
public sealed record MatchThresholdProfile(MatchThresholds MissingAlbum, MatchThresholds Replacing)
{
    public static readonly MatchThresholdProfile LidarrDefaults = new(MatchThresholds.LidarrDefaults, MatchThresholds.LidarrDefaults);

    public bool SetsDiffer => MissingAlbum != Replacing;

    /// <summary>The stricter value of each pair, used when the album's existing files can't be checked.</summary>
    public MatchThresholds Strictest => new(
        Math.Min(MissingAlbum.AlbumDistance, Replacing.AlbumDistance),
        Math.Min(MissingAlbum.TrackDistance, Replacing.TrackDistance));
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
        return new MatchThresholds(
            ToDistance(albumMatchMinimum, MatchThresholdSettings.AlbumMatchFloor),
            ToDistance(trackMatchMinimum, MatchThresholdSettings.TrackMatchFloor));
    }

    private static double ToDistance(int percent, int floor)
    {
        // Clamped so a value saved before the floors existed can't go below them.
        // (100 - percent) / 100.0 gives exactly 0.2 for 80, matching Lidarr's constant;
        // 1.0 - 0.8 would give 0.19999999999999996 and reject a match Lidarr accepts.
        return (100 - Math.Clamp(percent, floor, 100)) / 100.0;
    }
}
