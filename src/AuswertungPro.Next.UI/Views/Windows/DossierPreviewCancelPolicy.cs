namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Die Dossier-Vorschau ist die Stelle, an der die Dossiertexte wirklich
/// geschrieben werden. Esc und das Fenster-X warfen sie bisher ohne Rueckfrage
/// weg. Gefragt wird nur, wenn es wirklich etwas zu verlieren gibt - und nie
/// beim Uebernehmen, das dasselbe Schliessen ausloest.
/// </summary>
public static class DossierPreviewCancelPolicy
{
    public static bool NeedsDiscardConfirmation(bool hasChanges, bool isAccepting)
        => hasChanges && !isAccepting;
}
