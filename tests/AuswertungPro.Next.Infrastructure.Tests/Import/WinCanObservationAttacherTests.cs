using System.Xml.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class WinCanObservationAttacherTests
{
    [Fact]
    public void AttachFromRows_OrdnetSortiertZuUndErstelltUnabhaengigeArbeitskopie()
    {
        var rows = new List<Dictionary<string, string>>
        {
            Row("S_T", ("S_ID", "S1"), ("S_StartNode", "100"), ("S_EndNode", "200"), ("S_SectionFlow", "D")),
            Row("SI_T", ("SI_ID", "I1"), ("SI_Section_ID", "S1"), ("SI_InspectionDir", "U")),
            Row("SO_T", ("SO_Inspection_ID", "I1"), ("SO_OpCode", "BBB"), ("SO_Remark", "zweiter"), ("SO_Distance", "2,75"), ("SO_Counter", "20")),
            Row("SO_T", ("SO_Inspection_ID", "I1"), ("SO_OpCode", "AAA"), ("SO_Remark", "erster"), ("SO_Distance", "1.5"), ("SO_Counter", "10")),
            Row("SO_T", ("SO_Inspection_ID", "fehlt"), ("SO_OpCode", "CCC")),
            Row("SO_T", ("SO_Inspection_ID", "fehlt"), ("SO_OpCode", ""), ("SO_Remark", ""))
        };
        var record = Record("200-100");
        var warnings = new List<string>();

        WinCanObservationAttacher.AttachFromRows(rows, [record], warnings);

        Assert.NotNull(record.Protocol);
        Assert.Equal("200-100", record.Protocol.HaltungId);
        Assert.Equal("Import (WinCan Viewer MDB)", record.Protocol.Original.Comment);
        Assert.Equal("Arbeitskopie", record.Protocol.Current.Comment);
        Assert.Collection(
            record.Protocol.Original.Entries,
            entry => AssertEntry(entry, "AAA", "erster", 1.5),
            entry => AssertEntry(entry, "BBB", "zweiter", 2.75));
        Assert.Collection(
            record.Protocol.Current.Entries,
            entry => AssertEntry(entry, "AAA", "erster", 1.5),
            entry => AssertEntry(entry, "BBB", "zweiter", 2.75));
        Assert.NotSame(record.Protocol.Original.Entries[0], record.Protocol.Current.Entries[0]);
        Assert.Contains("SO_T: 1 Beobachtungen ohne Inspektions-Zuordnung uebersprungen.", warnings);
        Assert.Contains("WinCan Viewer: 1 Haltungen mit Protokolleintraegen aus SO_T.", warnings);
    }

    [Fact]
    public void AttachFromXml_NutztLetztenDoppeltenAbschnittUndBehaltetXmlMeldungen()
    {
        var doc = XDocument.Parse("""
            <NewDataSet>
              <S_T><S_ID>S1</S_ID><S_StartNode>falsch</S_StartNode><S_EndNode>falsch</S_EndNode></S_T>
              <S_T><S_ID>S1</S_ID><S_StartNode>100</S_StartNode><S_EndNode>200</S_EndNode><S_SectionFlow>D</S_SectionFlow></S_T>
              <SI_T><SI_ID>I1</SI_ID><SI_Section_ID>S1</SI_Section_ID></SI_T>
              <SO_T><SO_Inspection_ID>I1</SO_Inspection_ID><SO_OpCode>BAB</SO_OpCode><SO_Remark>Riss</SO_Remark><SO_Distance>3,25</SO_Distance><SO_Counter>1</SO_Counter></SO_T>
            </NewDataSet>
            """);
        var record = Record("100-200");
        var warnings = new List<string>();

        WinCanObservationAttacher.AttachFromXml(doc, [record], warnings);

        var entry = Assert.Single(record.Protocol!.Original.Entries);
        AssertEntry(entry, "BAB", "Riss", 3.25);
        Assert.Equal("Import (WinCan Viewer XML)", record.Protocol.Original.Comment);
        Assert.Contains("WinCan Viewer XML: 1 Haltungen mit Protokolleintraegen aus SO_T.", warnings);
    }

    [Fact]
    public void AttachFromXml_OhneRootOderPassendeBeobachtung_VeraendertRecordNicht()
    {
        var record = Record("10-20");
        var warnings = new List<string>();

        WinCanObservationAttacher.AttachFromXml(new XDocument(), [record], warnings);

        Assert.Null(record.Protocol);
        Assert.Empty(warnings);
    }

    private static HaltungRecord Record(string holding)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
        return record;
    }

    private static Dictionary<string, string> Row(
        string table,
        params (string Key, string Value)[] values)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["__table"] = table
        };
        foreach (var (key, value) in values)
            row[key] = value;
        return row;
    }

    private static void AssertEntry(ProtocolEntry entry, string code, string description, double meter)
    {
        Assert.Equal(code, entry.Code);
        Assert.Equal(description, entry.Beschreibung);
        Assert.Equal(meter, entry.MeterStart);
        Assert.Equal(ProtocolEntrySource.Imported, entry.Source);
    }
}
