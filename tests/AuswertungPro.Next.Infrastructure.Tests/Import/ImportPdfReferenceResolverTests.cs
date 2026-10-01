using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Protocols;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Zuordnung eines PDF-Dateinamens zu einer Haltung oder einem Schacht.
/// Grundregel: Es zaehlt nur, was im Projekt wirklich existiert — der Resolver
/// erfindet nie eine Haltung oder einen Schacht aus einem Dateinamen.
/// </summary>
public class ImportPdfReferenceResolverTests
{
    private static readonly string[] Haltungen =
    {
        "892037-74091", "80823-80872", "07.292862-892037"
    };

    private static readonly string[] Schaechte =
    {
        "74091", "892037", "80823", "80872"
    };

    private static ImportPdfReference? Loese(string dateiname)
        => new ImportPdfReferenceResolver().Resolve(dateiname, Haltungen, Schaechte);

    [Fact]
    public void WinCanSectionName_WirdDerHaltungZugeordnet()
    {
        var treffer = Loese("Section_8_892037-74091.pdf");

        Assert.NotNull(treffer);
        Assert.Equal(ImportPdfReferenceKind.Haltung, treffer!.Value.Kind);
        Assert.Equal("892037-74091", treffer.Value.Name);
    }

    [Fact]
    public void HaltungGewinntGegenDarinEnthalteneSchachtnummer()
    {
        // "892037" ist auch eine Schachtnummer und steckt in der Haltung.
        // Der laengere, spezifischere Treffer muss gewinnen.
        var treffer = Loese("Section_16_07.292862-892037.pdf");

        Assert.NotNull(treffer);
        Assert.Equal(ImportPdfReferenceKind.Haltung, treffer!.Value.Kind);
        Assert.Equal("07.292862-892037", treffer.Value.Name);
    }

    [Fact]
    public void ReinerSchachtname_WirdDemSchachtZugeordnet()
    {
        var treffer = Loese("74091.pdf");

        Assert.NotNull(treffer);
        Assert.Equal(ImportPdfReferenceKind.Schacht, treffer!.Value.Kind);
        Assert.Equal("74091", treffer.Value.Name);
    }

    [Fact]
    public void UnbekannteNummer_WirdNichtZugeordnet()
    {
        Assert.Null(Loese("Section_99_111111-222222.pdf"));
    }

    [Fact]
    public void DateinameOhneNummer_WirdNichtZugeordnet()
    {
        Assert.Null(Loese("WinCanScanExplorer_de.pdf"));
    }

    [Fact]
    public void ZweiVerschiedeneHaltungenImNamen_BleibenUnzugeordnet()
    {
        // Sammel-PDF: darf NICHT willkuerlich einer der beiden Haltungen zufallen.
        Assert.Null(Loese("Bericht_892037-74091_und_80823-80872.pdf"));
    }

    [Fact]
    public void SchachtnummerAlsTeilEinerLaengerenZahl_ZaehltNicht()
    {
        // "74091" steckt in "174091" - das ist kein Treffer.
        Assert.Null(Loese("Bericht_174091.pdf"));
    }

    [Fact]
    public void VertauschteSchachtreihenfolge_FindetDieBekannteHaltung()
    {
        var treffer = Loese("Section_8_74091-892037.pdf");

        Assert.NotNull(treffer);
        Assert.Equal(ImportPdfReferenceKind.Haltung, treffer!.Value.Kind);
        Assert.Equal("892037-74091", treffer.Value.Name);
    }
}
