using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// V4.3 — Anzeige-String fuer Wert+Einheit+Tool+Subject im Protokoll-UI.
/// </summary>
[Trait("Category", "Unit")]
public class ProtocolEntryVmMetadataTests
{
    private static ProtocolEntryVM Make()
    {
        var entry = new ProtocolEntry { Code = "BAB" };
        return new ProtocolEntryVM(entry);
    }

    [Fact]
    public void Display_OnlyValue()
    {
        var vm = Make();
        vm.VsaQ1 = "15";
        Assert.Equal("15", vm.VsaQ1Display);
    }

    [Fact]
    public void Display_ValueAndUnit()
    {
        var vm = Make();
        vm.VsaQ1 = "15";
        vm.VsaQ1Unit = "mm";
        Assert.Equal("15 mm", vm.VsaQ1Display);
    }

    [Fact]
    public void Display_ValueUnitTool()
    {
        var vm = Make();
        vm.VsaQ1 = "15";
        vm.VsaQ1Unit = "mm";
        vm.MeasurementTool = "Lineal";
        Assert.Equal("15 mm (Lineal)", vm.VsaQ1Display);
    }

    [Fact]
    public void Display_ValueUnitToolSubject()
    {
        var vm = Make();
        vm.VsaQ1 = "20";
        vm.VsaQ1Unit = "%";
        vm.MeasurementTool = "Querschnitt";
        vm.MeasurementSubject = "Wurzel";
        Assert.Equal("20 % (Querschnitt \u2014 Wurzel)", vm.VsaQ1Display);
    }

    [Fact]
    public void Display_SonstigeSubjectNotShown()
    {
        var vm = Make();
        vm.VsaQ1 = "10";
        vm.VsaQ1Unit = "%";
        vm.MeasurementTool = "Querschnitt";
        vm.MeasurementSubject = "Sonstige";
        Assert.Equal("10 % (Querschnitt)", vm.VsaQ1Display);
    }

    [Fact]
    public void Display_Empty()
    {
        var vm = Make();
        Assert.Equal("", vm.VsaQ1Display);
    }
}
