using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>
/// Bringt Werte aus Bestandsprojekten auf die Begriffe der Norm.
///
/// Frueher fuehrte das Programm eigene Kurzformen der Nutzungsart ("Schmutzwasser"), die
/// beim Export Werte erzeugten, die kein INTERLIS-Pruefer akzeptiert. Beim Speichern wird
/// die Schreibweise deshalb einmalig nachgezogen.
///
/// Zwei Grenzen gelten dabei fest:
/// <list type="bullet">
///   <item>Nur die Schreibweise aendert sich, nie die Aussage. Ein unbekannter Wert bleibt
///     unveraendert stehen.</item>
///   <item>Die Herkunft bleibt unangetastet: <c>FieldMeta</c> wird nicht angefasst, ein
///     importierter Wert wird also nicht zur Handaenderung. Sonst wuerde die XTF-Revision
///     ploetzlich Felder schreiben, die der Mensch nie bearbeitet hat.</item>
/// </list>
/// </summary>
internal static class ProjectVocabularyNormalizer
{
    /// <summary>Anzahl tatsaechlich angepasster Felder.</summary>
    public static int Normalize(Project? project)
    {
        if (project?.Data is null)
            return 0;

        var geaendert = 0;
        foreach (var record in project.Data)
        {
            if (record?.Fields is null)
                continue;

            if (!record.Fields.TryGetValue(FieldKeys.UsageType, out var alt))
                continue;

            var neu = NutzungsartVokabular.Normalisieren(alt);
            if (string.Equals(alt, neu, StringComparison.Ordinal))
                continue;

            // Bewusst direkt auf Fields: SetFieldValue wuerde Quelle und Zeitstempel
            // ueberschreiben und damit die Herkunft des Werts verfaelschen.
            record.Fields[FieldKeys.UsageType] = neu;
            geaendert++;
        }

        return geaendert;
    }
}
