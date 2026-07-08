using AuswertungPro.Next.Application.Schacht;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Mapper schreibt die einfache Schacht-Empfehlung in die Excel-Felder
/// "Massnahmen" (Text) und "Kosten" (Nettosumme) des SchachtRecord.
/// </summary>
public sealed class SchachtEmpfehlungRecordMapperTests
{
    private static HoldingCost Cost()
    {
        var measure = new MeasureCost();
        measure.Lines.Add(new CostLine { Text = "Rahmen/Deckel ersetzen", Qty = 1m, UnitPrice = 350m, Selected = true });
        measure.Lines.Add(new CostLine { Text = "Fugen sanieren", Qty = 1m, UnitPrice = 480m, Selected = true });
        return new HoldingCost { Holding = "KS 1", Measures = { measure } };
    }

    [Fact]
    public void ApplyTo_schreibt_Massnahmen_und_Kosten_Felder()
    {
        var record = new SchachtRecord();

        SchachtEmpfehlungRecordMapper.ApplyTo(record, Cost());

        Assert.Equal("Rahmen/Deckel ersetzen; Fugen sanieren", record.GetFieldValue("Massnahmen"));
        Assert.Equal("830.00", record.GetFieldValue("Kosten"));
    }

    [Fact]
    public void Clear_leert_beide_Felder()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Massnahmen", "Alt");
        record.SetFieldValue("Kosten", "999.00");

        SchachtEmpfehlungRecordMapper.Clear(record);

        Assert.Equal("", record.GetFieldValue("Massnahmen"));
        Assert.Equal("", record.GetFieldValue("Kosten"));
    }
}
