using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchachtDamageLineBuilderTests
{
    [Fact]
    public void Build_UsesPrimaereSchaedenAlias_AndSplitsLines()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Prim\u00e4re Sch\u00e4den", "Bankett: ausgebrochen\nSchachtrohr: korrodiert");

        var lines = SchachtDamageLineBuilder.Build(record);

        Assert.Equal(2, lines.Count);
        Assert.Equal("Bankett", lines[0].Component);
        Assert.Equal("ausgebrochen", lines[0].Text);
        Assert.Equal("Schachtrohr", lines[1].Component);
        Assert.Equal("korrodiert", lines[1].Text);
    }

    /// <summary>
    /// Fix-Runde 1 (Review Aufgabe 10c1): "Primaere Schaeden" (ASCII, mit Leerzeichen)
    /// ist die Feld-Alias-Schreibweise, die <c>SchachtProtocolApplier</c> und
    /// <c>SchachtProFieldNames.PrimaereSchaedenAscii</c> tatsaechlich schreiben —
    /// nicht zu verwechseln mit der umlautrichtigen Vorlagenschreibweise
    /// "Primäre Schäden" (bereits oben getestet) oder dem technischen
    /// Unterstrich-Schluessel "Primaere_Schaeden".
    /// </summary>
    [Fact]
    public void Build_UsesSchachtProAsciiAlias_WithSpaceNoUmlaut()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Primaere Schaeden", "Bankett: ausgebrochen");

        var lines = SchachtDamageLineBuilder.Build(record);

        var line = Assert.Single(lines);
        Assert.Equal("Bankett", line.Component);
        Assert.Equal("ausgebrochen", line.Text);
    }

    [Fact]
    public void Build_UsesTechnicalPrimaereSchaedenField_WhenTemplateAliasIsEmpty()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Primaere_Schaeden", "\u00fcberdeckt, 2 Einl\u00e4ufe");

        var lines = SchachtDamageLineBuilder.Build(record);

        var line = Assert.Single(lines);
        Assert.Equal("", line.Component);
        Assert.Equal("\u00fcberdeckt, 2 Einl\u00e4ufe", line.Text);
    }

    [Fact]
    public void Build_TreatsBemerkungenAsDamageLines()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Bemerkungen", "Ablagerung\nkorrodiert");

        var lines = SchachtDamageLineBuilder.Build(record);

        Assert.Equal(2, lines.Count);
        Assert.Equal("Ablagerung", lines[0].Text);
        Assert.Equal("korrodiert", lines[1].Text);
    }
}
