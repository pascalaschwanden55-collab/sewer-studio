using AuswertungPro.Next.Application.UseCases.Verteilung;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Einheitliche Rückmeldung nach jeder Verteilung: Bericht im Projekt unter __IMPORT_REPORTS,
/// dazu ein Toast mit «Bericht öffnen» — grün ohne Fehler, gelb mit Fehlern. Vorher stand das
/// Ergebnis nur im Ergebnisfeld und war nach dem nächsten Lauf weg.
/// </summary>
public sealed partial class ExportPageViewModel
{
    private IVerteilberichtAblage? _verteilberichte;

    /// <summary>Zuletzt geschriebener Verteilbericht (für Tests und «Bericht öffnen»).</summary>
    internal string? LetzterVerteilbericht { get; private set; }

    private void MeldeVerteilung(string art, string bericht, int fehler)
    {
        // Derselbe Projektordner wie beim Import (__IMPORT_REPORTS liegt dort). Der Aufrufer
        // hat unmittelbar davor geprueft, dass noch dasselbe Projekt offen ist.
        var projektOrdner = _shell.GetProjectFolder();
        var pfad = string.IsNullOrWhiteSpace(projektOrdner)
            ? null
            : _verteilberichte?.Schreibe(projektOrdner!, art, bericht);
        LetzterVerteilbericht = pfad;

        var text = fehler > 0
            ? $"{art} verteilt, {fehler} Fehler — bitte den Bericht prüfen."
            : $"{art} verteilt.";

        if (pfad is null)
        {
            if (fehler > 0)
                _toasts.Warning(text);
            else
                _toasts.Success(text);
            return;
        }

        LastResult = $"{LastResult}{Environment.NewLine}Bericht: {pfad}";
        void Oeffne()
        {
            if (!Services.SafeShellOpen.TryOpen(pfad, out var oeffnungsFehler))
                _dialogs.Warn(oeffnungsFehler ?? "Der Bericht konnte nicht geöffnet werden.", "Bericht öffnen");
        }
        if (fehler > 0)
            _toasts.Warning(text, "Bericht öffnen", Oeffne);
        else
            _toasts.Success(text, "Bericht öffnen", Oeffne);
    }
}
