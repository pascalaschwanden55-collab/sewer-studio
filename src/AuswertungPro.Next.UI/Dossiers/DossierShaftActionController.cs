using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Dossiers;

/// <summary>
/// Fuehrt Aktionen fuer eine Dossier-Schachtzeile immer auf dem aktuellen
/// Projektdatensatz aus. So bleibt auch nach einem Neuladen die eindeutige ID
/// und nicht eine veraltete Anzeigezeile massgebend.
/// </summary>
public sealed class DossierShaftActionController
{
    private readonly Func<Project> _getProject;
    private readonly IDialogService _dialogs;
    private readonly Action<SchachtRecord> _openProtocol;
    private readonly Action<SchachtRecord> _navigateToShaft;

    public DossierShaftActionController(
        Func<Project> getProject,
        IDialogService dialogs,
        Action<SchachtRecord> openProtocol,
        Action<SchachtRecord> navigateToShaft)
    {
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _openProtocol = openProtocol ?? throw new ArgumentNullException(nameof(openProtocol));
        _navigateToShaft = navigateToShaft ?? throw new ArgumentNullException(nameof(navigateToShaft));
    }

    public void OpenProtocol(Guid shaftId)
        => Execute(shaftId, _openProtocol);

    public void NavigateToShaft(Guid shaftId)
        => Execute(shaftId, _navigateToShaft);

    private void Execute(Guid shaftId, Action<SchachtRecord> action)
    {
        var record = _getProject().SchaechteData.FirstOrDefault(item => item.Id == shaftId);
        if (record is null)
        {
            _dialogs.Warn(
                "Dieser Schacht ist im aktuellen Projekt nicht mehr vorhanden.",
                "Schacht");
            return;
        }

        action(record);
    }
}
