using System;
using AuswertungPro.Next.Infrastructure;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtPdfParsingTests
{
    [Fact]
    public void ParseSchachtPdfPage_ExtractsNumberAndDate_FromSchachtprotokollLine()
    {
        var text = string.Join("\n", new[]
        {
            "GEP Aufnahmen Altdorf 2025",
            "Schachtprotokoll Nr. 74467",
            "Datum 02/10/2025",
            "Visum Bachmann"
        });

        var parsed = HoldingFolderDistributor.ParseSchachtPdfPage(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal("74467", parsed.ShaftNumber);
        Assert.Equal(new DateTime(2025, 10, 2), parsed.Date);
    }

    [Fact]
    public void ParseSchachtPdfPage_ExtractsNumberAndDate_FromLabeledFields()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll",
            "Schachtnummer: 12345",
            "Datum: 12.05.2025"
        });

        var parsed = HoldingFolderDistributor.ParseSchachtPdfPage(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal("12345", parsed.ShaftNumber);
        Assert.Equal(new DateTime(2025, 5, 12), parsed.Date);
    }
}
