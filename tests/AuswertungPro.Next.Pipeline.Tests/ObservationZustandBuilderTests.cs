using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ObservationZustandBuilderTests
{
    [Fact]
    public void Build_without_catalog_returns_description()
    {
        var entry = new ProtocolEntry { Code = "BCCBY", MeterStart = 0, Beschreibung = "Bogen nach rechts" };

        var text = ObservationZustandBuilder.Build(entry, catalog: null);

        Assert.Equal("Bogen nach rechts", text);
    }

    [Fact]
    public void Build_without_catalog_is_behaviour_neutral_for_empty_description()
    {
        var entry = new ProtocolEntry
        {
            Code = "BCCBY",
            MeterStart = 0,
            Beschreibung = "",
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BCCBY", Parameters = { ["Quantifizierung1"] = "45" } }
        };

        var text = ObservationZustandBuilder.Build(entry, catalog: null);

        Assert.Equal(ProtocolZustandText.BuildObservationZustandTextLong(entry), text);
        Assert.Equal("Q1=45", text);
    }

    [Fact]
    public void Build_with_catalog_formats_named_quantifier_with_unit()
    {
        var catalog = new FakeCatalog(new CodeDefinition
        {
            Code = "BCCBY",
            Title = "Bogen",
            Parameters =
            {
                new CodeParameter { Name = "Winkel", DataKey = "Quantifizierung1", Unit = "°" }
            }
        });

        var entry = new ProtocolEntry
        {
            Code = "BCCBY",
            MeterStart = 0,
            Beschreibung = "Bogen nach rechts",
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BCCBY", Parameters = { ["Quantifizierung1"] = "45" } }
        };

        var text = ObservationZustandBuilder.Build(entry, catalog);

        Assert.Equal("Bogen nach rechts, Winkel = 45°", text);
    }

    [Fact]
    public void Build_with_catalog_adds_clock_position()
    {
        var catalog = new FakeCatalog(new CodeDefinition { Code = "BAB", Title = "Riss" });

        var entry = new ProtocolEntry
        {
            Code = "BAB",
            MeterStart = 1.2,
            Beschreibung = "Riss",
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BAB", Parameters = { ["ClockPos1"] = "3" } }
        };

        var text = ObservationZustandBuilder.Build(entry, catalog);

        Assert.Equal("Riss, Lage 3 Uhr", text);
    }

    [Fact]
    public void Build_with_catalog_falls_back_to_raw_quantifier_when_no_param_mapping()
    {
        var catalog = new FakeCatalog(new CodeDefinition { Code = "BCCBY", Title = "Bogen" });

        var entry = new ProtocolEntry
        {
            Code = "BCCBY",
            MeterStart = 0,
            Beschreibung = "Bogen nach rechts",
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BCCBY", Parameters = { ["Quantifizierung1"] = "45" } }
        };

        var text = ObservationZustandBuilder.Build(entry, catalog);

        Assert.Equal("Bogen nach rechts, Q1 = 45", text);
    }

    [Fact]
    public void Build_with_catalog_does_not_double_count_same_value_under_two_keys()
    {
        // Katalog mappt Q1 via DataKey "Kruemmungswinkel"; der Wert liegt im Entry aber
        // zusaetzlich unter "Quantifizierung1" (WinCan-Import). Der benannte Parameter greift,
        // der rohe Q1-Fallback darf denselben Wert NICHT ein zweites Mal ausgeben.
        var catalog = new FakeCatalog(new CodeDefinition
        {
            Code = "BCCBY",
            Title = "Bogen",
            Parameters =
            {
                new CodeParameter { Name = "Winkel", DataKey = "Kruemmungswinkel", Unit = "°" }
            }
        });

        var entry = new ProtocolEntry
        {
            Code = "BCCBY",
            MeterStart = 0,
            Beschreibung = "Bogen nach rechts",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = "BCCBY",
                Parameters =
                {
                    ["Kruemmungswinkel"] = "45",
                    ["Quantifizierung1"] = "45"
                }
            }
        };

        var text = ObservationZustandBuilder.Build(entry, catalog);

        Assert.Equal("Bogen nach rechts, Winkel = 45°", text);
        Assert.DoesNotContain("Q1", text);
    }

    [Fact]
    public void Build_with_catalog_but_unknown_code_is_behaviour_neutral()
    {
        var catalog = new FakeCatalog(); // leer

        var entry = new ProtocolEntry
        {
            Code = "BCCBY",
            MeterStart = 0,
            Beschreibung = "",
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BCCBY", Parameters = { ["Quantifizierung1"] = "45" } }
        };

        var text = ObservationZustandBuilder.Build(entry, catalog);

        Assert.Equal(ProtocolZustandText.BuildObservationZustandTextLong(entry), text);
    }

    private sealed class FakeCatalog : ICodeCatalogProvider
    {
        private readonly Dictionary<string, CodeDefinition> _byCode =
            new(StringComparer.OrdinalIgnoreCase);

        public FakeCatalog(params CodeDefinition[] defs)
        {
            foreach (var d in defs)
                _byCode[d.Code] = d;
        }

        public IReadOnlyList<CodeDefinition> GetAll() => _byCode.Values.ToList();

        public bool TryGet(string code, out CodeDefinition def)
        {
            if (code is not null && _byCode.TryGetValue(code, out var found))
            {
                def = found;
                return true;
            }
            def = new CodeDefinition();
            return false;
        }

        public void Save(IReadOnlyList<CodeDefinition> codes) => throw new NotSupportedException();
        public IReadOnlyList<string> AllowedCodes() => _byCode.Keys.ToList();
        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => Array.Empty<string>();
    }
}
