using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>
/// Bringt Werte aus Bestandsprojekten auf die Begriffe der Norm.
///
/// Frueher fuehrte das Programm eigene Kurzformen der Nutzungsart ("Schmutzwasser"), die
/// beim Export Werte erzeugten, die kein INTERLIS-Pruefer akzeptiert. Die Schreibweise
/// wird deshalb beim Laden und beim Speichern nachgezogen.
///
/// Seit 2026-08-29 gilt dasselbe fuer Rohrmaterial, Schachtfunktion und Schachtmaterial.
/// Die Auswahlmenues fuehren nur noch die Begriffe der Norm; ohne Anhebung zeigte ein
/// Bestandsprojekt dort leer an, obwohl ein Wert gespeichert ist. Gemessen an Zone 1.15
/// betraf das 19 Haltungen und 10 Schaechte mit "Beton Normalbeton".
/// Seit 2026-09-03 werden auch Profil- und Schachtformen auf die lesbare Uri-Auswahl
/// gebracht.
/// Seit 2026-09-23 (Entscheid Pascal, Schritt A) hebt er die Felder aus WebGisBegriffe auf die
/// WebGIS-Beschriftung, zeichengenau; ein Wert ohne WebGIS-Gegenstueck bleibt stehen.
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
        if (project is null)
            return 0;

        var geaendert = 0;

        foreach (var record in project.Data ?? [])
        {
            if (record is null) continue;
            // Material folgt in Schritt B; bis dahin die bisherige Normschreibweise.
            geaendert += Hebe(record.Fields, FieldKeys.PipeMaterial, MaterialVokabular.Normalisieren);
            // Seit 23.09.2026 (Entscheid Pascal): die WebGIS-Beschriftung, zeichengenau.
            foreach (var feld in WebGisBegriffe.HaltungFelder)
                geaendert += Hebe(record.Fields, feld, w => WebGisBegriffe.Normalisieren(false, feld, w));
        }

        foreach (var record in project.SchaechteData ?? [])
        {
            if (record is null) continue;
            geaendert += Hebe(record.Fields, "Material", SchachtMaterialVokabular.Normalisieren);
            geaendert += Hebe(record.Fields, FieldKeys.ShaftShape, SchachtformVokabular.Normalisieren);
            foreach (var feld in WebGisBegriffe.SchachtFelder)
                geaendert += Hebe(record.Fields, SchachtFeldnamen.Feld(record, feld),
                    w => WebGisBegriffe.Normalisieren(true, feld, w));

            // Nur der Normschacht hat die WebGIS-Funktionsliste; Spezialbauwerk und
            // Versickerungsanlage behalten ihre Normfunktion, bis deren Listen erhoben sind.
            var funktionsfeld = SchachtFeldnamen.Feld(record, WebGisBegriffe.SchachtFunktion);
            var art = record.GetFieldValue(SchachtFeldnamen.Feld(record, FieldKeys.ShaftStructureType));
            var normschacht = AbwasserbauwerkVokabular.Klasse(art, record.GetFieldValue(funktionsfeld)) == "Normschacht";
            geaendert += normschacht
                ? Hebe(record.Fields, funktionsfeld, w => WebGisBegriffe.Normalisieren(true, WebGisBegriffe.SchachtFunktion, w))
                : Hebe(record.Fields, funktionsfeld, SchachtFunktionVokabular.Normalisieren);
        }

        return geaendert;
    }

    /// <summary>
    /// Hebt ein einzelnes Feld auf die Schreibweise des Vokabulars.
    ///
    /// Bewusst direkt auf <c>Fields</c>: <c>SetFieldValue</c> wuerde Quelle und
    /// Zeitstempel ueberschreiben und damit die Herkunft des Werts verfaelschen.
    /// Eine vorhandene Handmarkierung bleibt dadurch ebenfalls unangetastet.
    /// </summary>
    private static int Hebe(
        IDictionary<string, string>? fields,
        string feld,
        Func<string?, string> normalisieren)
    {
        if (fields is null || !fields.TryGetValue(feld, out var alt))
            return 0;

        var neu = normalisieren(alt);
        if (string.Equals(alt, neu, StringComparison.Ordinal))
            return 0;

        fields[feld] = neu;
        return 1;
    }
}
