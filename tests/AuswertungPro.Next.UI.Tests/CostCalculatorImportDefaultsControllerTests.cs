using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorImportDefaultsControllerTests
{
    [Fact]
    public void InitializeFromHaltungRecord_speichert_defaults_und_wendet_sie_auf_bestehende_und_neue_massnahmen_an()
    {
        var controller = new CostCalculatorImportDefaultsController();
        var existing = Block("A");
        var record = new HaltungRecord();
        record.SetFieldValue("DN_mm", "300", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", "45.3", FieldSource.Xtf, userEdited: false);

        controller.InitializeFromHaltungRecord(record, new[] { existing });

        Assert.Equal("300", existing.DnText);
        Assert.Equal("45.30", existing.LengthText);
        Assert.Equal("0", existing.ConnectionsText);

        var later = Block("B");
        controller.ApplyTo(later);

        Assert.Equal("300", later.DnText);
        Assert.Equal("45.30", later.LengthText);
        Assert.Equal("0", later.ConnectionsText);
    }

    [Fact]
    public void ApplyTo_ueberschreibt_keine_manuell_gesetzten_massnahmenwerte()
    {
        var controller = new CostCalculatorImportDefaultsController();
        var record = new HaltungRecord();
        record.SetFieldValue("DN_mm", "300", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", "45.3", FieldSource.Xtf, userEdited: false);
        controller.InitializeFromHaltungRecord(record, Array.Empty<MeasureBlockVm>());
        var block = Block("A");
        block.DnText = "250";
        block.LengthText = "12.00";
        block.ConnectionsText = "2";

        controller.ApplyTo(block);

        Assert.Equal("250", block.DnText);
        Assert.Equal("12.00", block.LengthText);
        Assert.Equal("2", block.ConnectionsText);
    }

    private static MeasureBlockVm Block(string id)
        => new(
            new MeasureTemplate
            {
                Id = id,
                Name = id
            },
            new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase));
}
