namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Unveraenderliche Momentaufnahme eines Schachts fuer den PDF-Nachlauf.
/// So kann das Lesen der PDFs im Hintergrund laufen, ohne UI-Daten anzufassen.
/// </summary>
public sealed record SchachtStammdatenQuelle(
    Guid RecordId,
    string Schachtnummer,
    string PdfPath,
    string Link,
    string Schachtform,
    string Dimension,
    string Schachttiefe);

/// <summary>Nur die fehlenden Werte, die nach erfolgreichem PDF-Lesen gesetzt werden duerfen.</summary>
public sealed record SchachtStammdatenErgaenzung(
    Guid RecordId,
    string PdfPath,
    string? Schachtform,
    string? Dimension,
    string? Schachttiefe);

public sealed record SchachtStammdatenErgaenzungsFortschritt(
    int Aktuell,
    int Gesamt,
    string Schachtnummer,
    string Meldung);

public sealed record SchachtStammdatenErgaenzungsErgebnis(
    int Gesamt,
    int BereitsVollstaendig,
    int PdfGefunden,
    int MitErgaenzung,
    int PdfNichtGefunden,
    int NichtLesbar,
    IReadOnlyList<SchachtStammdatenErgaenzung> Ergaenzungen,
    IReadOnlyList<string> Meldungen);

/// <summary>
/// Ermittelt fehlende Schachtform, Dimension und Schachttiefe aus den bereits
/// im Projekt vorhandenen Schachtprotokollen. Der Dienst veraendert das Projekt nicht.
/// </summary>
public interface ISchachtStammdatenErgaenzungsService
{
    SchachtStammdatenErgaenzungsErgebnis Ermitteln(
        string projektOrdner,
        IReadOnlyList<SchachtStammdatenQuelle> schaechte,
        IProgress<SchachtStammdatenErgaenzungsFortschritt>? fortschritt = null,
        CancellationToken cancellationToken = default);
}
