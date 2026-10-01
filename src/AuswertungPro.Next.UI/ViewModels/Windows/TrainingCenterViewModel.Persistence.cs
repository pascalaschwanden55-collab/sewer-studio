using System;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public partial class TrainingCenterViewModel
{
    /// <summary>Speichert Faelle und Root-Ordner; Fehler werden im Status und im Log gemeldet.</summary>
    private async Task AutoSaveStateAsync()
    {
        try
        {
            await _store.SaveAsync(
                TrainingCenterSaveRequestFactory.BuildStateWithDefaults(Cases, _rootFolders));
        }
        catch (Exception ex)
        {
            StatusText = "Automatisches Speichern fehlgeschlagen: "
                + UserError.DescribeAndReport(ex, "Training Center automatisch speichern");
        }
    }
}
