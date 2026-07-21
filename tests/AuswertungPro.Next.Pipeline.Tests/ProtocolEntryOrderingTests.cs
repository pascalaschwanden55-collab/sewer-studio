using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolEntryOrderingTests
{
    [Fact]
    public void Order_verwendet_Start_dann_End_dann_VsaDistanz_dann_Distance()
    {
        var distance = Entry("distance", parameters: new() { ["Distance"] = "4.5" });
        var vsa = Entry("vsa", parameters: new()
        {
            ["vsa.distanz"] = "3,5",
            ["Distance"] = "0.5"
        });
        var end = Entry("end", meterEnd: 2);
        var start = Entry("start", meterStart: 1, meterEnd: 99);

        var result = ProtocolEntryOrdering.Order([distance, vsa, end, start]);

        Assert.Equal([start, end, vsa, distance], result);
    }

    [Fact]
    public void Order_nutzt_Distance_wenn_VsaDistanz_ungueltig_ist()
    {
        var fallback = Entry("fallback", parameters: new()
        {
            ["vsa.distanz"] = "ungueltig",
            ["Distance"] = "1,25"
        });
        var later = Entry("later", meterStart: 2);

        var result = ProtocolEntryOrdering.Order([later, fallback]);

        Assert.Equal([fallback, later], result);
    }

    [Fact]
    public void Order_sortiert_bei_gleichem_Start_nach_End_und_Code_stabil()
    {
        var laterEnd = Entry("Z", meterStart: 10, meterEnd: 12);
        var codeLower = Entry("a", meterStart: 10, meterEnd: 11);
        var codeUpper = Entry("A", meterStart: 10, meterEnd: 11);
        var noEnd = Entry("Z", meterStart: 10);

        var result = ProtocolEntryOrdering.Order([laterEnd, codeLower, codeUpper, noEnd]);

        Assert.Equal([noEnd, codeLower, codeUpper, laterEnd], result);
    }

    [Fact]
    public void Order_stellt_fehlende_und_ungueltige_Meter_nach_gueltige_Eintraege()
    {
        var invalid = Entry("B", parameters: new() { ["Distance"] = "1,2,3" });
        var missing = Entry("A");
        var valid = Entry("Z", meterStart: 100);

        var result = ProtocolEntryOrdering.Order([invalid, missing, valid]);

        Assert.Equal([valid, missing, invalid], result);
    }

    [Fact]
    public void Order_bewahrt_das_bisherige_Verhalten_fuer_NaN_und_Unendlich()
    {
        var positiveInfinity = Entry("plus", meterStart: double.PositiveInfinity);
        var finite = Entry("finite", meterStart: 0);
        var nan = Entry("nan", meterStart: double.NaN);
        var negativeInfinity = Entry("minus", meterStart: double.NegativeInfinity);
        var missing = Entry("missing");

        var result = ProtocolEntryOrdering.Order(
            [positiveInfinity, finite, nan, negativeInfinity, missing]);

        Assert.Equal([nan, negativeInfinity, finite, positiveInfinity, missing], result);
    }

    [Fact]
    public void Order_stellt_geloeschte_unveraendert_ans_Ende_und_mutiert_die_Eingabe_nicht()
    {
        var deletedFirst = Entry("deleted-first", meterStart: 0, deleted: true);
        var activeLater = Entry("active-later", meterStart: 20);
        var deletedSecond = Entry("deleted-second", meterStart: 1, deleted: true);
        var activeEarlier = Entry("active-earlier", meterStart: 10);
        var input = new List<ProtocolEntry>
        {
            deletedFirst,
            activeLater,
            deletedSecond,
            activeEarlier
        };

        var result = ProtocolEntryOrdering.Order(input);

        Assert.Equal([activeEarlier, activeLater, deletedFirst, deletedSecond], result);
        Assert.Equal([deletedFirst, activeLater, deletedSecond, activeEarlier], input);
    }

    private static ProtocolEntry Entry(
        string code,
        double? meterStart = null,
        double? meterEnd = null,
        bool deleted = false,
        Dictionary<string, string>? parameters = null)
        => new()
        {
            Code = code,
            MeterStart = meterStart,
            MeterEnd = meterEnd,
            IsDeleted = deleted,
            CodeMeta = parameters is null
                ? null
                : new ProtocolEntryCodeMeta { Parameters = parameters }
        };
}
