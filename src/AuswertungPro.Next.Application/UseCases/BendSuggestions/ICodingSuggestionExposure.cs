namespace AuswertungPro.Next.Application.UseCases.BendSuggestions;

/// <summary>
/// Sitzungsgedaechtnis der Beeinflussung: Fuer welche Haltungen wurde in diesem
/// Programmlauf eine Vorschlagsliste angesehen?
///
/// Sobald die Liste einer Haltung sichtbar war, ist die ganze folgende Codierung
/// dieser Haltung beeinflusst — auch an Stellen ohne Vorschlag. Das Wissen
/// "dort hat die KI nichts gemeldet" wirkt genauso wie ein Rahmen im Bild.
/// Belegt am 2026-08-07: Dieselbe Stelle wurde mit sichtbarem Modellrahmen als
/// Bogen codiert und ohne ihn als verschobene Rohrverbindung mit Knick.
///
/// Bewusst nur Sitzungsdauer: Ein Neustart setzt das Gedaechtnis zurueck. Das
/// ist optimistisch, aber die Alternative waere, jede einmal angesehene Haltung
/// dauerhaft aus dem Messbestand zu verbrennen. Wer Messmaterial erzeugen will,
/// oeffnet die Liste nicht — nur so entsteht der unbeeinflusste Bestand, den
/// die Tauschentscheidung (<c>ModelPromotionPolicy</c>) spaeter braucht.
/// </summary>
public interface ICodingSuggestionExposure
{
    /// <summary>
    /// Vermerkt: Die Vorschlagsliste dieser Haltung wurde angesehen. Die
    /// Haltungsnummer wird wie ueberall im Projekt normalisiert
    /// (<c>EvalContaminationGuard.NormalizeHaltungKey</c>).
    /// </summary>
    void MarkExposed(string haltung);

    /// <summary>
    /// True, wenn die Liste dieser Haltung in diesem Programmlauf angesehen
    /// wurde. Eine leere oder unbekannte Haltung gilt als nicht angesehen.
    /// </summary>
    bool WasExposed(string haltung);
}
