using NzbDrone.Core.Plugins.MatchThreshold;
using Xunit;

namespace MatchThreshold.Test;

public class MatchThresholdSettingsValidationTests
{
    [Fact]
    public void Lidarr_defaults_save_without_warnings()
    {
        var result = new MatchThresholdSettings().Validate();

        Assert.True(result.IsValid);
        Assert.False(result.HasWarnings);
    }

    [Fact]
    public void Album_below_80_warns_but_is_valid()
    {
        var result = new MatchThresholdSettings { AlbumMatchMinimum = 75, AcknowledgeDataLossRisk = true }.Validate();

        Assert.True(result.IsValid);
        Assert.Equal(new[] { MatchThresholdSettings.BelowDefaultWarning }, result.Warnings.Select(w => w.ErrorMessage));
    }

    [Fact]
    public void Album_below_70_gives_the_stronger_warning_only()
    {
        var result = new MatchThresholdSettings { AlbumMatchMinimum = 65, AcknowledgeDataLossRisk = true }.Validate();

        Assert.True(result.IsValid);
        Assert.Equal(new[] { MatchThresholdSettings.AlbumFarBelowDefaultWarning }, result.Warnings.Select(w => w.ErrorMessage));
    }

    [Fact]
    public void Track_below_60_warns_but_is_valid()
    {
        var result = new MatchThresholdSettings { TrackMatchMinimum = 50, AcknowledgeDataLossRisk = true }.Validate();

        Assert.True(result.IsValid);
        Assert.Equal(new[] { MatchThresholdSettings.BelowDefaultWarning }, result.Warnings.Select(w => w.ErrorMessage));
    }

    [Fact]
    public void Out_of_range_is_an_error_without_extra_warnings()
    {
        var result = new MatchThresholdSettings { AlbumMatchMinimum = 0, AcknowledgeDataLossRisk = true }.Validate();

        Assert.False(result.IsValid);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Each_lowered_field_gets_one_warning()
    {
        var result = new MatchThresholdSettings { AlbumMatchMinimum = 75, ReplacingAlbumMatchMinimum = 75, AcknowledgeDataLossRisk = true }.Validate();

        Assert.True(result.IsValid);
        Assert.Equal(
            new[] { nameof(MatchThresholdSettings.AlbumMatchMinimum), nameof(MatchThresholdSettings.ReplacingAlbumMatchMinimum) },
            result.Warnings.Select(w => w.PropertyName).OrderBy(p => p));
        Assert.All(result.Warnings, w => Assert.Equal(MatchThresholdSettings.BelowDefaultWarning, w.ErrorMessage));
    }

    [Theory]
    [InlineData(59, 60, false)]
    [InlineData(60, 60, true)]
    [InlineData(80, 39, false)]
    [InlineData(80, 40, true)]
    [InlineData(101, 60, false)]
    public void Values_below_the_floors_cannot_be_saved(int album, int track, bool valid)
    {
        var result = new MatchThresholdSettings
        {
            AlbumMatchMinimum = album,
            TrackMatchMinimum = track,
            ReplacingAlbumMatchMinimum = Math.Max(album, 80),
            ReplacingTrackMatchMinimum = Math.Max(track, 60),
            AcknowledgeDataLossRisk = true
        }.Validate();

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void A_value_below_the_floor_gets_the_range_error_only()
    {
        var result = new MatchThresholdSettings { AlbumMatchMinimum = 50, AcknowledgeDataLossRisk = true }.Validate();

        Assert.Equal(new[] { $"Must be between {MatchThresholdSettings.AlbumMatchFloor} and 100" }, result.Errors.Select(e => e.ErrorMessage));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Saved_values_below_the_floors_are_raised_to_them()
    {
        var settings = new MatchThresholdSettings { AlbumMatchMinimum = 10, TrackMatchMinimum = 5, AcknowledgeDataLossRisk = true };

        Assert.Equal(MatchThresholds.FromPercentages(60, 40), settings.ToProfile().MissingAlbum);
        Assert.Equal(0.40, settings.ToProfile().MissingAlbum.AlbumDistance);
    }

    [Fact]
    public void Replacing_looser_than_missing_warns()
    {
        var result = new MatchThresholdSettings { AlbumMatchMinimum = 90, ReplacingAlbumMatchMinimum = 85 }.Validate();

        Assert.True(result.IsValid);
        Assert.Equal(new[] { MatchThresholdSettings.ReplacingLooserThanMissingWarning }, result.Warnings.Select(w => w.ErrorMessage));
    }

    [Fact]
    public void Lowered_missing_albums_with_default_replacing_warns_only_about_missing()
    {
        var result = new MatchThresholdSettings { AlbumMatchMinimum = 75, AcknowledgeDataLossRisk = true }.Validate();

        Assert.Equal(new[] { MatchThresholdSettings.BelowDefaultWarning }, result.Warnings.Select(w => w.ErrorMessage));
    }

    [Fact]
    public void Raising_above_default_has_no_warning()
    {
        var result = new MatchThresholdSettings { AlbumMatchMinimum = 90, TrackMatchMinimum = 70, ReplacingAlbumMatchMinimum = 90, ReplacingTrackMatchMinimum = 70 }.Validate();

        Assert.False(result.HasWarnings);
    }
}
