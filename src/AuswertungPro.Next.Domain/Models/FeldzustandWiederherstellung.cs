namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Die eine Regel hinter Rueckgaengig/Wiederholen eines Feldes fuer Haltungen und Schaechte
/// (<see cref="HaltungRecord.StelleFeldzustandWiederHer"/>, <see cref="SchachtRecord.StelleFeldzustandWiederHer"/>):
/// Wert UND Herkunftsdaten zeichengenau auf einen frueheren Zustand setzen, <c>null</c> heisst
/// "gab es nicht". Keine Handmarke, kein neuer Zeitstempel (Deepscan 02.10.2026, B6).
/// </summary>
public static class FeldzustandWiederherstellung
{
    /// <exception cref="ArgumentException">Bei einer virtuellen Anzeigespalte (<see cref="VirtuelleSpalte"/>).</exception>
    public static void Anwende(
        Dictionary<string, string> fields,
        Dictionary<string, FieldMetadata> fieldMeta,
        string fieldName,
        string? value,
        FieldMetadata? meta)
    {
        VirtuelleSpalte.WeiseAb(fieldName, nameof(fieldName));
        if (value is null) fields.Remove(fieldName);
        else fields[fieldName] = value;
        if (meta is null) fieldMeta.Remove(fieldName);
        else fieldMeta[fieldName] = FieldMetadataKopie.Von(meta);
    }
}
