using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer.
/// Neue Produktionspfade verwenden <see cref="IImportRunReportExporter"/>.
/// </summary>
public static class ImportRunReportExporter
{
    private static readonly IImportRunReportExporter Default = new ImportRunReportFileExporter();

    public static string Export(ImportRunLog log, string reportDirectory)
        => Default.Export(log, reportDirectory);
}
