using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SettingsPageViewModel
{
    private readonly IBackupAdditionalFolders? _backupAdditionalFolders;
    [ObservableProperty] private string _additionalBackupFoldersText = "";

    partial void OnAdditionalBackupFoldersTextChanged(string value)
    {
        try { _backupAdditionalFolders?.Save(value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)); }
        catch (Exception ex)
        {
            var message = UserError.DescribeAndReport(ex, "Zusätzliche Sicherungsordner");
            _dialogs.Error(message, "Datensicherung");
        }
    }
}
