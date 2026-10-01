namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Temporal-Voting fuer Klassifikator-Codes: Ein Code gilt erst als bestaetigt,
/// wenn er in mehreren aufeinanderfolgenden Frame-Entscheidungen innerhalb eines
/// Meterfensters konsistent auftritt. Daempft Einzelbild-Ausreisser — die
/// Hauptfehlerquelle der LEER→Befund-Kipper (Paket 2, Schritt 5).
/// Reine C#-Logik (Thin-AI), zustandsbehaftet pro Video-Lauf.
/// </summary>
public interface ITemporalCodeVotingService
{
    /// <summary>
    /// Registriert die Klassifikator-Entscheidung eines Frames (code=null fuer
    /// "keine Entscheidung") und liefert den bestaetigten Code oder null,
    /// solange keine Mehrheit im Fenster besteht. Ein Frame mit eigenem, vom bestaetigten
    /// abweichenden Code erhaelt nie den fremden bestaetigten Code (Entscheid 01.10.2026).
    /// </summary>
    string? RegisterAndVote(string? code, double meter);

    /// <summary>Setzt das Fenster zurueck (neuer Video-Lauf).</summary>
    void Reset();
}
