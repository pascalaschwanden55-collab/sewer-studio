namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// Gemeinsame Text-/Format-Muster fuer die PDF-/Protokoll-Verarbeitung, damit dieselbe
/// Mustererkennung nicht in mehreren Bereichen (Import, Verteilung, ...) getrennt gepflegt werden
/// muss. Bewusst klein gehalten: hier landen nur Bausteine, die nachweislich IDENTISCH genutzt
/// werden - kein Sammelbecken fuer bereichsspezifische, fein getunte Muster.
/// </summary>
public static class SewerTextPatterns
{
    /// <summary>
    /// Schweizer/deutsches Datum DD.MM.JJ(JJ) mit Trenner Punkt/Slash/Minus
    /// (z.B. "09.06.2017", "9-6-17"). Identisch genutzt von Import (LegacyPdfImportService) und
    /// Verteilung (HoldingFolderDistributor: LabeledDateRx, GenericDateRx, FormEntryDateRx).
    /// </summary>
    public const string GermanDateCore = @"\d{2}[./-]\d{2}[./-]\d{2,4}";
}
