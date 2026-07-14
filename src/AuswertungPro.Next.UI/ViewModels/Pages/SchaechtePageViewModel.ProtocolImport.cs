using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SchaechtePageViewModel
{
    private bool CanRefreshProtocol()
        => Selected is not null && !string.IsNullOrWhiteSpace(Selected.GetFieldValue("PDF_Path"));

    private async Task RefreshProtocolAsync()
    {
        var schacht = Selected;
        if (schacht is null)
            return;

        var relPath = schacht.GetFieldValue("PDF_Path");
        if (string.IsNullOrWhiteSpace(relPath))
            return;

        var projektOrdner = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projektOrdner))
        {
            _dialogs.Info("Kein Projekt geoeffnet.", "Aktualisieren");
            return;
        }

        if (!_dialogs.ConfirmWarn(
                "Der Schacht wird komplett aus dem Protokoll neu aufgebaut. Von Hand erfasste Werte gehen dabei verloren. Fortfahren?",
                "Aktualisieren"))
            return;

        var absPath = ProjectPathResolver.ResolveFilePathFromProjectFolder(relPath, projektOrdner);
        if (absPath is null)
        {
            _dialogs.Warn("Die verknuepfte Protokoll-Datei wurde nicht gefunden.", "Aktualisieren");
            return;
        }

        var ergebnis = await ReadProtocolAsync(absPath, "Aktualisieren");
        if (ergebnis is null || !ProjectIsStillOpen(projektOrdner, "Aktualisieren"))
            return;

        if (!ergebnis.IstSchachtprotokoll || string.IsNullOrWhiteSpace(ergebnis.Schachtnummer))
        {
            _dialogs.Warn(
                ResolveReadFailure(ergebnis, "Das verknuepfte PDF ist kein lesbares Schachtprotokoll."),
                "Aktualisieren");
            return;
        }

        _schachtProtocolImport.Apply(schacht, ergebnis, relPath);
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        _shell.TrySaveProject();
        LastResult = $"Schacht {ergebnis.Schachtnummer} aktualisiert ({ergebnis.Schaeden.Count} Beobachtungen).";
    }

    private async Task ImportProtocolAsync()
    {
        var projektOrdner = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projektOrdner))
        {
            _dialogs.Info("Kein Projekt geoeffnet.", "Protokoll importieren");
            return;
        }

        var quelle = _dialogs.ConfirmCancel(
            "Quelle auswaehlen:\n\n" +
            "Ja = einzelne PDF-Datei\n" +
            "Nein = ganzen Ordner einschliesslich Unterordner\n" +
            "Abbrechen = nichts importieren",
            "Protokoll importieren");
        if (quelle == DialogConfirm.Cancel)
            return;

        if (quelle == DialogConfirm.No)
        {
            var ordner = _dialogs.SelectFolder("Ordner mit Schachtprotokollen auswaehlen");
            if (!string.IsNullOrWhiteSpace(ordner))
                await ImportProtocolFolderAsync(projektOrdner, ordner);
            return;
        }

        var pdfPfad = _dialogs.OpenFile("Schachtprotokoll auswaehlen", "PDF (*.pdf)|*.pdf");
        if (!string.IsNullOrWhiteSpace(pdfPfad))
            await ImportSingleProtocolAsync(projektOrdner, pdfPfad);
    }

    private async Task ImportSingleProtocolAsync(string projektOrdner, string pdfPfad)
    {
        var ergebnis = await ReadProtocolAsync(pdfPfad, "Protokoll importieren");
        if (ergebnis is null || !ProjectIsStillOpen(projektOrdner, "Protokoll importieren"))
            return;

        if (!ergebnis.IstSchachtprotokoll)
        {
            _dialogs.Warn(
                ResolveReadFailure(ergebnis, "Das gewaehlte PDF ist kein Schachtprotokoll."),
                "Protokoll importieren");
            return;
        }
        if (string.IsNullOrWhiteSpace(ergebnis.Schachtnummer))
        {
            _dialogs.Warn("Im Protokoll wurde keine Schachtnummer gefunden.", "Protokoll importieren");
            return;
        }

        var ziel = ResolveProtocolTarget(ergebnis);
        if (ziel is null)
            return;

        string relPath;
        try
        {
            LastResult = $"Schacht {ergebnis.Schachtnummer}: PDF wird ins Projekt kopiert ...";
            relPath = await Task.Run(() =>
                _schachtProtocolImport.DistributePdf(projektOrdner, ergebnis.Schachtnummer, pdfPfad));
        }
        catch (Exception ex)
        {
            LastResult = "Protokoll konnte nicht kopiert werden.";
            var userMessage = UserError.DescribeAndReport(ex, "Schachtprotokoll kopieren");
            _dialogs.Warn($"Das PDF konnte nicht ins Projekt kopiert werden:\n{userMessage}", "Protokoll importieren");
            return;
        }

        if (!ProjectIsStillOpen(projektOrdner, "Protokoll importieren"))
            return;

        _schachtProtocolImport.Apply(ziel, ergebnis, relPath);
        if (!Records.Contains(ziel))
        {
            lock (_shell.CollectionLock)
            {
                Records.Add(ziel);
            }
        }
        Selected = ziel;

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        _shell.TrySaveProject();
        LastResult = $"Protokoll importiert: Schacht {ergebnis.Schachtnummer} ({ergebnis.Schaeden.Count} Beobachtungen).";
    }

    private async Task<SchachtProtocolParseResult?> ReadProtocolAsync(string pdfPath, string dialogTitle)
    {
        try
        {
            LastResult = $"PDF wird gelesen: {Path.GetFileName(pdfPath)} ...";
            return await Task.Run(() => _schachtProtocolImport.Parse(pdfPath));
        }
        catch (Exception ex)
        {
            LastResult = "PDF konnte nicht gelesen werden.";
            var userMessage = UserError.DescribeAndReport(ex, "Schachtprotokoll lesen");
            _dialogs.Warn($"Das PDF konnte nicht gelesen werden:\n{userMessage}", dialogTitle);
            return null;
        }
    }

    private SchachtRecord? ResolveProtocolTarget(SchachtProtocolParseResult ergebnis)
    {
        var vorhanden = _schachtProtocolImport.FindSchacht(_shell.Project, ergebnis.Schachtnummer);
        if (vorhanden is null)
            return new SchachtRecord();

        var wahl = _dialogs.ConfirmCancel(
            $"Schacht {ergebnis.Schachtnummer} ist bereits vorhanden.\n\n" +
            "Ja = Ueberschreiben\nNein = Als neuen Schacht anlegen\nAbbrechen = Nichts tun",
            "Protokoll importieren");

        return wahl switch
        {
            DialogConfirm.Yes => vorhanden,
            DialogConfirm.No => new SchachtRecord(),
            _ => null
        };
    }

    private bool ProjectIsStillOpen(string expectedFolder, string dialogTitle)
    {
        if (string.Equals(_shell.GetProjectFolder(), expectedFolder, StringComparison.OrdinalIgnoreCase))
            return true;

        LastResult = "Vorgang abgebrochen: Projekt wurde gewechselt.";
        _dialogs.Warn("Das Projekt wurde waehrend des Einlesens gewechselt. Es wurden keine Daten uebernommen.", dialogTitle);
        return false;
    }

    private static string ResolveReadFailure(SchachtProtocolParseResult ergebnis, string fallback)
        => string.IsNullOrWhiteSpace(ergebnis.Lesehinweis) ? fallback : ergebnis.Lesehinweis;
}
