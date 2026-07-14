using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Export;

/// <summary>
/// Baut den sicheren Zielordner fuer ein einzelnes Verteilobjekt.
/// Benutzerdefinierte Ebenen stehen immer vor dem festen Objektordner.
/// </summary>
public interface IDistributionDirectoryTreeResolver
{
    string ResolveObjectDirectory(
        string root,
        string? ordnerPattern,
        string? unterordnerPattern,
        string objektordnerPattern,
        DistributionPatternContext context);
}

/// <summary>
/// Loest optionale Ordnermuster auf und haengt den festen Haltungs-, Schacht-
/// oder Dichtheitspruefungsordner immer als letzte Ebene an.
/// </summary>
public sealed class DistributionDirectoryTreeResolver : IDistributionDirectoryTreeResolver
{
    private readonly IDistributionPatternResolver _patternResolver;

    public DistributionDirectoryTreeResolver()
        : this(new DistributionPatternResolver())
    {
    }

    public DistributionDirectoryTreeResolver(IDistributionPatternResolver patternResolver)
    {
        _patternResolver = patternResolver ?? throw new ArgumentNullException(nameof(patternResolver));
    }

    public string ResolveObjectDirectory(
        string root,
        string? ordnerPattern,
        string? unterordnerPattern,
        string objektordnerPattern,
        DistributionPatternContext context)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Der Ziel-Wurzelordner darf nicht leer sein.", nameof(root));

        ArgumentNullException.ThrowIfNull(context);

        var segmente = new List<string> { root };
        AddOptionalSegment(segmente, ordnerPattern, context);
        AddOptionalSegment(segmente, unterordnerPattern, context);

        // Diese letzte Ebene ist absichtlich nicht konfigurierbar. Darauf bauen
        // Video-Zuordnung und Projektpfade auf.
        var objektordner = _patternResolver.ResolveSegment(objektordnerPattern, context);
        segmente.Add(ProjectPathResolver.SanitizePathSegment(objektordner));

        return Path.Combine(segmente.ToArray());
    }

    private void AddOptionalSegment(
        ICollection<string> segmente,
        string? pattern,
        DistributionPatternContext context)
    {
        var aufgeloest = _patternResolver.ResolveSegment(pattern, context);
        if (string.IsNullOrWhiteSpace(aufgeloest))
            return;

        segmente.Add(ProjectPathResolver.SanitizePathSegment(aufgeloest));
    }
}
