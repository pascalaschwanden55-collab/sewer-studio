using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DataPageViewModel
{
    [RelayCommand(CanExecute = nameof(KannBearbeitungUmschalten))]
    private void BearbeitungUmschalten(HaltungRecord? record)
    {
        // Auch direkte Befehlsaufrufe nach Projektwechsel oder Loeschen absichern.
        if (!KannBearbeitungUmschalten(record)) return;
        record!.BearbeitungErledigt = !record.BearbeitungErledigt;
        record.ModifiedAtUtc = DateTime.UtcNow;
        _shell.MarkProjectDirty();
        ScheduleAutoSave();
    }

    private bool KannBearbeitungUmschalten(HaltungRecord? record)
        => !_disposed && _shell.IsProjectReady && record is not null && _shell.Project.Data.Contains(record);
}
