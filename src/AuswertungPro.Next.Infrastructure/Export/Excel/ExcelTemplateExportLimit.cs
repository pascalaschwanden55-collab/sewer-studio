using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Export.Excel;

/// <summary>Schuetzt den speichergebundenen ClosedXML-Export vor unbegrenzten Datenmengen.</summary>
internal static class ExcelTemplateExportLimit
{
    internal const int MaxRecords = 20_000;

    public static Result? RejectIfExceeded(int recordCount, string recordLabel, string errorCode)
    {
        if (recordCount <= MaxRecords)
            return null;

        return Result.Fail(
            errorCode,
            $"Excel-Export abgebrochen: {recordCount} {recordLabel} ueberschreiten die sichere " +
            $"Obergrenze von {MaxRecords} Zeilen. Bitte das Projekt oder den Export aufteilen.");
    }
}
