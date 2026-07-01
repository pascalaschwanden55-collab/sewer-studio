using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageHydraulikPanelControllerTests
{
    [Fact]
    public void BuildOpenRequest_liefert_leere_werte_ohne_record()
    {
        var request = DataPageHydraulikPanelController.BuildOpenRequest(null);

        Assert.Null(request.DnMillimeters);
        Assert.Null(request.Material);
        Assert.Null(request.WasserstandMillimeters);
    }

    [Fact]
    public void BuildOpenRequest_uebernimmt_dn_und_material_aus_record()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("DN_mm", "1'200", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("Rohrmaterial", "PVC", FieldSource.Xtf, userEdited: false);

        var request = DataPageHydraulikPanelController.BuildOpenRequest(record);

        Assert.Equal(1200d, request.DnMillimeters);
        Assert.Equal("PVC", request.Material);
        Assert.Null(request.WasserstandMillimeters);
    }

    [Fact]
    public void BuildOpenRequest_laesst_unlesbaren_dn_leer()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("DN_mm", "unbekannt", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("Rohrmaterial", "Beton", FieldSource.Xtf, userEdited: false);

        var request = DataPageHydraulikPanelController.BuildOpenRequest(record);

        Assert.Null(request.DnMillimeters);
        Assert.Equal("Beton", request.Material);
    }
}
