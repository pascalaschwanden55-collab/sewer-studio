using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Map;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Map;

public class HaltungConditionProviderTests
{
    [Fact]
    public void Build_MapptHaltungsnameAufZustand()
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", "A-B", FieldSource.Manual, userEdited: true);
        r.SetFieldValue("Zustandsklasse", "3", FieldSource.Manual, userEdited: true);

        var map = HaltungConditionProvider.Build(new List<HaltungRecord> { r });

        Assert.True(map.ContainsKey("A-B"));
        Assert.Equal(3, map["A-B"]);
    }

    [Fact]
    public void Build_OhneZustandsklasse_ErgibtNull()
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", "C-D", FieldSource.Manual, userEdited: true);
        // Zustandsklasse bleibt leer

        var map = HaltungConditionProvider.Build(new List<HaltungRecord> { r });

        Assert.True(map.ContainsKey("C-D"));
        Assert.Null(map["C-D"]);
    }

    [Fact]
    public void Build_OhneHaltungsname_WirdIgnoriert()
    {
        var r = new HaltungRecord();
        // Haltungsname bleibt leer

        var map = HaltungConditionProvider.Build(new List<HaltungRecord> { r });

        Assert.Empty(map);
    }
}
