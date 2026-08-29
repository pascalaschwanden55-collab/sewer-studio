namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Was ein Schreibvorgang an einem Feld bewirkt hat.
///
/// Ohne dieses Ergebnis meldeten Aufrufer Erfolg, obwohl der Schutz den
/// Schreibvorgang abgelehnt hatte: Fotos wurden kopiert und der Schacht zeigte
/// nicht darauf, automatische Werte wurden als ergaenzt gezaehlt, ohne dass sie
/// im Datensatz standen.
/// </summary>
public enum FeldSchreibErgebnis
{
    /// <summary>Der Wert steht jetzt im Feld.</summary>
    Geschrieben,

    /// <summary>Derselbe Wert stand schon da; nichts wurde veraendert.</summary>
    Unveraendert,

    /// <summary>Abgelehnt: das Feld traegt eine Handaenderung, die nicht ueberschrieben wird.</summary>
    HandwertGeschuetzt
}
