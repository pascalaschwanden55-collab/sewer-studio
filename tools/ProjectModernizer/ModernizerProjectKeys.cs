internal static class ModernizerProjectKeys
{
    public const string InspectionDateYear = "Datum_Jahr";
    public const string SecondaryVideoLink = "Link_G";

    public const string ContractorLogoPath = "AuftragnehmerLogoPath";
    public const string CustomerLogoPath = "AuftraggeberLogoPath";
    public const string ImportSource = "ImportQuelle";
    public const string ImportSourceHistory = "ImportQuellenHistorie";
    public const string PdfStoredFiles = "PDF_StoredFiles";
    public const string TxtStoredFiles = "TXT_StoredFiles";
    public const string XtfStoredFiles = "XTF_StoredFiles";

    public const string LogosFolder = "Logos";
    public const string ModernizedImportSourceHistory = "Modernisiert: Rohdaten liegen projektintern unter Importdateien.";

    public static IReadOnlyList<string> LogoPathMetadataKeys { get; } = new[]
    {
        CustomerLogoPath,
        ContractorLogoPath
    };

    public static IReadOnlyList<string> StoredImportMetadataKeys { get; } = new[]
    {
        PdfStoredFiles,
        XtfStoredFiles,
        TxtStoredFiles
    };

    public static IReadOnlyList<string> SchachtNumberFields { get; } = new[]
    {
        "Schachtnummer",
        "Nr.",
        "NR."
    };
}
