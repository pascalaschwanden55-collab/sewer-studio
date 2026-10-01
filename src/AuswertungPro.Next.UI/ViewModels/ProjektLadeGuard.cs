using System;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>
/// Sperrt Neu/Oeffnen/Projektwechsel, solange ein Projekt geladen wird.
///
/// Anlass (Auditbefund 12, 18.09.2026): Der Dirty-Guard lief VOR dem Laden. Waehrend des
/// Ladens blieben die Befehle bedienbar, sodass ein neuer Entwurf entstehen konnte, den
/// das verspaetete Ergebnis anschliessend ersetzte. Die Uebernahme prueft das inzwischen
/// zusaetzlich; diese Sperre laesst den Fall gar nicht erst entstehen.
/// </summary>
internal sealed class ProjektLadeGuard : IShellOperationGuard
{
    private bool _aktiv;

    public bool IstAktiv => _aktiv;

    public bool CanSaveProjectFromShell => !_aktiv;

    public string ProjectSaveBlockedMessage =>
        "Ein Projekt wird gerade geladen. Bitte warten, bis der Vorgang abgeschlossen ist.";

    public bool AllowsInternalProjectSave => false;

    public bool CanLeaveShellContext => !_aktiv;

    public string LeaveBlockedMessage =>
        "Ein Projekt wird gerade geladen. Bitte warten, bis der Vorgang abgeschlossen ist.";

    public event EventHandler? OperationAvailabilityChanged;

    public void Setze(bool aktiv)
    {
        if (_aktiv == aktiv)
            return;

        _aktiv = aktiv;
        OperationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }
}
