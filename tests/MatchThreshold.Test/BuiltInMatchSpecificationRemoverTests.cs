using System.Reflection;
using Moq;
using NLog;
using NzbDrone.Core.MediaFiles.TrackImport;
using NzbDrone.Core.MediaFiles.TrackImport.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Plugins.MatchThreshold;
using Xunit;
using RemovalResult = NzbDrone.Core.Plugins.MatchThreshold.BuiltInMatchSpecificationRemover.RemovalResult;

namespace MatchThreshold.Test;

public class BuiltInMatchSpecificationRemoverTests
{
    private static readonly Logger Logger = LogManager.CreateNullLogger();

    [Fact]
    public void Removes_built_in_check_and_keeps_the_rest()
    {
        var builtIn = new CloseAlbumMatchSpecification(Logger);
        var other = new Mock<IImportDecisionEngineSpecification<LocalAlbumRelease>>().Object;
        var configurable = Configurable();
        var maker = DecisionMaker(builtIn, other, configurable);

        Assert.Equal(RemovalResult.Removed, BuiltInMatchSpecificationRemover.RemoveBuiltIn(maker));

        Assert.Equal(new[] { other, configurable }, AlbumSpecifications(maker));
    }

    [Fact]
    public void Second_run_reports_already_removed()
    {
        var maker = DecisionMaker(new CloseAlbumMatchSpecification(Logger), Configurable());

        BuiltInMatchSpecificationRemover.RemoveBuiltIn(maker);

        Assert.Equal(RemovalResult.AlreadyRemoved, BuiltInMatchSpecificationRemover.RemoveBuiltIn(maker));
    }

    [Fact]
    public void Leaves_built_in_check_when_configurable_one_is_missing()
    {
        var builtIn = new CloseAlbumMatchSpecification(Logger);
        var maker = DecisionMaker(builtIn);

        Assert.Equal(RemovalResult.ConfigurableMissing, BuiltInMatchSpecificationRemover.RemoveBuiltIn(maker));
        Assert.Equal(new[] { builtIn }, AlbumSpecifications(maker));
    }

    [Fact]
    public void Reports_unsupported_decision_maker()
    {
        var other = new Mock<IMakeImportDecision>().Object;

        Assert.Equal(RemovalResult.FieldNotFound, BuiltInMatchSpecificationRemover.RemoveBuiltIn(other));
    }

    private static ConfigurableCloseAlbumMatchSpecification Configurable()
    {
        return new ConfigurableCloseAlbumMatchSpecification(new Mock<IMatchThresholdSettingsReader>().Object, new Mock<NzbDrone.Core.MediaFiles.IMediaFileService>().Object, Logger);
    }

    // The real Lidarr class; only the specification lists matter for this test.
    private static ImportDecisionMaker DecisionMaker(params IImportDecisionEngineSpecification<LocalAlbumRelease>[] albumSpecifications)
    {
        return new ImportDecisionMaker(
            new List<IImportDecisionEngineSpecification<LocalTrack>>(),
            albumSpecifications,
            null, null, null, null, null, null, Logger);
    }

    private static List<IImportDecisionEngineSpecification<LocalAlbumRelease>> AlbumSpecifications(ImportDecisionMaker maker)
    {
        var field = typeof(ImportDecisionMaker).GetField(BuiltInMatchSpecificationRemover.FieldName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        return ((IEnumerable<IImportDecisionEngineSpecification<LocalAlbumRelease>>)field.GetValue(maker)!).ToList();
    }
}
