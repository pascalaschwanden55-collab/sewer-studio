using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPrimaryDamageTextBuilderTests
{
    [Fact]
    public void Build_returns_empty_text_for_missing_or_empty_protocol()
    {
        Assert.Equal("", CodingPrimaryDamageTextBuilder.Build(null));
        Assert.Equal("", CodingPrimaryDamageTextBuilder.Build(new ProtocolDocument()));
    }

    [Fact]
    public void Build_ignores_deleted_entries_and_entries_without_code()
    {
        var doc = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries =
                {
                    new() { Code = "BAJ", Beschreibung = "deleted", MeterStart = 1.0, IsDeleted = true },
                    new() { Code = "", Beschreibung = "blank", MeterStart = 2.0 }
                }
            }
        };

        var text = CodingPrimaryDamageTextBuilder.Build(doc);

        Assert.Equal("", text);
    }

    [Fact]
    public void Build_formats_active_entries_with_primary_damage_mapper()
    {
        var doc = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries =
                {
                    new()
                    {
                        Code = "BAJ",
                        Beschreibung = "Riss",
                        MeterStart = 1.23,
                        CodeMeta = new ProtocolEntryCodeMeta
                        {
                            Parameters =
                            {
                                ["vsa.q1"] = "20"
                            }
                        }
                    },
                    new()
                    {
                        Code = "BAG",
                        Beschreibung = "Versatz",
                        MeterStart = 2.5
                    }
                }
            }
        };

        var text = CodingPrimaryDamageTextBuilder.Build(doc);

        Assert.Equal("1.23m BAJ Riss Q1=20\n2.50m BAG Versatz", text);
    }
}
