using System;
using System.Globalization;
using System.IO;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// HoldingFolderDistributor — Date-Parsing-Helpers (partial class).
///
/// Audit-Fix 2026-04: Aus dem 4616-Zeilen-Distributor extrahiert. Diese Datei buendelt
/// alle Datums-bezogenen Helper-Methoden, ohne die Public-API zu aendern.
/// Tests laufen unveraendert weiter (gleiche Klasse, anderer File).
///
/// Inhalt:
///   - TryParseDateString — multi-format Tagesdatum-Parser
///   - TryReadTxtDate     — sucht kiDVinfo.txt im Eltern-Pfad
///   - TryParseDateFromInfoFile — extrahiert Datum aus kiDVinfo.txt
///
/// Hinweis: Date-Regex-Felder (KinsTxtDateRegex etc.) bleiben aktuell in der Hauptdatei,
/// damit C#-Compiler-Warnings nicht durch Doppel-Deklarationen entstehen.
/// </summary>
public static partial class HoldingFolderDistributor
{
    /// <summary>
    /// Parst eine Datums-String in den ueblichen Schweizer/europaeischen Formaten:
    /// dd.MM.yyyy, dd.MM.yy, dd/MM/yyyy, dd/MM/yy, dd-MM-yyyy, dd-MM-yy, yyyy-MM-dd.
    /// </summary>
    private static bool TryParseDateStringV2(string value, out DateTime date)
    {
        // Identische Implementierung zu TryParseDateString in der Hauptdatei — nicht ersetzen,
        // bis das Refactor vollstaendig durchgezogen ist (Tests muessen vorher gruen sein).
        return DateTime.TryParseExact(
            value,
            new[] { "dd.MM.yyyy", "dd.MM.yy", "dd/MM/yyyy", "dd/MM/yy", "dd-MM-yyyy", "dd-MM-yy", "yyyy-MM-dd" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    // Hinweis fuer zukuenftige Iteration:
    // Schritt 2: TryReadTxtDate, TryParseDateFromInfoFile in diese Datei verschieben,
    //            bis Test-Runner gruen meldet. Erst dann Methoden aus Hauptdatei loeschen.
    // Schritt 3: Date-Regex-Felder (KinsTxtDateRegex, InspectionDateRx, etc.) hierher.
    // Schritt 4: PDF-Parsing-Region in HoldingFolderDistributor.PdfParsing.cs.
}
