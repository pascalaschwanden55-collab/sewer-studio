using System.Windows.Input;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Was eine Taste in der Aufklapp-Liste bewirkt.</summary>
public enum AufklappTastenAktion
{
    /// <summary>Nichts; die Taste gehoert jemand anderem (Pfeiltasten der Liste, Eingabefeld).</summary>
    Nichts,

    /// <summary>Die gewaehlte Haltung auf- oder zuklappen.</summary>
    Schalten,

    /// <summary>Die aufgeklappte Haltung zuklappen.</summary>
    Zuklappen,

    /// <summary>Den Tastaturfokus aus dem Eingabefeld auf die Zeile holen.</summary>
    FokusAufZeile
}

/// <summary>
/// Nova, Aufklapp-Liste: die Tastaturregel als reine Entscheidung, ohne Baum und ohne Ereignis.
///
/// Der wichtigste Punkt ist Escape. Steht der Fokus in einem Eingabefeld des aufgeklappten
/// Formulars, darf Escape die Haltung NICHT zuklappen — das Formular verschwaende sonst unter
/// dem Cursor, und die gerade getippte Eingabe waere weg, weil sie erst beim Fokusverlust
/// zurueckgeschrieben wird. Der erste Escape holt deshalb nur den Fokus auf die Zeile (dabei
/// schreibt das Feld ueber den normalen Weg zurueck); erst ein zweiter Escape, jetzt auf der
/// Zeile selbst, klappt zu.
///
/// Enter und Leertaste gelten ebenfalls nur auf der Zeile: Im Formular gehoert die Leertaste
/// dem Eingabefeld. Die Pfeiltasten bleiben ganz bei der ListBox — eine Auswahl per Pfeiltaste
/// klappt bewusst nicht auf.
/// </summary>
public static class HaltungAufklappTastenregel
{
    /// <param name="inEinerZeile">Kam die Taste aus einer Zeile der Liste (oder deren Formular)?</param>
    /// <param name="aufDerZeile">Hatte die Zeile selbst den Fokus (nicht ein Feld darin)?</param>
    public static AufklappTastenAktion Bestimme(Key taste, bool inEinerZeile, bool aufDerZeile)
    {
        if (!inEinerZeile)
            return AufklappTastenAktion.Nichts;

        return taste switch
        {
            Key.Escape => aufDerZeile ? AufklappTastenAktion.Zuklappen : AufklappTastenAktion.FokusAufZeile,
            Key.Enter or Key.Space when aufDerZeile => AufklappTastenAktion.Schalten,
            _ => AufklappTastenAktion.Nichts
        };
    }
}
