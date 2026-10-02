using System.Threading.Tasks;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// «Vom WebGIS holen» fuer die Haltungen (23.09.2026) — derselbe Ablauf wie auf der Export-Seite
/// (<see cref="WebGisHolenAblauf"/>, nicht-modales Fenster); hier steht nur die Verbindung zur Seite.
/// </summary>
public sealed partial class DataPageViewModel
{
    private readonly WebGisHolenAblauf? _webGisHolen;

    [RelayCommand]
    private async Task WebGisHolenAsync()
    {
        if (_webGisHolen is null || !_shell.IsProjectReady) return;
        await _webGisHolen.OeffneAsync(_shell, () => _settings.LastProjectPath, MeldeUebernahme);
    }
}
