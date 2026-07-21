using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Ergebnis des Parsens EINES Schacht-Protokoll-PDFs. UI-frei, damit es im
/// ViewModel (Kollisionspruefung) und beim Anwenden wiederverwendet werden kann.
/// </summary>
public sealed record SchachtProtocolParseResult(
    bool IstSchachtprotokoll,
    string? Schachtnummer,
    string? Datum,
    string? Funktion,
    string? Schachtform,
    string? Dimension,
    string? Schachttiefe,
    string? PrimaereSchaeden,
    string? Bemerkungen,
    string? Status,
    string? Link,
    IReadOnlyList<(string Bauteil, string Schaden)> Schaeden,
    string? Lesehinweis = null);

/// <summary>
/// Liest ein einzelnes Schacht-Protokoll-PDF und wendet es auf einen Schacht an
/// (Felder + Schaeden). Verteilt die PDF-Datei in die kanonische Projektstruktur.
/// Bewusst schlank und ohne UI, damit die Kernlogik testbar bleibt; Dialoge
/// (Warnung, Kollisions-Nachfrage) orchestriert das ViewModel.
/// </summary>
public interface ISchachtProtocolImportService
{
    /// <summary>Liest die PDF, prueft ob Schachtprotokoll, liefert Felder + Schaeden. Ohne Seiteneffekt.</summary>
    SchachtProtocolParseResult Parse(string pdfPfad);

    /// <summary>Findet einen Schacht per Schachtnummer (Aliase Schachtnummer/Nr./NR.). Null wenn keiner passt.</summary>
    SchachtRecord? FindSchacht(Project project, string? schachtnummer);

    /// <summary>Schreibt Felder + Schaeden + PDF_Path auf den gegebenen Record (baut ihn komplett neu auf).</summary>
    void Apply(SchachtRecord ziel, SchachtProtocolParseResult ergebnis, string pdfPfadFuerFeld);

    /// <summary>Kopiert die PDF ins Projekt und gibt den relativen Projektpfad zurueck.</summary>
    string DistributePdf(string projektOrdner, string schachtnummer, string pdfQuelle);
}

/// <summary>
/// Optionale, additive Erweiterung fuer Importdienste, die neben dem bisherigen
/// Pfad auch sicher melden koennen, ob eine neue Projektdatei angelegt wurde.
/// </summary>
public interface ISchachtProtocolDistributionResultService
{
    SchachtProtocolDistributionResult DistributePdfWithResult(
        string projektOrdner,
        string schachtnummer,
        string pdfQuelle);
}

public sealed record SchachtProtocolDistributionResult(
    string RelativePath,
    bool FileCreated);
