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
    public void Build_with_catalog_adds_missing_primary_title_before_operator_note()
    {
        var catalog = new FakeCatalog(new CodeDefinition
        {
            Code = "BCE",
            Title = "Rohrende"
        });
        var entry = new ProtocolEntry
        {
            Code = "BCE",
            MeterStart = 2.9,
            Beschreibung = "Anschluss von 12 Uhr in Schmutzleitung"
        };

        var text = ObservationZustandBuilder.Build(entry, catalog);

        Assert.Equal("Rohrende, Anschluss von 12 Uhr in Schmutzleitung", text);
    }

    [Fact]
    public void Build_with_catalog_does_not_add_clock_to_non_clock_rohrende_code()
    {
        var catalog = new FakeCatalog(new CodeDefinition
        {
            Code = "BCE",
            Title = "Rohrende"
        });
        var entry = new ProtocolEntry
        {
            Code = "BCE",
            Beschreibung = "Anschluss von 12 Uhr in Schmutzleitung",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters = { ["vsa.uhr.von"] = "12" }
            }
        };

        var text = ObservationZustandBuilder.Build(entry, catalog);

        Assert.Equal("Rohrende, Anschluss von 12 Uhr in Schmutzleitung", text);
    }

    [Fact]
    public void Build_with_catalog_does_not_repeat_title_already_in_description()
    {
        var catalog = new FakeCatalog(new CodeDefinition
        {
            Code = "BCD",
            Title = "Rohranfang"
        });
        var entry = new ProtocolEntry
        {
            Code = "BCD",
            MeterStart = 0,
            Beschreibung = "Rohranfang"
        };

        var text = ObservationZustandBuilder.Build(entry, catalog);

        Assert.Equal("Rohranfang", text);
    }

    [Fact]
    public void Build_with_catalog_preserves_note_when_description_is_empty()
    {
        var catalog = new FakeCatalog(new CodeDefinition
        {
            Code = "BCE",
            Title = "Rohrende"
        });
        var entry = new ProtocolEntry
        {
            Code = "BCE",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Notes = "Anschluss in Schmutzleitung"
            }
        };

        var text = ObservationZustandBuilder.Build(entry, catalog);

        Assert.Equal("Rohrende, Anschluss in Schmutzleitung", text);
    }

    [Fact]
    public void Build_with_catalog_adds_clock_position()
    {
        var catalog = new FakeCatalog(new CodeDefinition
        {
            Code = "BAB",
            Title = "Riss",
            Parameters =
            {
                new CodeParameter
                {
                    Name = "Uhrlage Anfang",
                    DataKey = "SchadenlageAnfang",
                    Type = "clock"
                }
            }
        });

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
    public void Build_with_catalog_does_not_repeat_clock_already_in_description()
    {
        var catalog = new FakeCatalog(new CodeDefinition { Code = "BCAAA", Title = "Anschluss" });
        var entry = new ProtocolEntry
        {
            Code = "BCAAA",
            Beschreibung = "Anschluss mit Formstueck, offen, bei 9 Uhr",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters = { ["ClockPos1"] = "9" }
            }
        };

        var text = ObservationZustandBuilder.Build(entry, catalog);

        Assert.Equal("Anschluss mit Formstueck, offen, bei 9 Uhr", text);
    }

    [Fact]
    public void Build_with_catalog_does_not_repeat_clock_range_already_in_description()
    {
        var catalog = new FakeCatalog(new CodeDefinition { Code = "BAAA", Title = "Rohr deformiert" });
        var entry = new ProtocolEntry
        {
            Code = "BAAA",
            Beschreibung = "Rohr vertikal deformiert, von 4 Uhr bis 8 Uhr, Start",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters =
                {
                    ["ClockPos1"] = "4",
                    ["ClockPos2"] = "8"
                }
            }
        };

        var text = ObservationZustandBuilder.Build(entry, catalog);

        Assert.Equal("Rohr deformiert, Rohr vertikal deformiert, von 4 Uhr bis 8 Uhr, Start", text);
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
    }

    [Fact]
    public void Build_with_catalog_uses_valid_clock_alias_and_does_not_repeat_clock_parameter()
    {
        var catalog = new FakeCatalog(new CodeDefinition
        {
            Code = "BCAAA",
            Title = "Seitlicher Anschluss",
            Parameters =
            {
                new CodeParameter
                {
                    Name = "Uhrlage Anfang",
                    DataKey = "SchadenlageAnfang",
                    Type = "clock"
                }
            }
        });
        var entry = new ProtocolEntry
        {
            Code = "BCAAA",
            Beschreibung = "offen",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = "BCAAA",
                Parameters =
                {
                    ["vsa.uhr.von"] = "2.62136",
                    ["ClockPos1"] = "9",
                    ["SchadenlageAnfang"] = "9"
                }
            }
        };

        var text = ObservationZustandBuilder.Build(entry, catalog);

        Assert.Equal("Seitlicher Anschluss, offen, Lage 9 Uhr", text);
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
