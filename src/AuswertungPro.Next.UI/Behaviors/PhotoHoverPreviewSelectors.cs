using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Behaviors;

/// <summary>
/// UI-freie Foto-Pfad-Selektoren fuer die generalisierte Hover-Foto-Vorschau
/// (<see cref="PhotoHoverPreviewBehavior"/>). Jede Liste haengt per Code-Behind den
/// passenden Selektor an; ohne Selektor greift der ProtocolEntry-Fallback.
/// Konvention: fremder Typ -> null (Fallback greift), eigener Typ -> (ggf. leere) Pfadliste.
/// Bewusst ohne WPF-Abhaengigkeiten, damit die Zuordnung voll unit-testbar bleibt.
/// </summary>
public static class PhotoHoverPreviewSelectors
{
    /// <summary>
    /// Ermittelt die Roh-Fotopfade eines Listeneintrags. Reihenfolge:
    /// 1) Selektor (falls gesetzt und nicht null), 2) <see cref="ProtocolEntry.FotoPaths"/>, 3) leer.
    /// Ein Selektor gewinnt auch dann, wenn er bewusst eine leere Liste liefert.
    /// </summary>
    public static IEnumerable<string> ExtractPhotoPaths(
        object? item,
        Func<object, IEnumerable<string>?>? selector)
    {
        if (item is null)
            return Array.Empty<string>();

        var viaSelector = selector?.Invoke(item);
        if (viaSelector is not null)
            return viaSelector;

        // Rueckwaertskompatibilitaet: die 3 Bestandslisten (ProtocolEntry) laufen ohne Selektor.
        if (item is ProtocolEntry entry)
            return entry.FotoPaths;

        return Array.Empty<string>();
    }

    /// <summary>Codier-/Import-Ereignis -> Fotos des zugehoerigen ProtocolEntry.</summary>
    public static IEnumerable<string>? CodingEventPhotos(object item)
        => item is CodingEvent ev ? ev.Entry.FotoPaths : null;

    /// <summary>Medien-Suchtreffer -> gefundene Foto-Dateien.</summary>
    public static IEnumerable<string>? MediaMatchRowPhotos(object item)
        => item is MediaMatchRow row ? row.Match.FotoPaths : null;

    /// <summary>Trainingssample -> Frame, dann Beweisbild, dann Zusatzbilder (leere/null verworfen).</summary>
    public static IEnumerable<string>? TrainingSamplePhotos(object item)
    {
        if (item is not TrainingSample sample)
            return null;

        var paths = new List<string>();
        if (!string.IsNullOrWhiteSpace(sample.FramePath))
            paths.Add(sample.FramePath);
        if (!string.IsNullOrWhiteSpace(sample.EvidenceFramePath))
            paths.Add(sample.EvidenceFramePath!);
        if (sample.AdditionalFramePaths is { } additional)
            paths.AddRange(additional);
        return paths;
    }

    /// <summary>Review-Queue-Eintrag -> Self-Training-Frame (falls vorhanden).</summary>
    public static IEnumerable<string>? ReviewQueueItemPhotos(object item)
    {
        if (item is not ReviewQueueItem queueItem)
            return null;

        var path = queueItem.SelfTrainingFramePath;
        return string.IsNullOrWhiteSpace(path) ? Array.Empty<string>() : new[] { path! };
    }

    /// <summary>Lehrer-Annotation -> Crop bevorzugt, sonst voller Frame.</summary>
    public static IEnumerable<string>? TeacherAnnotationPhotos(object item)
    {
        if (item is not TeacherAnnotation annotation)
            return null;

        var path = annotation.CroppedRegionPath ?? annotation.FullFramePath;
        return path is null ? Array.Empty<string>() : new[] { path };
    }
}
