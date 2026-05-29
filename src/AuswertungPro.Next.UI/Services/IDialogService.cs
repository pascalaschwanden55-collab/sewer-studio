namespace AuswertungPro.Next.UI;

/// <summary>Ergebnis einer Drei-Wege-Bestaetigung (Ja/Nein/Abbrechen).</summary>
public enum DialogConfirm { Yes, No, Cancel }

public interface IDialogService
{
    string? OpenFile(string title, string filter, string? initialDirectory = null);
    string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null);
    string[] OpenFiles(string title, string filter);
    string? SelectFolder(string title, string? initialPath = null);

    // ── Meldungen / Bestaetigungen (ersetzen direkte MessageBox-Aufrufe in ViewModels) ──

    /// <summary>Reine Info-Meldung (OK, Info-Icon).</summary>
    void Info(string message, string title = "Hinweis");

    /// <summary>Warnung (OK, Warn-Icon).</summary>
    void Warn(string message, string title = "Warnung");

    /// <summary>Fehlermeldung (OK, Fehler-Icon).</summary>
    void Error(string message, string title = "Fehler");

    /// <summary>Ja/Nein-Bestaetigung. true = Ja.</summary>
    bool Confirm(string message, string title = "Bestaetigung");

    /// <summary>Ja/Nein/Abbrechen-Bestaetigung.</summary>
    DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung");
}
