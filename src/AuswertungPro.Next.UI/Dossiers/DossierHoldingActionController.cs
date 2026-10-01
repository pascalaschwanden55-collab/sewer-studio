using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Dossiers;

/// <summary>
/// Fuehrt Aktionen fuer eine Dossier-Haltungszeile immer auf dem aktuellen
/// Projektdatensatz aus. So bleiben Dossier-Zeile und Originalhaltung getrennt.
/// </summary>
public sealed class DossierHoldingActionController
{
    private readonly Func<Project> _getProject;
    private readonly IDialogService _dialogs;
    private readonly Action<HaltungRecord> _playVideo;
    private readonly Action<HaltungRecord> _openProtocol;
    private readonly Action<HaltungRecord> _navigateToHolding;

    public DossierHoldingActionController(
        Func<Project> getProject,
        IDialogService dialogs,
        Action<HaltungRecord> playVideo,
        Action<HaltungRecord> openProtocol,
        Action<HaltungRecord> navigateToHolding)
    {
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _playVideo = playVideo ?? throw new ArgumentNullException(nameof(playVideo));
        _openProtocol = openProtocol ?? throw new ArgumentNullException(nameof(openProtocol));
        _navigateToHolding = navigateToHolding ?? throw new ArgumentNullException(nameof(navigateToHolding));
    }

    public void PlayVideo(Guid holdingId)
        => Execute(holdingId, _playVideo);

    public void OpenProtocol(Guid holdingId)
        => Execute(holdingId, _openProtocol);

    public void NavigateToHolding(Guid holdingId)
        => Execute(holdingId, _navigateToHolding);

    private void Execute(Guid holdingId, Action<HaltungRecord> action)
    {
        var record = _getProject().Data.FirstOrDefault(item => item.Id == holdingId);
        if (record is null)
        {
            _dialogs.Warn(
                "Diese Haltung ist im aktuellen Projekt nicht mehr vorhanden.",
                "Haltung");
            return;
        }

        action(record);
    }
}
