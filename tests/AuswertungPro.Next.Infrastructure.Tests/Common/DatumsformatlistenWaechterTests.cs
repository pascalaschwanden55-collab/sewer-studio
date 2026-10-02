using System.Text.RegularExpressions;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

/// <summary>
/// Sperrklinke zu Deepscan 02.10.2026, A4: Datum_Jahr wird nur noch ueber
/// <c>HaltungFeldwerte</c> gedeutet. Eigene Tag-Monat-Jahr-Formatlisten zum Parsen
/// (ParseExact mit "dd.MM.yyyy" o.ae.) gibt es nur noch in den unten genannten Dateien.
/// Die Liste darf nur schrumpfen; ein neuer Leser von Datum_Jahr nutzt die gemeinsame Regel.
/// </summary>
public sealed class DatumsformatlistenWaechterTests
{
    private static readonly Dictionary<string, string> Erlaubt = new(StringComparer.Ordinal)
    {
        ["src/AuswertungPro.Next.Application/Common/HaltungFeldwerte.cs"] = "die gemeinsame Leseregel",
        // Importparser: legen fest, in welcher Rohform ein Fremdformat gespeichert wird.
        ["src/AuswertungPro.Next.Infrastructure/Ai/Training/PdfReview/TrainingPdfProtocolMetadataParser.cs"] = "Importparser PDF-Protokoll",
        ["src/AuswertungPro.Next.Infrastructure/HoldingDistribution/BehaelterPruefungParser.cs"] = "Importparser PDF-Text",
        ["src/AuswertungPro.Next.Infrastructure/HoldingDistribution/HoldingVideoMatching.cs"] = "Datum im Videodateinamen",
        ["src/AuswertungPro.Next.Infrastructure/HoldingFolderDistributor.SidecarXtf.cs"] = "Importparser XTF-Begleitdatei",
        ["src/AuswertungPro.Next.Infrastructure/HoldingTextNormalizer.cs"] = "Importparser PDF-Text der Haltungsverteilung",
        ["src/AuswertungPro.Next.Infrastructure/Import/DichtheitImportDistributionService.cs"] = "Importparser Dichtheitsprotokoll",
        ["src/AuswertungPro.Next.Infrastructure/Import/Kins/KinsDbfWhitelistEnrichmentService.cs"] = "Importparser KINS",
        ["src/AuswertungPro.Next.Infrastructure/Import/Kins/KinsImportService.cs"] = "Importparser KINS",
        ["src/AuswertungPro.Next.Infrastructure/Import/Pdf/PdfPathMetadataExtractor.cs"] = "Importparser PDF-Pfad",
        ["src/AuswertungPro.Next.Infrastructure/Import/Pdf/SchachtProtocolParser.cs"] = "Importparser Schachtprotokoll",
        ["src/AuswertungPro.Next.Infrastructure/Import/WinCan/WinCanValueNormalizer.cs"] = "Importparser WinCan",
        ["src/AuswertungPro.Next.Infrastructure/Import/Xtf/M150ValueExtractor.cs"] = "Importparser M150",
        ["src/AuswertungPro.Next.Infrastructure/Import/Xtf/VsaKekUntersuchungsWahl.cs"] = "Importparser VSA-KEK",
        ["src/AuswertungPro.Next.Infrastructure/Import/Xtf/XtfValueNormalizer.cs"] = "Importparser XTF",
        // Keine Leser von Datum_Jahr: strenge Pruefung von WebGIS-/DSS-Datumsfeldern und eigene Dateiformate.
        ["src/AuswertungPro.Next.Application/UseCases/Objektakten/ObjektFeldPruefung.cs"] = "WebGIS-Datumsfeld, strenge Pruefung",
        ["src/AuswertungPro.Next.Application/Xtf/Dss/DssExportSchema.cs"] = "DSS-Datumsfeld, strenge Pruefung",
        ["src/AuswertungPro.Next.Infrastructure/Media/MediaConflictCenterService.cs"] = "eigene Konfliktdatei",
        ["src/AuswertungPro.Next.UI/ViewModels/Pages/MediaConflictsPageViewModel.cs"] = "eigene Konfliktdatei",
    };

    private static readonly Regex TagMonatJahrFormat = new(@"""d{1,2}[./-]M{1,2}[./-]y{2,4}", RegexOptions.CultureInvariant);

    [Fact]
    public void Eigene_Datumsformatlisten_gibt_es_nur_in_der_Leseregel_und_den_Importparsern()
    {
        var root = TestRepoPaths.RepoRoot();
        var gefunden = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p =>
            {
                var text = File.ReadAllText(p);
                return text.Contains("ParseExact", StringComparison.Ordinal) && TagMonatJahrFormat.IsMatch(text);
            })
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        var neu = gefunden.Except(Erlaubt.Keys).OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.True(neu.Count == 0,
            "Eigene Datumsformatliste ausserhalb der gemeinsamen Leseregel (HaltungFeldwerte): " + string.Join(", ", neu));

        var veraltet = Erlaubt.Keys.Except(gefunden).OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.True(veraltet.Count == 0,
            "Diese Dateien haben keine eigene Liste mehr; bitte aus der Sperrklinke streichen: " + string.Join(", ", veraltet));
    }
}
