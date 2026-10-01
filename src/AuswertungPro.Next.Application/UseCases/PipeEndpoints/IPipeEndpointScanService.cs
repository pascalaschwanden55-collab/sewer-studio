namespace AuswertungPro.Next.Application.UseCases.PipeEndpoints;

/// <summary>Fortschritt des Durchlaufs: verarbeitete Bilder von insgesamt.</summary>
public sealed record PipeEndpointScanProgress(int Processed, int Total);

/// <summary>
/// Vorabdurchlauf eines Videos fuer Rohranfang und Rohrende.
///
/// Bewusst ein Vorablauf und keine Live-Einblendung: Etwa jede siebte Angabe ist
/// falsch, und eine falsche Marke waehrend des Codierens kostet mehr Vertrauen,
/// als die richtigen einsparen. Zwei Zeilen zum Bestaetigen sind billiger zu
/// pruefen und lassen sich gegen die Protokolle nachmessen.
/// </summary>
public interface IPipeEndpointScanService
{
    /// <summary>
    /// Nennt die aktuell freigegebenen Klassen samt gemessener Guete. Leer, wenn
    /// keine gueltige Freigabe vorliegt — dann bleibt der Durchlauf gesperrt.
    /// </summary>
    Task<IReadOnlyList<PipeEndpointClass>> GetReleasedClassesAsync(CancellationToken cancellationToken);

    Task<PipeEndpointScanResult> ScanAsync(
        PipeEndpointScanRequest request,
        CancellationToken cancellationToken,
        IProgress<PipeEndpointScanProgress>? progress = null);
}
