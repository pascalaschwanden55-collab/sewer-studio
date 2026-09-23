using System.Threading.Tasks;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// «Vom WebGIS holen» fuer die Schaechte (23.09.2026) — derselbe Ablauf wie bei den Haltungen.
/// Haengt wie die uebrigen aendernden Aktionen an <see cref="CanMutateShaftData"/>.
/// </summary>
public sealed partial class SchaechtePageViewModel
{
    private WebGisHolenAblauf? _webGisHolen;

    [RelayCommand]
    private async Task WebGisHolenAsync()
    {
        if (_webGisHolen is null || !CanMutateShaftData) return;
        await _webGisHolen.OeffneAsync(_shell, () => Settings.LastProjectPath, () =>
        {
            ScheduleAutoSave();
            FelderExternErgaenzt?.Invoke();
        });
    }
}
