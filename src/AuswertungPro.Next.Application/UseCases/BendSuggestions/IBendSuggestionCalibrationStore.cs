namespace AuswertungPro.Next.Application.UseCases.BendSuggestions;

/// <summary>
/// Liest den gemessenen Arbeitspunkt eines Bogen-Kandidaten.
/// </summary>
public interface IBendSuggestionCalibrationStore
{
    /// <summary>
    /// Liefert die hinterlegte Kalibrierung oder null, wenn keine vorhanden ist.
    /// Eine vorhandene, aber beschaedigte Kalibrierung wird gemeldet statt
    /// stillschweigend uebergangen — ein Tippfehler darf nicht wie "nicht
    /// hinterlegt" aussehen.
    /// </summary>
    BendSuggestionCalibration? TryRead(string candidateId);
}
