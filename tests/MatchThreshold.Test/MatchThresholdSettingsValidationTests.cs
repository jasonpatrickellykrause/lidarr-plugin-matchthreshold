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
        Assert.Equal(new[] { MatchThresholdSettings.AlbumBelowDefaultWarning }, result.Warnings.Select(w => w.ErrorMessage));
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
        Assert.Equal(new[] { MatchThresholdSettings.TrackBelowDefaultWarning }, result.Warnings.Select(w => w.ErrorMessage));
    }

    [Fact]
    public void Out_of_range_is_an_error_without_extra_warnings()
    {
        var result = new MatchThresholdSettings { AlbumMatchMinimum = 0, AcknowledgeDataLossRisk = true }.Validate();

        Assert.False(result.IsValid);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Replacing_below_80_adds_the_replacement_warning()
    {
        var result = new MatchThresholdSettings { AlbumMatchMinimum = 75, ReplacingAlbumMatchMinimum = 75, AcknowledgeDataLossRisk = true }.Validate();

        Assert.True(result.IsValid);
        Assert.Contains(MatchThresholdSettings.ReplacingBelowDefaultWarning, result.Warnings.Select(w => w.ErrorMessage));
        Assert.DoesNotContain(MatchThresholdSettings.ReplacingLooserThanMissingWarning, result.Warnings.Select(w => w.ErrorMessage));
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

        Assert.Equal(new[] { MatchThresholdSettings.AlbumBelowDefaultWarning }, result.Warnings.Select(w => w.ErrorMessage));
    }

    [Fact]
    public void Raising_above_default_has_no_warning()
    {
        var result = new MatchThresholdSettings { AlbumMatchMinimum = 90, TrackMatchMinimum = 70, ReplacingAlbumMatchMinimum = 90, ReplacingTrackMatchMinimum = 70 }.Validate();

        Assert.False(result.HasWarnings);
    }
}
