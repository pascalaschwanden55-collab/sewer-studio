using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;

namespace AuswertungPro.Next.Infrastructure.Export.Excel;

/// <summary>Schuetzt den speichergebundenen ClosedXML-Export vor unbegrenzten Datenmengen.</summary>
internal static class ExcelTemplateExportLimit
{
    // Jede exportierte Zeile muss von Kennzahlen und bedingter Formatierung
    // erfasst werden. Eine hoehere reine Speichergrenze wuerde zwar eine Datei
    // erzeugen, aber ab Zeile 5001 falsche Summen anzeigen.
    internal const int MaxRecords = ExcelVorlagenLayout.MaximaleDatenzeilen;

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
