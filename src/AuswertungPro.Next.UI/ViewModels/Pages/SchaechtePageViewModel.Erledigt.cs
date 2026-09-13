using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SchaechtePageViewModel
{
    [RelayCommand(CanExecute = nameof(KannBearbeitungUmschalten))]
    private void BearbeitungUmschalten(SchachtRecord? record)
    {
        if (!KannBearbeitungUmschalten(record)) return;
        record!.BearbeitungErledigt = !record.BearbeitungErledigt;
        record.ModifiedAtUtc = DateTime.UtcNow;
        _shell.MarkProjectDirty();
        ScheduleAutoSave();
    }

    private bool KannBearbeitungUmschalten(SchachtRecord? record)
        => _shell.IsProjectReady && CanMutateShaftData
           && record is not null && _shell.Project.SchaechteData.Contains(record);
}
