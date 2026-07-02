namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Nicht-blockierende Erfolgs-/Status-Meldungen (Toasts unten rechts). Bewusst so einfach
/// wie <see cref="IDialogService"/>. Fehler bleiben absichtlich beim blockierenden Dialog –
/// ein Toast ist fuer Bestaetigungen ("gespeichert", "Export fertig") gedacht, nicht fuer
/// Dinge, die der Nutzer wirklich lesen und quittieren muss.
/// </summary>
public interface IToastService
{
    void Success(string message);
    void Info(string message);
    void Warning(string message);
    void Error(string message);
}
