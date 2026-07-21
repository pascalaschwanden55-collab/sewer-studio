using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Protocol;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProtocolEntryVmApplyCodeSelectionTests
{
    [Fact]
    public void ApplyCodeSelection_bereinigt_Parameter_spiegelt_Aliase_und_erhaelt_die_UI_Fassade()
    {
        var codeMeta = new ProtocolEntryCodeMeta
        {
            Code = "ALT",
            UpdatedAt = DateTimeOffset.UnixEpoch,
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["alt"] = "bleibt nicht"
            }
        };
        var entry = new ProtocolEntry
        {
            Code = "ALT",
            MeterStart = 9,
            MeterEnd = 10,
            CodeMeta = codeMeta
        };
        var input = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [" custom "] = " value ",
            [" "] = "ignoriert",
            ["leer"] = "   ",
            ["Distance"] = "5",
            ["vsa.distanz"] = "6",
            ["TimeCtr"] = "01:02",
            ["Q1"] = "primaer",
            ["Quantifizierung1"] = "sekundaer"
        };
        var changedProperties = new List<string?>();
        var viewModel = new ProtocolEntryVM(entry);
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        var before = DateTimeOffset.UtcNow;
        viewModel.ApplyCodeSelection(
            " BABAC ",
            input,
            meterStart: 1.25,
            meterEnd: 2.5,
            severity: " hoch ",
            count: 3,
            notes: " Hinweis ");
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(" BABAC ", entry.Code);
        Assert.Equal(1.25, entry.MeterStart);
        Assert.Equal(2.5, entry.MeterEnd);
        Assert.Same(codeMeta, entry.CodeMeta);
        Assert.Equal(" BABAC ", entry.CodeMeta.Code);
        Assert.Equal("hoch", entry.CodeMeta.Severity);
        Assert.Equal(3, entry.CodeMeta.Count);
        Assert.Equal("Hinweis", entry.CodeMeta.Notes);
        Assert.InRange(entry.CodeMeta.UpdatedAt, before, after);

        var result = entry.CodeMeta.Parameters;
        Assert.Equal("value", result["custom"]);
        Assert.Equal("6", result["vsa.distanz"]);
        Assert.Equal("6", result["Distance"]);
        Assert.Equal("01:02", result["vsa.video"]);
        Assert.Equal("01:02", result["TimeCtr"]);
        Assert.Equal("primaer", result["vsa.q1"]);
        Assert.Equal("primaer", result["Q1"]);
        Assert.Equal("primaer", result["Quantifizierung1"]);
        Assert.Equal("BABAC", result["vsa.code"]);
        Assert.Equal("BABAC", result["Code"]);
        Assert.DoesNotContain("leer", result.Keys);
        Assert.DoesNotContain("alt", result.Keys);

        Assert.Contains(" custom ", input.Keys);
        Assert.Equal("5", input["Distance"]);
        Assert.Equal("sekundaer", input["Quantifizierung1"]);
        var expectedNotifications = new[]
        {
            nameof(ProtocolEntryVM.Code),
            nameof(ProtocolEntryVM.MeterStart),
            nameof(ProtocolEntryVM.MeterEnd),
            nameof(ProtocolEntryVM.Severity),
            nameof(ProtocolEntryVM.Count),
            nameof(ProtocolEntryVM.CodeNotes),
            nameof(ProtocolEntryVM.Parameters)
        };
        Assert.Equal(expectedNotifications.Length, changedProperties.Count);
        Assert.All(expectedNotifications, expected => Assert.Contains(expected, changedProperties));
    }
}
