using AuswertungPro.Next.Application.UseCases.Datenaenderungen;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Optik Aufgabe 16: Rueckgaengig/Wiederholen einer Datenseite — derselbe Ablauf fuer Haltungen und
/// Schaechte (Deepscan 02.10.2026, A2: vorher je Seite eine Kopie). Die Seite prueft ihre eigene
/// Schranke und reicht nur Bereich, Warnung, «geaendert» und die Erfolgsmeldung herein.
///
/// Schlusswelle (Item 4): «nicht vollständig» (<c>Teilweise=true</c>) heisst, das Anwenden UND der
/// Rueckbau der bereits geschriebenen Teile sind mitten im Schritt gescheitert — an den betroffenen
/// Datensaetzen koennen trotzdem Feldwerte stehen geblieben sein. Das muss ins Projekt und in die
/// Anzeige, obwohl der Schritt selbst nicht als Rueckgaengig-Eintrag zaehlt (Angewendet bleibt false).
/// </summary>
internal static class SeitenVerlauf
{
    public static void Wende(
        IDatenaenderungsVerlauf verlauf,
        DatenaenderungsBereich bereich,
        bool rueckgaengig,
        Action<string> warne,
        Action geaendert,
        Action<string> meldeErfolg)
    {
        ArgumentNullException.ThrowIfNull(verlauf);
        ArgumentNullException.ThrowIfNull(warne);
        ArgumentNullException.ThrowIfNull(geaendert);
        ArgumentNullException.ThrowIfNull(meldeErfolg);

        var ergebnis = rueckgaengig ? verlauf.Rueckgaengig(bereich) : verlauf.Wiederholen(bereich);
        if (!ergebnis.Angewendet)
        {
            warne(ergebnis.Meldung);
            if (ergebnis.Teilweise)
                geaendert();
            return;
        }

        geaendert();
        meldeErfolg(ergebnis.Meldung);
    }
}
