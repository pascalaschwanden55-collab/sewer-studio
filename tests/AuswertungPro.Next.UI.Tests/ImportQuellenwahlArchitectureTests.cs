using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter fuer die WinCan-Quellenwahl und das Stopptor.
///
/// Zwei Regeln muessen dauerhaft halten:
///
/// 1. Der Import findet seine Quellen OHNE KI, Ollama oder Sidecar. Das Programm soll
///    auch auf einem Rechner mit kleiner Grafikkarte und bei ausgeschalteter KI
///    vollstaendig importieren.
/// 2. Das Stopptor liegt VOR der Veroeffentlichung — in beiden Importwegen. Die alte
///    ValidatePlausibility laeuft nach Publish und taugt dafuer nicht.
/// </summary>
public sealed class ImportQuellenwahlArchitectureTests
{
    private static readonly string[] KiBegriffe =
    [
        "Ollama", "Sidecar", "Qwen", "Gemma", "VisionPipeline", "IProtocolAiService",
        "KnowledgeBase", "Infrastructure.Ai", "Application.Ai"
    ];

    private static string Lies(params string[] teile)
    {
        var pfad = Path.Combine(FindRepositoryRoot(), Path.Combine(teile));
        Assert.True(File.Exists(pfad), $"Datei nicht gefunden: {pfad}");
        return File.ReadAllText(pfad);
    }

    public static TheoryData<string[]> KiFreieDateien =>
    [
        ["src", "AuswertungPro.Next.Application", "UseCases", "Import", "Quellen", "Quellenwahl.cs"],
        ["src", "AuswertungPro.Next.Application", "UseCases", "Import", "Quellen", "ImportPlausibilitaetsTor.cs"],
        // Ausdruecklich AUCH der konkrete Pruefer in Infrastructure: dort sitzt das
        // Risiko, nicht im reinen Application-Baustein.
        ["src", "AuswertungPro.Next.Infrastructure", "Import", "WinCan", "WinCanDb3Pruefer.cs"],
    ];

    [Theory]
    [MemberData(nameof(KiFreieDateien))]
    public void Quellensuche_kommt_ohne_KI_und_GPU_aus(string[] teile)
    {
        var quelltext = Lies(teile);

        foreach (var begriff in KiBegriffe)
        {
            Assert.False(
                quelltext.Contains(begriff, StringComparison.OrdinalIgnoreCase)
                && !IstNurImKommentar(quelltext, begriff),
                $"{Path.Combine(teile)} darf nicht von \"{begriff}\" abhaengen — "
                + "der Import muss ohne KI und ohne grosse Grafikkarte laufen.");
        }
    }

    [Fact]
    public void Erkennung_und_Import_benutzen_denselben_Pruefer()
    {
        // Genau hier lag der Andermatt-Fehler: zwei Kopien derselben Regel, eine davon
        // veraltet. Es darf keine zweite Auswahlregel mehr geben.
        var detektor = Lies("src", "AuswertungPro.Next.Infrastructure", "Import", "KanalExportDetector.cs");
        var importer = Lies("src", "AuswertungPro.Next.Infrastructure", "Import", "WinCan", "WinCanDbImportService.Sammelordner.cs");

        Assert.Contains("WinCanDb3Pruefer.Pruefe", detektor, StringComparison.Ordinal);
        Assert.Contains("WinCanDb3Pruefer.Pruefe", importer, StringComparison.Ordinal);

        // Keine Auswahl nach Dateigroesse mehr — das war die fehlerhafte Regel.
        Assert.DoesNotContain("OrderByDescending(p => { try { return new FileInfo(p).Length", detektor, StringComparison.Ordinal);
        Assert.DoesNotContain("candidate.Length", importer, StringComparison.Ordinal);
    }

    [Fact]
    public void Stopptor_liegt_vor_Publish_im_manuellen_Import()
    {
        var quelltext = Lies("src", "AuswertungPro.Next.UI", "Services", "ImportRunWorkflowController.cs");

        var tor = quelltext.IndexOf("DarfUebernehmen(request", StringComparison.Ordinal);
        var publish = quelltext.IndexOf("fileTransaction.Publish()", StringComparison.Ordinal);

        Assert.True(tor > 0, "Aufruf des Stopptors nicht gefunden.");
        Assert.True(publish > 0, "Publish nicht gefunden.");
        Assert.True(tor < publish, "Das Stopptor muss VOR fileTransaction.Publish() stehen.");
    }

    [Fact]
    public void Stopptor_liegt_vor_Publish_im_EinKnopfImport()
    {
        var quelltext = Lies("src", "AuswertungPro.Next.UI", "Services", "ImportOneClickProjectController.cs");

        var tor = quelltext.IndexOf("DarfUebernehmen(urteil", StringComparison.Ordinal);
        var publish = quelltext.IndexOf("fileTransaction.Publish()", StringComparison.Ordinal);

        Assert.True(tor > 0, "Aufruf des Stopptors nicht gefunden.");
        Assert.True(publish > 0, "Publish nicht gefunden.");
        Assert.True(tor < publish, "Das Stopptor muss VOR fileTransaction.Publish() stehen.");
    }

    [Fact]
    public void Rueckfrage_ist_produktiv_verdrahtet()
    {
        // Ohne diese Verdrahtung waere ConfirmImplausible null und jede Mengenabweichung
        // wuerde fail-closed abbrechen, ohne dass der Benutzer je gefragt wird.
        var viewModel = Lies("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ImportPageViewModel.cs");

        Assert.Contains("ConfirmImplausible: BestaetigeUnstimmigesErgebnis", viewModel, StringComparison.Ordinal);
        Assert.Contains("defaultNo: true", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Abbruchtext_behauptet_nicht_dass_nichts_veraendert_wurde()
    {
        // Vor dem Tor koennen Wiederherstellungspunkt, Arbeitsdateien und Bericht
        // bereits entstanden sein. "Nichts veraendert" waere gelogen.
        var tor = Lies("src", "AuswertungPro.Next.Application", "UseCases", "Import", "Quellen", "ImportPlausibilitaetsTor.cs");

        Assert.Contains(
            "Keine Projektdaten und keine Importdateien uebernommen.",
            tor,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static bool IstNurImKommentar(string quelltext, string begriff)
        => quelltext
            .Split('\n')
            .Where(z => z.Contains(begriff, StringComparison.OrdinalIgnoreCase))
            .All(z =>
            {
                var t = z.TrimStart();
                return t.StartsWith("//", StringComparison.Ordinal)
                       || t.StartsWith("///", StringComparison.Ordinal)
                       || t.StartsWith("*", StringComparison.Ordinal);
            });
}
