using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Infrastructure.Dossiers;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Eine eigene Fassung eines Vorlagentextes darf eine Verzeichniszeile nie
/// anfassen.
///
/// Genau das ist passiert: Als das Inhaltsverzeichnis noch als fester Text
/// angeboten wurde, war der Schluessel die GANZE Zeile — „1.Übersichtsplan
/// Werkleitungen3", samt Nummer und Seitenzahl. Wer die Seitenzahl aus dem
/// Text loeschte, bekam eine Ersetzung, die bis heute wirkt: Sie schreibt den
/// ganzen Text in den ersten Lauf, leert die uebrigen — und nimmt damit die
/// Seitenzahl UND die Tabulatorstruktur mit. Die Zeile stand danach ohne
/// Seitenzahl und ohne Einzug da, waehrend ihre Nachbarn richtig aussahen.
///
/// Die Verzeichniszeilen gehoeren dem Verzeichnis-Editor. Der Textersetzer
/// laesst sie deshalb aus — unabhaengig davon, was in alten Dossiers noch
/// gespeichert ist.
/// </summary>
public sealed class DossierTocLiteralGuardTests : IDisposable
{
    private readonly string _ordner = Path.Combine(
        Path.GetTempPath(), "toc_literal_" + Guid.NewGuid().ToString("N"));

    private readonly string _datei;

    public DossierTocLiteralGuardTests()
    {
        Directory.CreateDirectory(_ordner);
        _datei = Path.Combine(_ordner, "Eigentuemerdossier.docx");

        var wurzel = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);
        Assert.NotNull(wurzel);

        File.Copy(
            Path.Combine(wurzel!, "Export_Vorlage", DossierWordTemplate.TemplateFileName),
            _datei);
    }

    public void Dispose()
    {
        try { Directory.Delete(_ordner, recursive: true); } catch { }
    }

    private static Paragraph Verzeichniszeile(WordprocessingDocument document, string teil)
        => document.MainDocumentPart!.Document!.Body!
            .Descendants<Paragraph>()
            .First(p => p.InnerText.Contains(teil, StringComparison.Ordinal)
                && (p.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "")
                    .StartsWith("Verzeichnis", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Ein_alter_Schluessel_mit_ganzer_Zeile_bleibt_wirkungslos()
    {
        using var document = WordprocessingDocument.Open(_datei, true);

        // Genau der Stand aus Pascals dossiers.json.
        DocxLiteralTextReplacer.Apply(document, new Dictionary<string, string>
        {
            ["1.Übersichtsplan Werkleitungen3"] = "1.Übersichtsplan Werkleitungen"
        });

        var zeile = Verzeichniszeile(document, "Übersichtsplan Werkleitungen");

        // Seitenzahl und Tabulatoren stehen noch.
        Assert.Contains("PAGEREF", string.Concat(
            zeile.Descendants<FieldCode>().Select(code => code.Text)),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, zeile.Descendants<TabChar>().Count());
        Assert.EndsWith("3", zeile.InnerText.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void Auch_der_reine_Titel_geht_nicht_ueber_den_Textersetzer()
    {
        // Fuer den Titel ist der Verzeichnis-Editor zustaendig; ginge auch der
        // Textersetzer darueber, waere die Seitenzahl erneut in Gefahr.
        using var document = WordprocessingDocument.Open(_datei, true);

        DocxLiteralTextReplacer.Apply(document, new Dictionary<string, string>
        {
            ["Eigentumsverhältnisse"] = "Wem gehört was"
        });

        var zeile = Verzeichniszeile(document, "Eigentumsverhältnisse");

        Assert.Contains("Eigentumsverhältnisse", zeile.InnerText, StringComparison.Ordinal);
        Assert.Equal(2, zeile.Descendants<TabChar>().Count());
    }

    [Fact]
    public void Ausserhalb_des_Verzeichnisses_wirkt_der_Ersetzer_weiter()
    {
        // Die Kapitelueberschrift im Text ist KEINE Verzeichniszeile und muss
        // weiterhin aenderbar bleiben.
        using var document = WordprocessingDocument.Open(_datei, true);

        var geaendert = DocxLiteralTextReplacer.Apply(document, new Dictionary<string, string>
        {
            ["Eigentumsverhältnisse"] = "Wem gehört was"
        });

        Assert.True(geaendert > 0, "Die Ueberschrift im Text wurde nicht geaendert.");

        var ueberschrift = document.MainDocumentPart!.Document!.Body!
            .Descendants<Paragraph>()
            .First(p => (p.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "")
                    .StartsWith("berschrift", StringComparison.OrdinalIgnoreCase)
                && p.InnerText.Contains("Wem gehört was", StringComparison.Ordinal));

        Assert.Equal("Wem gehört was", ueberschrift.InnerText);
    }
}
