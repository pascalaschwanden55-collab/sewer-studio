using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Die Einstellung "Fotos pro Seite" erreicht den PDF-Erzeuger einmal beim Aufbau im
/// <c>ServiceProvider</c>. Wer stattdessen den statischen Kompatibilitaetsweg
/// <c>ProtocolRegenerationService</c> oder ein eigenes <c>new ProtocolPdfExporter()</c>
/// verwendet, baut sich einen zweiten Erzeuger ohne Einstellung - der Regler bliebe dort
/// still wirkungslos.
/// </summary>
public sealed class ProtocolPdfLayoutSettingsReachTests
{
    [Fact]
    public void Kein_UI_Code_verwendet_den_statischen_Regenerierungsweg()
    {
        var treffer = FindeInUiQuelltext("ProtocolRegenerationService.");

        Assert.True(
            treffer.Count == 0,
            "Statischer Regenerierungsweg umgeht die Einstellung 'Fotos pro Seite': "
            + string.Join(", ", treffer));
    }

    [Fact]
    public void Kein_UI_Code_baut_einen_eigenen_PDF_Erzeuger()
    {
        var treffer = FindeInUiQuelltext("new ProtocolPdfExporter(")
            .Where(pfad => !pfad.EndsWith("ServiceProvider.cs", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            treffer.Count == 0,
            "Eigener PDF-Erzeuger umgeht die Einstellung 'Fotos pro Seite': "
            + string.Join(", ", treffer));
    }

    [Fact]
    public void Nur_der_ServiceProvider_baut_den_AppSettings_Adapter()
    {
        var treffer = FindeInUiQuelltext("new AppSettingsProtocolPdfLayoutSettings(");

        Assert.Equal(["ServiceProvider.cs"], treffer);
    }

    [Fact]
    public void Der_Dossier_Dialog_verwendet_die_injizierte_Einstellung()
    {
        var controller = LiesUiDatei("DataPage", "DataPagePrintController.cs");
        var dialog = LiesUiDatei("Views", "Windows", "DossierPrintDialog.xaml.cs");

        Assert.Contains("new DossierPrintDialog(_protocolPdfLayoutSettings)", controller, StringComparison.Ordinal);
        Assert.Contains("DossierPrintDialog(IProtocolPdfLayoutSettings", dialog, StringComparison.Ordinal);
        Assert.Contains("PhotosPerPage = _protocolPdfLayoutSettings.PhotosPerPage", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("AppSettings.Load()", dialog, StringComparison.Ordinal);
    }

    private static List<string> FindeInUiQuelltext(string suchtext)
    {
        var uiRoot = Path.Combine(FindRepositoryRoot(), "src", "AuswertungPro.Next.UI");

        return Directory
            .EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(pfad => !pfad.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(pfad => !pfad.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(pfad => File.ReadAllText(pfad).Contains(suchtext, StringComparison.Ordinal))
            .Select(pfad => Path.GetRelativePath(uiRoot, pfad))
            .OrderBy(pfad => pfad, StringComparer.Ordinal)
            .ToList();
    }

    private static string LiesUiDatei(params string[] teile)
        => File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "AuswertungPro.Next.UI",
            Path.Combine(teile)));
}
