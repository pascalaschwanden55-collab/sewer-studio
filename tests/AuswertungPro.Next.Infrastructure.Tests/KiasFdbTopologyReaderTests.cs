using System.Data;
using System.Reflection;
using AuswertungPro.Next.Infrastructure.Import.Ibak;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class KiasFdbTopologyReaderTests
{
    [Fact]
    public void ReadStammdatenRows_MixedValueTypes_SkipsOnlyBadRows()
    {
        var table = new DataTable();
        table.Columns.Add("OBJ_NAME", typeof(object));
        table.Columns.Add("DISCRIM", typeof(object));
        table.Columns.Add("OBJ_LENGTH", typeof(object));
        table.Columns.Add("PROFILE_HEIGHT", typeof(object));
        table.Columns.Add("PROFILE_WIDTH", typeof(object));
        table.Columns.Add("STR3", typeof(object));
        table.Columns.Add("STR5", typeof(object));

        table.Rows.Add("06-001", "Lt", "12,5", "300", 400.0d, "Hauptstrasse", DBNull.Value);
        table.Rows.Add("06-002", "Sc", 8.75d, DBNull.Value, "250,0", "", "Wassen");
        table.Rows.Add("06-003", "Lt", "nicht-zahl", 200, 200, "Fehlerstrasse", "Ort");

        using var reader = table.CreateDataReader();
        var messages = new List<string>();

        var method = typeof(KiasFdbTopologyReader).GetMethod(
            "ReadStammdatenRows",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var result = Assert.IsType<Dictionary<string, KiasFdbTopologyReader.StammdatenEntry>>(
            method.Invoke(null, [reader, messages]));

        Assert.Equal(2, result.Count);

        var first = result["06-001"];
        Assert.Equal("Lt", first.Discrim);
        Assert.Equal(12.5, first.Laenge_m);
        Assert.Equal(300, first.ProfileHeight_mm);
        Assert.Equal(400, first.ProfileWidth_mm);
        Assert.Equal("Hauptstrasse", first.Strasse);
        Assert.Null(first.Ort);

        var second = result["06-002"];
        Assert.Equal(8.75, second.Laenge_m);
        Assert.Null(second.ProfileHeight_mm);
        Assert.Equal(250, second.ProfileWidth_mm);
        Assert.Null(second.Strasse);
        Assert.Equal("Wassen", second.Ort);

        Assert.Contains(messages, message => message.Contains("1 fehlerhafte", StringComparison.OrdinalIgnoreCase));
    }
}
