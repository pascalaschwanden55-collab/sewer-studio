using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechteColumnPolicyTests
{
    [Theory]
    [InlineData("Ausgeführt durch", "Ausgefuehrt_durch")]
    [InlineData("Eigentümer", "Eigentuemer")]
    [InlineData("EigentÃ¼mer", "Eigentuemer")]
    [InlineData("Referenzprüfung", "Referenzpruefung")]
    [InlineData("Ja / Nein", "Sanieren_JaNein")]
    [InlineData("Sanierung ja", "Sanieren_JaNein")]
    [InlineData("Dichtheitsprüfung", "Pruefungsresultat")]
    [InlineData("Bemerkung", null)]
    public void ResolveOptionField_BehaeltBestehendeSpaltenzuordnung(string column, string? expected)
        => Assert.Equal(expected, SchaechteColumnPolicy.ResolveOptionField(column));

    [Fact]
    public void DisplayUndSpaltentypen_BehaltenBestehendeSonderregeln()
    {
        Assert.Equal("Sanieren Ja/Nein", SchaechteColumnPolicy.GetDisplayHeader("Ja_Nein"));
        Assert.True(SchaechteColumnPolicy.IsCostColumn("Sanierungskosten CHF"));
        Assert.True(SchaechteColumnPolicy.IsZustandsklasseColumn("Zustandsklasse Schacht"));
        Assert.True(SchaechteColumnPolicy.IsPrimaryDamagesColumn("Primäre Schäden"));
        Assert.True(SchaechteColumnPolicy.IsDetailsNameColumn("Schachtnummer"));
    }

    [Fact]
    public void GetSchachtNumber_NutztDieBisherigePrioritaet()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("NR.", "  dritt  ");
        record.SetFieldValue("Nr.", "  zweit  ");
        record.SetFieldValue("Schachtnummer", "  zuerst  ");

        Assert.Equal("zuerst", SchaechteColumnPolicy.GetSchachtNumber(record));

        record.SetFieldValue("Schachtnummer", "");
        Assert.Equal("zweit", SchaechteColumnPolicy.GetSchachtNumber(record));

        record.SetFieldValue("Nr.", "");
        Assert.Equal("dritt", SchaechteColumnPolicy.GetSchachtNumber(record));

        record.SetFieldValue("NR.", "");
        Assert.Equal(string.Empty, SchaechteColumnPolicy.GetSchachtNumber(record));
    }

    [Theory]
    [InlineData("Sanierungskosten", "Sanierung und Kosten")]
    [InlineData("PDF Link", "Dokumente und Medien")]
    [InlineData("Zustandsklasse", "Zustand und Inspektion")]
    [InlineData("Schachtmaterial", "Stammdaten")]
    [InlineData("Freie Notiz", "Weitere Angaben")]
    public void ResolveSchachtDetailGroup_BehaeltGruppierung(string column, string expected)
        => Assert.Equal(expected, SchaechteColumnPolicy.ResolveSchachtDetailGroup(column));

    [Theory]
    [InlineData("Kosten PDF", "Sanierung und Kosten")]
    [InlineData("PDF Schaden", "Dokumente und Medien")]
    [InlineData("Schaden Schacht", "Zustand und Inspektion")]
    public void ResolveSchachtDetailGroup_BehaeltVorrangBeiMehrdeutigenNamen(
        string column,
        string expected)
        => Assert.Equal(expected, SchaechteColumnPolicy.ResolveSchachtDetailGroup(column));

    [Fact]
    public void DropdownSpec_WirdWeiterUeberZentralePolicyAufgeloest()
    {
        Assert.True(SchaechteColumnPolicy.TryResolveDropdownColumnSpec("Eigentümer", out var spec));
        Assert.Equal("EigentuemerOptions", spec.ItemsSourcePath);
    }
}
