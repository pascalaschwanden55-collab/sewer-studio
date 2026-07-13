using System;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingProtocolPdfExportServiceFactory
{
    public static CodingProtocolPdfExportService Create(ProtocolPdfExporter exporter)
        => Create((IProtocolPdfExporter)exporter);

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
