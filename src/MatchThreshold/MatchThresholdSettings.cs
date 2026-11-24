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

    internal const string MissingAlbumFieldWarning =
        "Lowering this lets Lidarr import files it matched to the wrong release, such as another edition, a regional release, or a different album with a similar title. " +
        "A wrong match is renamed and moved into that album's folder and marks the album as downloaded, so Lidarr stops searching for the right one. " +
        "It also applies when Lidarr rescans files already in your library.";

    internal const string MissingTrackFieldWarning =
        "Lowering this lets a file import as the wrong track, for example a live or remixed version in place of the album version.";

    internal const string ReplacingAlbumFieldWarning =
        "Lowering this lets a download that matched the wrong release replace music you already have. Lidarr deletes or recycles the files it replaces.";

    internal const string ReplacingTrackFieldWarning =
        "Lowering this lets a wrong track, for example a live or remixed version, replace the file you already have for that track.";

    internal const string AlbumBelowDefaultWarning =
        "Below Lidarr's 80%, more wrong matches will import without asking. Check Activity > History after imports, and use Manual Import for one-off albums instead if you can.";

    internal const string AlbumFarBelowDefaultWarning =
        "Below 70%, releases that differ in several fields at once, such as album title, year, track count and country, can still import. Expect wrong imports.";

    internal const string TrackBelowDefaultWarning =
        "Below Lidarr's 60%, files with a different title or length can import as the wrong track.";

    internal const string ReplacingBelowDefaultWarning =
        "Below Lidarr's default, a wrong match can replace files you already have. Keep a recycle bin set under Settings > Media Management so replaced files can be restored.";

    internal const string ReplacingLooserThanMissingWarning =
        "Replacing files is set looser than filling missing albums. Replacements usually need to be at least as strict, because a bad replacement removes music you had.";

    private static readonly MatchThresholdSettingsValidator Validator = new();

    [FieldDefinition(0, Label = "Missing albums: minimum album match (%)", Type = FieldType.Number, Section = MetadataSectionType.Metadata, HelpText = "For albums with no files yet. Imports scoring below this are rejected with \"Album match is not close enough\". Lidarr's built-in value is 80.", HelpTextWarning = MissingAlbumFieldWarning)]
    public int AlbumMatchMinimum { get; set; } = LidarrAlbumMatchMinimum;

    [FieldDefinition(1, Label = "Missing albums: minimum track match (%)", Type = FieldType.Number, Section = MetadataSectionType.Metadata, HelpText = "For albums with no files yet. Downloads are rejected with \"Worst track match\" when any track scores below this. Lidarr's built-in value is 60.", HelpTextWarning = MissingTrackFieldWarning)]
    public int TrackMatchMinimum { get; set; } = LidarrTrackMatchMinimum;

    [FieldDefinition(2, Label = "Replacing files: minimum album match (%)", Type = FieldType.Number, Section = MetadataSectionType.Metadata, HelpText = "For downloads of albums that already have files, such as quality upgrades. Lidarr's built-in value is 80.", HelpTextWarning = ReplacingAlbumFieldWarning)]
    public int ReplacingAlbumMatchMinimum { get; set; } = LidarrAlbumMatchMinimum;

    [FieldDefinition(3, Label = "Replacing files: minimum track match (%)", Type = FieldType.Number, Section = MetadataSectionType.Metadata, HelpText = "For downloads of albums that already have files. Lidarr's built-in value is 60.", HelpTextWarning = ReplacingTrackFieldWarning)]
    public int ReplacingTrackMatchMinimum { get; set; } = LidarrTrackMatchMinimum;

    public MatchThresholdProfile ToProfile() => new(
        MatchThresholds.FromPercentages(AlbumMatchMinimum, TrackMatchMinimum),
        MatchThresholds.FromPercentages(ReplacingAlbumMatchMinimum, ReplacingTrackMatchMinimum));

    public NzbDroneValidationResult Validate() => new(Validator.Validate(this));
}

public class MatchThresholdSettingsValidator : AbstractValidator<MatchThresholdSettings>
{
    public MatchThresholdSettingsValidator()
    {
        Range(c => c.AlbumMatchMinimum, "Missing albums: minimum album match");
        Range(c => c.TrackMatchMinimum, "Missing albums: minimum track match");
        Range(c => c.ReplacingAlbumMatchMinimum, "Replacing files: minimum album match");
        Range(c => c.ReplacingTrackMatchMinimum, "Replacing files: minimum track match");

        // Warnings, not errors: Lidarr shows them when saving and saves only after Save is clicked again.
        AlbumWarnings(c => c.AlbumMatchMinimum);
        TrackWarning(c => c.TrackMatchMinimum);

        AlbumWarnings(c => c.ReplacingAlbumMatchMinimum);
        TrackWarning(c => c.ReplacingTrackMatchMinimum);
        RuleFor(c => c.ReplacingAlbumMatchMinimum)
            .Must(v => v >= MatchThresholdSettings.LidarrAlbumMatchMinimum)
            .When(c => c.ReplacingAlbumMatchMinimum >= 1)
            .WithMessage(MatchThresholdSettings.ReplacingBelowDefaultWarning)
            .AsWarning();

        RuleFor(c => c.ReplacingAlbumMatchMinimum)
            .Must((c, v) => v >= c.AlbumMatchMinimum && c.ReplacingTrackMatchMinimum >= c.TrackMatchMinimum)
            .When(c => c.ReplacingAlbumMatchMinimum >= 1)
            .WithMessage(MatchThresholdSettings.ReplacingLooserThanMissingWarning)
            .AsWarning();
    }

    private void Range(Expression<Func<MatchThresholdSettings, int>> field, string label)
    {
        RuleFor(field).InclusiveBetween(1, 100).WithMessage($"{label} must be between 1 and 100");
    }

    private void AlbumWarnings(Expression<Func<MatchThresholdSettings, int>> field)
    {
        var value = field.Compile();
        RuleFor(field)
            .GreaterThanOrEqualTo(70)
            .When(c => value(c) >= 1)
            .WithMessage(MatchThresholdSettings.AlbumFarBelowDefaultWarning)
            .AsWarning();
        RuleFor(field)
            .GreaterThanOrEqualTo(MatchThresholdSettings.LidarrAlbumMatchMinimum)
            .When(c => value(c) >= 70)
            .WithMessage(MatchThresholdSettings.AlbumBelowDefaultWarning)
            .AsWarning();
    }

    private void TrackWarning(Expression<Func<MatchThresholdSettings, int>> field)
    {
        var value = field.Compile();
        RuleFor(field)
            .GreaterThanOrEqualTo(MatchThresholdSettings.LidarrTrackMatchMinimum)
            .When(c => value(c) >= 1)
            .WithMessage(MatchThresholdSettings.TrackBelowDefaultWarning)
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
        return new MatchThresholds(ToDistance(albumMatchMinimum), ToDistance(trackMatchMinimum));
    }

    private static double ToDistance(int percent)
    {
        // (100 - percent) / 100.0 gives exactly 0.2 for 80, matching Lidarr's constant;
        // 1.0 - 0.8 would give 0.19999999999999996 and reject a match Lidarr accepts.
        return (100 - Math.Clamp(percent, 1, 100)) / 100.0;
    }
}
