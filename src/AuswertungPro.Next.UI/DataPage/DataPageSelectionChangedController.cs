using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageSelectionChangedController
{
    public static void Handle(
        HaltungRecord? selected,
        IEnumerable<IRelayCommand?> commands,
        Action<HaltungRecord> normalizeSelectedFindings,
        Action<HaltungRecord> syncSelectedProtocolFromFindings,
        Action refreshSelectedProtocolEntries)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(normalizeSelectedFindings);
        ArgumentNullException.ThrowIfNull(syncSelectedProtocolFromFindings);
        ArgumentNullException.ThrowIfNull(refreshSelectedProtocolEntries);

        foreach (var command in commands)
            command?.NotifyCanExecuteChanged();

        if (selected is not null)
        {
            normalizeSelectedFindings(selected);
            syncSelectedProtocolFromFindings(selected);
        }

        refreshSelectedProtocolEntries();
    }
}
