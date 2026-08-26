using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>
/// Ob die Eingabeseite neu gebaut werden muss.
///
/// Der Anlass war ein Fehler beim Schreiben: Jede fertige Ausgabevorschau
/// setzte die Seitenliste neu, das löste die Auswahl erneut aus, und die
/// Eingabeseite wurde komplett neu gebaut. Wer gerade tippte, verlor den
/// Cursor, und der aufgeklappte Abschnitt klappte wieder zu — mitten im Wort.
///
/// Neu gebaut wird deshalb nur, wenn sich die gezeigten Vorlagenseiten wirklich
/// ändern. Die Eingaben brauchen keinen Neuaufbau: Sie lesen und schreiben
/// direkt am Dossier, und die Listen frischen ihre Zeilen selbst auf.
///
/// Verglichen wird die Identität der Seiten, nicht ihre Nummer. Ein neu
/// eingelesenes Dokument bringt andere Objekte mit; dann sind auch die Eingaben
/// neu daran zu binden.
/// </summary>
public static class DossierPreviewFieldRebuild
{
    public static bool IstNoetig(
        IReadOnlyList<DossierPreviewPage>? gebaut,
        IReadOnlyList<DossierPreviewPage> gewuenscht)
        => gebaut is null || !gebaut.SequenceEqual(gewuenscht);
}
