using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public class ImportPlausibilityValidatorTests
{
    private static HaltungRecord Record(string name, string? dn = null, string? laenge = null)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: false);
        if (dn is not null) r.SetFieldValue("DN_mm", dn, FieldSource.Manual, userEdited: false);
        if (laenge is not null) r.SetFieldValue("Haltungslaenge_m", laenge, FieldSource.Manual, userEdited: false);
        return r;
    }

    [Fact]
    public void PlausibleWerte_KeineWarnung()
    {
        var r = Record("06.1-2", dn: "300", laenge: "45.0");
        Assert.Empty(ImportPlausibilityValidator.Validate(r));
    }

    [Theory]
    [InlineData("10")]      // zu klein
    [InlineData("99999")]   // zu gross
    public void DnAusserhalbBereich_Warnung(string dn)
    {
        var r = Record("06.1-2", dn: dn);
        Assert.NotEmpty(ImportPlausibilityValidator.Validate(r));
    }

    [Fact]
    public void Meterstand_HinterHaltungslaenge_Warnung()
    {
        var r = Record("06.1-2", dn: "300", laenge: "40.0");
        r.Protocol = new ProtocolDocument
        {
            HaltungId = "06.1-2",
            Current = new ProtocolRevision
            {
                Entries = new List<ProtocolEntry>
                {
                    new() { Code = "BAB", MeterStart = 55.0 }  // 55 m in 40-m-Haltung
                }
            }
        };

        var warnings = ImportPlausibilityValidator.Validate(r);
        Assert.Contains(warnings, w => w.Contains("hinter der Haltungslaenge"));
    }

    [Fact]
    public void Meterstand_InnerhalbToleranz_KeineWarnung()
    {
        var r = Record("06.1-2", dn: "300", laenge: "40.0");
        r.Protocol = new ProtocolDocument
        {
            HaltungId = "06.1-2",
            Current = new ProtocolRevision
            {
                Entries = new List<ProtocolEntry> { new() { Code = "BAB", MeterStart = 40.5 } }
            }
        };

        Assert.Empty(ImportPlausibilityValidator.Validate(r));
    }
}
