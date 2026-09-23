using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// «Vom WebGIS holen» fuer alle Einstiege (Haltungen, Schaechte, Export-Seite). Oeffnet EIN
/// nicht-modales Fenster (<see cref="WebGisHolenWindow"/>); ein zweiter Aufruf holt es nach vorn
/// und prueft neu. Der Ablauf selbst liegt in <see cref="WebGisImportUseCase"/>; hier stehen nur
/// Fenster, Projektbindung, Sprung zum Objekt und Bericht. Die Anmeldung bleibt auf der Export-Seite.
/// </summary>
public sealed class WebGisHolenAblauf
{
    private readonly Func<bool> _angemeldet;
    private readonly Func<WebGisImportUseCase> _useCase;
    private readonly IDialogService _dialogs;
    private WebGisHolenWindow? _fenster;
    private Action? _geaendert;

    public WebGisHolenAblauf(Func<bool> angemeldet, Func<WebGisImportUseCase> useCase, IDialogService dialogs)
    {
        _angemeldet = angemeldet ?? throw new ArgumentNullException(nameof(angemeldet));
        _useCase = useCase ?? throw new ArgumentNullException(nameof(useCase));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    public bool Angemeldet => _angemeldet();

    /// <summary>
    /// Oeffnet das Fenster (oder holt es nach vorn) und liest. <paramref name="projektPfad"/> liefert
    /// den Ablageort des Berichts; <paramref name="geaendert"/> laeuft, wenn etwas uebernommen wurde.
    /// </summary>
    public async Task OeffneAsync(ShellViewModel shell, Func<string?> projektPfad, Action geaendert)
    {
        ArgumentNullException.ThrowIfNull(shell);
        _geaendert = geaendert;
        if (!_angemeldet())
        {
            _dialogs.Info("Bitte zuerst auf der Seite «Export» am WebGIS anmelden (Knopf «Am WebGIS anmelden»). "
                + "Die Sitzung gilt danach auch hier.", "Vom WebGIS holen");
            return;
        }
        if (_fenster is { IsLoaded: true })
        {
            _fenster.Activate();
            await _fenster.PruefeAsync();
            return;
        }

        Project? gelesenFuer = null;
        var fenster = new WebGisHolenWindow(
            pruefen: async () =>
            {
                if (!_angemeldet() || !shell.IsProjectReady) return null;
                var projekt = shell.Project;
                var ordner = Ablageordner(projektPfad()); // vor dem Lesen binden (Projektwechsel)
                var plan = await _useCase().BauePlanAsync(projekt);
                SchreibeBericht(ordner, WebGisImportBericht.Details(plan));
                gelesenFuer = projekt;
                return plan;
            },
            uebernehmen: plan =>
            {
                if (gelesenFuer is null || !ReferenceEquals(gelesenFuer, shell.Project))
                    return "Projekt gewechselt — nichts übernommen.";
                var n = WebGisImportUseCase.Uebernimm(plan, gelesenFuer);
                if (n > 0)
                {
                    shell.MarkProjectDirty();
                    _geaendert?.Invoke();
                }
                return $"{n} Änderungen übernommen. Bitte das Projekt speichern.";
            },
            oeffnen: (art, id) =>
            {
                var projekt = shell.Project;
                if (art == WebGisObjektart.Haltung)
                    shell.NavigateToHolding(projekt.Data.FirstOrDefault(h => h.Id == id));
                else
                    shell.NavigateToShaft(projekt.SchaechteData.FirstOrDefault(s => s.Id == id));
            });
        var besitzer = System.Windows.Application.Current?.MainWindow;
        if (besitzer is not null && besitzer.IsLoaded) fenster.Owner = besitzer;
        else fenster.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        fenster.Closed += (_, _) => _fenster = null;
        _fenster = fenster;
        fenster.Show();
        await fenster.PruefeAsync();
    }

    /// <summary>__WebGIS_Export im Projektordner (wie beim Senden); null ohne Projektpfad.</summary>
    public static string? Ablageordner(string? projektPfad)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projektPfad)) return null;
            var wurzel = Directory.Exists(projektPfad) ? projektPfad : Path.GetDirectoryName(projektPfad);
            if (string.IsNullOrWhiteSpace(wurzel)) return null;
            if (string.Equals(Path.GetFileName(wurzel), "Projektdateien", StringComparison.OrdinalIgnoreCase))
                wurzel = Path.GetDirectoryName(wurzel) ?? wurzel;
            var ordner = Path.Combine(wurzel, "__WebGIS_Export");
            Directory.CreateDirectory(ordner);
            return ordner;
        }
        catch (Exception)
        {
            return null; // Bericht ist Beilage; das Holen haengt nicht daran.
        }
    }

    private static void SchreibeBericht(string? ordner, string text)
    {
        try
        {
            if (ordner is null) return;
            File.WriteAllText(Path.Combine(ordner, $"WebGIS_Holen-Vorschau_{DateTime.Now:yyyyMMdd_HHmmss}.txt"), text);
        }
        catch (Exception)
        {
            // Bericht ist Beilage; das Holen haengt nicht daran.
        }
    }
}
