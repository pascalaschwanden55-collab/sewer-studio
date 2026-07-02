using System.IO;
using AuswertungPro.Next.UI.ViewModels.Pages;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BuilderPagePdfBlockBuilderTests
{
    [Fact]
    public void BuildProjectCustomerBlock_verbindet_definierte_firmenzeilen_in_fester_reihenfolge()
    {
        var metadata = new Dictionary<string, string>
        {
            ["FirmaEmail"] = " info@example.ch ",
            ["Auftraggeber"] = " Gemeinde ",
            ["FirmaTelefon"] = " ",
            ["FirmaName"] = " Kanal AG "
        };

        var block = BuilderPagePdfBlockBuilder.BuildProjectCustomerBlock(metadata);

        Assert.Equal("Gemeinde\nKanal AG\ninfo@example.ch", block);
    }

    [Fact]
    public void BuildProjectCustomerBlock_liefert_nicht_definiert_ohne_textwerte()
    {
        var metadata = new Dictionary<string, string>
        {
            ["Auftraggeber"] = " ",
            ["FirmaName"] = ""
        };

        var block = BuilderPagePdfBlockBuilder.BuildProjectCustomerBlock(metadata);

        Assert.Equal("Nicht definiert", block);
    }

    [Fact]
    public void BuildObjectBlock_nimmt_definierte_metadaten_und_haltungsanzahl()
    {
        var metadata = new Dictionary<string, string>
        {
            ["Zone"] = " Projekt A ",
            ["Gemeinde"] = " Uri ",
            ["AuftragNr"] = " 42 ",
            ["Bearbeiter"] = " ",
            ["InspektionsDatum"] = "26.06.2026"
        };

        var block = BuilderPagePdfBlockBuilder.BuildObjectBlock(metadata, holdingCount: 7);

        Assert.Equal(
            "Projekt: Projekt A\nGemeinde: Uri\nAuftrag-Nr.: 42\nInspektionsdatum: 26.06.2026\nHaltungen im Ausdruck: 7",
            block);
    }

    [Fact]
    public void BuilderPageViewModel_enthaelt_keine_pdf_block_builder_mehr()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "BuilderPageViewModel.cs"));

        Assert.DoesNotContain("private static string BuildProjectCustomerBlock", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string BuildObjectBlock", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void AddLine(StringBuilder", source, StringComparison.Ordinal);
    }

}
