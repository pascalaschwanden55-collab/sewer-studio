using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Infrastructure.Export;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using AuswertungPro.Next.Infrastructure.Import.Kins;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.UI.Modules;

/// <summary>
/// Phase 5.2.A: Erste Modul-Extraktion aus ServiceProvider.cs.
/// Buendelt die 7 Domain-IO-Services (Projects + Import + Export).
/// Etabliert das Modul-Pattern fuer die weiteren Phasen 5.2.B-G.
/// </summary>
internal static class ImportExportModule
{
    /// <summary>
    /// Wired-Up-Bundle der 7 Import/Export-Services.
    /// KinsImport haengt an WinCanImport + IbakImport (gleicher Konstruktor wie vorher).
    /// </summary>
    public sealed record Services(
        IProjectRepository Projects,
        IPdfImportService PdfImport,
        IXtfImportService XtfImport,
        IWinCanDbImportService WinCanImport,
        IIbakImportService IbakImport,
        IKinsImportService KinsImport,
        IExcelExportService ExcelExport);

    /// <summary>
    /// Erzeugt die Services in der korrekten Reihenfolge (Kins braucht WinCan + Ibak).
    /// Reine Konstruktor-Aufrufe, keine IO/Warmup.
    /// </summary>
    public static Services Configure()
    {
        var winCan = new WinCanDbImportService();
        var ibak = new IbakExportImportService();

        return new Services(
            Projects: new JsonProjectRepository(),
            PdfImport: new PdfImportServiceAdapter(),
            XtfImport: new XtfImportServiceAdapter(),
            WinCanImport: winCan,
            IbakImport: ibak,
            KinsImport: new KinsImportService(winCan, ibak),
            ExcelExport: new ExcelTemplateExportService());
    }
}
