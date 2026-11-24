using DryIoc;
using NzbDrone.Common.Composition.Extensions;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.MediaFiles.TrackImport;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Plugins.MatchThreshold;
using Xunit;

namespace MatchThreshold.Test;

/// <summary>
/// Registers the plugin assembly the way Lidarr's host does and checks the pieces the swap depends on.
/// </summary>
public class RegistrationTests
{
    [Fact]
    public void Lidarr_container_picks_up_the_configurable_check_and_startup_handler()
    {
        var container = new Container(rules => rules.WithNzbDroneRules())
            .AutoAddServices(new List<System.Reflection.Assembly> { typeof(MatchThresholdPlugin).Assembly });

        var albumChecks = container.GetServiceRegistrations()
            .Where(r => r.ServiceType == typeof(IImportDecisionEngineSpecification<LocalAlbumRelease>))
            .Select(r => r.Factory.ImplementationType)
            .ToList();
        Assert.Contains(typeof(ConfigurableCloseAlbumMatchSpecification), albumChecks);

        var startupHandlers = container.GetServiceRegistrations()
            .Where(r => r.ServiceType == typeof(IHandle<ApplicationStartedEvent>))
            .Select(r => r.Factory.ImplementationType);
        Assert.Contains(typeof(BuiltInMatchSpecificationRemover), startupHandlers);
    }
}
