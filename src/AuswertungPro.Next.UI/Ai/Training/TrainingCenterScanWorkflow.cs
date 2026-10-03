using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCenterScanWorkflowRequest(
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    IReadOnlyCollection<string> RootFolders,
    Func<string, bool> DirectoryExists,
    Func<string, ICollection<string>, ICollection<string>, CancellationToken, Task<IReadOnlyList<TrainingCase>>> ScanFolderAsync,
    Action<IReadOnlyList<TrainingCase>> ReplaceCases,
    Action<IReadOnlyList<TrainingCase>> AppendCases,
    Action<string> SetStatusText,
    Func<Task> SaveStateAsync,
    Func<CancellationToken> ResetCancellation,
    Action<string> Log);

public static class TrainingCenterScanWorkflow
{
    public static async Task RunAsync(TrainingCenterScanWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GetIsBusy())
            return;

        if (request.RootFolders.Count == 0)
        {
            request.SetStatusText("Bitte zuerst einen oder mehrere Ordner wählen.");
            return;
        }

        var uebersprungeneOrdner = new List<string>();
        var hinweise = new List<string>();
        var protokolliert = false;

        // Folgepaket 1: Ordner- und Dateihinweise genau einmal ins Protokoll, auch im Abbruchpfad
        // (Abbruch mitten im Lauf oder erst vor dem Speichern); liefert den Zusatz fuer den Status.
        string ProtokolliereHinweise()
        {
            var uebersprungen = UebersprungeneOrdner.Meldungen(uebersprungeneOrdner);
            if (!protokolliert)
            {
                foreach (var meldung in uebersprungen.Concat(hinweise))
                    request.Log(meldung);
                protokolliert = true;
            }

            var teile = new List<string>();
            if (uebersprungen.Count > 0)
                teile.Add($"{uebersprungen.Count} Ordner übersprungen");
            if (hinweise.Count > 0)
                teile.Add(hinweise.Count == 1 ? "1 Dateihinweis" : $"{hinweise.Count} Dateihinweise");
            return teile.Count == 0 ? "" : $" · {string.Join(" · ", teile)} (siehe Protokoll)";
        }

        try
        {
            request.SetIsBusy(true);
            request.SetStatusText("Scanne Ordner...");
            request.ReplaceCases(Array.Empty<TrainingCase>());
            // Deepscan R6: «Abbrechen» wirkt auch auf den Scan; der Dienst prueft je Ordner.
            var cancellationToken = request.ResetCancellation();

            // Momentaufnahme: Waehrend des await kann der Nutzer die Ordnerliste aendern
            // (Ordner wählen/zurücksetzen); gescannt wird die Liste vom Start (Review PR #85).
            var ordner = request.RootFolders.ToArray();
            var allFound = new List<TrainingCase>();
            foreach (var folder in ordner)
            {
                if (!request.DirectoryExists(folder))
                    continue;

                var found = await request.ScanFolderAsync(folder, uebersprungeneOrdner, hinweise, cancellationToken);
                allFound.AddRange(found);
                request.AppendCases(found);
            }

            var withProtocol = allFound.Count(c => !string.IsNullOrEmpty(c.ProtocolPath));
            var pdfOnly = allFound.Count(c =>
                string.IsNullOrEmpty(c.VideoPath) && !string.IsNullOrEmpty(c.ProtocolPath));
            var summary = TrainingCenterDisplayFormatter.FormatScanSummary(
                allFound.Count,
                withProtocol,
                pdfOnly);

            // Deepscan R8 / PR #85: uebersprungene Ordner und Dateihinweise stehen im Protokoll.
            request.SetStatusText(summary + ProtokolliereHinweise());

            // Review PR #85: Ein Abbruch waehrend des letzten Ordners laesst den Dienst normal zurueckkehren;
            // vor dem Speichern deshalb nochmals pruefen, damit kein abgebrochener Scan gespeichert wird.
            cancellationToken.ThrowIfCancellationRequested();
            await request.SaveStateAsync();
        }
        catch (OperationCanceledException)
        {
            // Bereits gefundene Faelle bleiben sichtbar; gespeichert wird ein abgebrochener Scan nicht.
            // Die bis dahin gesammelten Hinweise gehen nicht verloren (Folgepaket 1).
            request.SetStatusText("Scan abgebrochen." + ProtokolliereHinweise());
        }
        catch (Exception)
        {
            // Unerwarteter Fehler: die gesammelten Hinweise noch ins Protokoll, dann wie bisher weiterwerfen
            // (globaler Fehlerdialog mit UserError-Text).
            ProtokolliereHinweise();
            throw;
        }
        finally
        {
            request.SetIsBusy(false);
        }
    }
}
