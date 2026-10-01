using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Dossiers;

/// <summary>
/// Verbindet die Dossier-Schachtaktionen mit demselben sicheren PDF-Weg wie
/// die Seite "Schaechte" und mit der zentralen Seitennavigation.
/// </summary>
internal static class DossierShaftActionFactory
{
    public static DossierShaftActionController Create(
        ShellViewModel shell,
        ServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(services);

        var fileActions = new SchaechteFileActionController(
            services.SchachtFileTargets,
            services.ShellOpen,
            services.ExplorerReveal,
            services.Dialogs);

        return new DossierShaftActionController(
            () => shell.Project,
            services.Dialogs,
            record => fileActions.OpenProtocol(record, services.Settings.LastProjectPath),
            shell.NavigateToShaft);
    }
}
