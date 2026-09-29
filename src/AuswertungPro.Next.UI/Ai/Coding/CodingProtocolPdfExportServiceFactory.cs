using System;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingProtocolPdfExportServiceFactory
{
    public static CodingProtocolPdfExportService Create(ProtocolPdfExporter exporter)
        => Create((IProtocolPdfExporter)exporter);

    // Bewusst kein Zugriff auf den zentralen Anwendungs-Dienstecontainer hier: der
    // Architektur-Waechter erlaubt diesen Service-Locator ausschliesslich in MainWindow.xaml.cs,
    // und UI/Ai ist fuer neue Ablaufklassen ohnehin eingefroren. Der Standardpfad kommt deshalb
    // aus der gemeinsamen Quelle BerichtsLogoResolver (Optikanalyse 28.09.2026, Aufgabe 15) statt
    // aus IBerichtsMarke/AppSettings; eine per Einstellung ueberschriebene Datei wirkt an dieser
    // einzelnen Stelle (Coding-Modus, "Als PDF exportieren") erst nach einem groesseren, separat
    // zu planenden Umbau des UI/Ai-Composition-Roots.
    public static CodingProtocolPdfExportService Create(IProtocolPdfExporter exporter)
        => new(
            eventCount => CodingProtocolDialogServiceFactory.Create().ConfirmPdfExport(eventCount),
            (record, lastProjectPath, baseDirectory, now) =>
                CodingProtocolPdfExportPlanner.Build(record, lastProjectPath, baseDirectory, now),
            defaultFileName => CodingProtocolPdfSavePathDialogFactory.Create().Show(defaultFileName),
            () => PlayerShellProjectServiceFactory.Create().GetCurrentProject(),
            (project, record, doc, projectRoot, options) =>
                exporter.BuildHaltungsprotokollPdf(project!, record, doc, projectRoot, options),
            (path, pdf) => CodingProtocolPdfFileServiceFactory.Create().SaveAndOpen(path, pdf),
            message => CodingProtocolDialogServiceFactory.Create().ShowPdfExportFailed(message),
            () => PlayerClock.Now(),
            () => AppContext.BaseDirectory);
}
