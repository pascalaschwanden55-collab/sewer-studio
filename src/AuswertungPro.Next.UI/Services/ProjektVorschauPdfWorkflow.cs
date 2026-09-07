using System;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.UseCases.Uebersicht;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Nova-Fixwelle F3: Die duenne Anbindung des Vorschau-PDF an Dialoge, Dateisystem und den
/// PDF-Erzeuger. Der Ablauf selbst liegt in <see cref="ProjektVorschauPdfUseCase"/> — die
/// klassische Uebersicht und die neue Projektuebersicht rufen beide hierhin, damit es keine
/// zweite Fassung desselben Wegs gibt.
/// </summary>
internal static class ProjektVorschauPdfWorkflow
{
    public static async Task<ProjektVorschauPdfErgebnis> AusfuehrenAsync(
        Func<ProjectPreview?> baueVorschau,
        IDialogService dialogs,
        string leerText)
    {
        ArgumentNullException.ThrowIfNull(baueVorschau);
        ArgumentNullException.ThrowIfNull(dialogs);

        var ergebnis = await ProjektVorschauPdfUseCase.AusfuehrenAsync(new ProjektVorschauPdfActions(
            baueVorschau,
            dateiname => dialogs.SaveFile(
                "Projektvorschau PDF speichern",
                "PDF (*.pdf)|*.pdf",
                defaultExt: "pdf",
                defaultFileName: dateiname),
            vorschau => Task.Run(() => ProjectPreviewPdfBuilder.Build(vorschau)),
            async (pfad, bytes) =>
            {
                var ziel = Path.GetFullPath(pfad);
                var ordner = Path.GetDirectoryName(ziel);
                if (!string.IsNullOrWhiteSpace(ordner))
                    Directory.CreateDirectory(ordner);
                await File.WriteAllBytesAsync(ziel, bytes).ConfigureAwait(false);
            }));

        switch (ergebnis.Status)
        {
            case ProjektVorschauPdfStatus.KeineVorschau:
                dialogs.Info(leerText, "Projektvorschau");
                break;
            case ProjektVorschauPdfStatus.Geschrieben:
                dialogs.Info($"PDF erstellt:\n{Path.GetFullPath(ergebnis.Pfad)}", "Projektvorschau");
                break;
            case ProjektVorschauPdfStatus.Fehler:
                dialogs.Error(
                    $"PDF konnte nicht erstellt werden:\n{UserError.DescribeAndReport(ergebnis.Fehler!, "Projektvorschau PDF erstellen")}",
                    "Projektvorschau");
                break;
        }

        return ergebnis;
    }
}
