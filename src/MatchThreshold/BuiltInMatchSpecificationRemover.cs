using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NLog;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.MediaFiles.TrackImport;
using NzbDrone.Core.MediaFiles.TrackImport.Specifications;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Plugins.MatchThreshold;

/// <summary>
/// Removes Lidarr's hardcoded CloseAlbumMatchSpecification from the import decision maker at startup.
///
/// Lidarr has no way to unregister a built-in check, and every check in the list must pass, so the
/// configurable copy alone can only make imports stricter. ImportDecisionMaker is a container singleton
/// that keeps its checks in a private field; replacing that field's contents once, before any import
/// runs, leaves the configurable copy as the only album-match check.
///
/// If Lidarr renames the field or the class, nothing is removed and an error is logged. Imports then
/// keep working with Lidarr's built-in 80% threshold.
/// </summary>
public class BuiltInMatchSpecificationRemover : IHandle<ApplicationStartedEvent>
{
    internal const string FieldName = "_albumSpecifications";

    private readonly Lazy<IMakeImportDecision> _importDecisionMaker;
    private readonly Logger _logger;

    public BuiltInMatchSpecificationRemover(Lazy<IMakeImportDecision> importDecisionMaker, Logger logger)
    {
        _importDecisionMaker = importDecisionMaker;
        _logger = logger;
    }

    public void Handle(ApplicationStartedEvent message)
    {
        try
        {
            var result = RemoveBuiltIn(_importDecisionMaker.Value);
            switch (result)
            {
                case RemovalResult.Removed:
                    _logger.Info("Match Threshold: replaced Lidarr's built-in album match check with the configurable one");
                    break;
                case RemovalResult.AlreadyRemoved:
                    _logger.Debug("Match Threshold: built-in album match check was already removed");
                    break;
                case RemovalResult.ConfigurableMissing:
                    _logger.Error("Match Threshold: the configurable album match check is not registered, so Lidarr's built-in check was left in place");
                    break;
                default:
                    _logger.Error("Match Threshold: could not find Lidarr's album match checks ({0}). This Lidarr version is not supported; imports use the built-in 80% threshold.", result);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Match Threshold: failed to replace the built-in album match check; imports use the built-in 80% threshold");
        }
    }

    internal static RemovalResult RemoveBuiltIn(IMakeImportDecision decisionMaker)
    {
        var field = decisionMaker.GetType().GetField(FieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null || !typeof(IEnumerable<IImportDecisionEngineSpecification<LocalAlbumRelease>>).IsAssignableFrom(field.FieldType))
        {
            return RemovalResult.FieldNotFound;
        }

        if (field.GetValue(decisionMaker) is not IEnumerable<IImportDecisionEngineSpecification<LocalAlbumRelease>> current)
        {
            return RemovalResult.FieldNotFound;
        }

        // Materialize once: the container may hand over a lazy enumerable that builds new instances.
        var specifications = current.ToList();

        if (!specifications.Any(s => s is ConfigurableCloseAlbumMatchSpecification))
        {
            // Never remove the built-in check without the replacement; that would disable matching entirely.
            return RemovalResult.ConfigurableMissing;
        }

        if (!specifications.Any(s => s is CloseAlbumMatchSpecification))
        {
            return RemovalResult.AlreadyRemoved;
        }

        var replaced = specifications.Where(s => s is not CloseAlbumMatchSpecification).ToArray();
        field.SetValue(decisionMaker, replaced);
        return RemovalResult.Removed;
    }

    internal enum RemovalResult
    {
        Removed,
        AlreadyRemoved,
        ConfigurableMissing,
        FieldNotFound
    }
}
