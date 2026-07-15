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

    /// <summary>
    /// Wie oben, aber mit Verteil-Variante: bei <see cref="DistributionVariant.Sanierung"/>
    /// wird nach dem festen Objektordner eine weitere feste Ebene
    /// <c>{sanierungDateiPattern}_Saniert {Jahr}</c> angehaengt.
    /// </summary>
    string ResolveObjectDirectory(
        string root,
        string? ordnerPattern,
        string? unterordnerPattern,
        string objektordnerPattern,
        DistributionPatternContext context,
        DistributionVariant variant,
        string? sanierungDateiPattern);
}

/// <summary>
/// Loest optionale Ordnermuster auf und haengt den festen Haltungs-, Schacht-
/// oder Dichtheitspruefungsordner immer als letzte Ebene an. Bei Sanierung folgt
/// eine weitere feste Ebene mit "_Saniert {Jahr}"-Suffix.
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
        => ResolveObjectDirectory(
            root, ordnerPattern, unterordnerPattern, objektordnerPattern, context,
            DistributionVariant.Normal, sanierungDateiPattern: null);

    public string ResolveObjectDirectory(
        string root,
        string? ordnerPattern,
        string? unterordnerPattern,
        string objektordnerPattern,
        DistributionPatternContext context,
        DistributionVariant variant,
        string? sanierungDateiPattern)
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

        // Sanierung: eine weitere feste Ebene {Datei}_Saniert {Jahr} rahmt die
        // Ablage ein. Objektordner und Dateiname darunter bleiben unveraendert,
        // daher bleibt die Video-Zuordnung erhalten (nur eine Ebene tiefer).
        if (variant == DistributionVariant.Sanierung)
        {
            var basis = _patternResolver.ResolveSegment(sanierungDateiPattern, context);
            var jahr = _patternResolver.ResolveSegment("{Jahr}", context);
            var sanierungOrdner = string.IsNullOrWhiteSpace(basis)
                ? $"Saniert {jahr}".TrimEnd()
                : $"{basis}_Saniert {jahr}".TrimEnd();
            segmente.Add(ProjectPathResolver.SanitizePathSegment(sanierungOrdner));
        }

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
