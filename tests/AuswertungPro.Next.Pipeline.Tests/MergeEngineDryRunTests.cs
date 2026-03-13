using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Pipeline.Tests;

public class MergeEngineDryRunTests
{
    [Fact]
    public void DryRun_DoesNotMutateTarget()
    {
        var target = CreateRecord("H1", new Dictionary<string, string>
        {
            ["Haltungsname"] = "H1",
            ["DN_mm"] = ""
        });
        var source = CreateRecord("H1", new Dictionary<string, string>
        {
            ["Haltungsname"] = "H1",
            ["DN_mm"] = "300"
        });

        var log = new ImportRunLog();
        var ctx = new ImportRunContext(CancellationToken.None, null, log, dryRun: true);

        var result = MergeEngine.MergeRecord(target, source, FieldSource.Xtf, ctx: ctx);

        // Stats should count the update
        Assert.True(result.Updated > 0);
        // But target should NOT be mutated
        Assert.Equal("", (target.GetFieldValue("DN_mm") ?? "").Trim());
    }

    [Fact]
    public void NormalRun_DoesApplyChanges()
    {
        var target = CreateRecord("H1", new Dictionary<string, string>
        {
            ["Haltungsname"] = "H1",
            ["DN_mm"] = ""
        });
        var source = CreateRecord("H1", new Dictionary<string, string>
        {
            ["Haltungsname"] = "H1",
            ["DN_mm"] = "300"
        });

        var log = new ImportRunLog();
        var ctx = new ImportRunContext(CancellationToken.None, null, log, dryRun: false);

        var result = MergeEngine.MergeRecord(target, source, FieldSource.Xtf, ctx: ctx);

        Assert.True(result.Updated > 0);
        Assert.Equal("300", (target.GetFieldValue("DN_mm") ?? "").Trim());
    }

    [Fact]
    public void DryRun_LogsEntries()
    {
        var target = CreateRecord("H1", new Dictionary<string, string>
        {
            ["Haltungsname"] = "H1",
            ["DN_mm"] = "",
            ["Rohrmaterial"] = ""
        });
        var source = CreateRecord("H1", new Dictionary<string, string>
        {
            ["Haltungsname"] = "H1",
            ["DN_mm"] = "300",
            ["Rohrmaterial"] = "PVC"
        });

        var log = new ImportRunLog();
        var ctx = new ImportRunContext(CancellationToken.None, null, log, dryRun: true);

        MergeEngine.MergeRecord(target, source, FieldSource.Xtf, ctx: ctx);

        Assert.True(log.TotalUpdated >= 2);
        Assert.All(log.EntriesList.Where(e => e.Status == ImportLogStatus.Updated),
            e => Assert.Equal("H1", e.RecordKey));
    }

    [Fact]
    public void DryRun_SameStatsAsRealRun()
    {
        var fields = new Dictionary<string, string>
        {
            ["Haltungsname"] = "H1",
            ["DN_mm"] = "200",
            ["Rohrmaterial"] = ""
        };
        var sourceFields = new Dictionary<string, string>
        {
            ["Haltungsname"] = "H1",
            ["DN_mm"] = "300",
            ["Rohrmaterial"] = "PVC"
        };

        // Dry run
        var targetDry = CreateRecord("H1", new Dictionary<string, string>(fields));
        var sourceDry = CreateRecord("H1", new Dictionary<string, string>(sourceFields));
        var logDry = new ImportRunLog();
        var ctxDry = new ImportRunContext(CancellationToken.None, null, logDry, dryRun: true);
        var resultDry = MergeEngine.MergeRecord(targetDry, sourceDry, FieldSource.Xtf, ctx: ctxDry);

        // Real run
        var targetReal = CreateRecord("H1", new Dictionary<string, string>(fields));
        var sourceReal = CreateRecord("H1", new Dictionary<string, string>(sourceFields));
        var logReal = new ImportRunLog();
        var ctxReal = new ImportRunContext(CancellationToken.None, null, logReal, dryRun: false);
        var resultReal = MergeEngine.MergeRecord(targetReal, sourceReal, FieldSource.Xtf, ctx: ctxReal);

        Assert.Equal(resultDry.Updated, resultReal.Updated);
        Assert.Equal(resultDry.Conflicts, resultReal.Conflicts);
        Assert.Equal(resultDry.Errors, resultReal.Errors);
    }

    [Fact]
    public void NullCtx_WorksAsBeforeWithoutLogging()
    {
        var target = CreateRecord("H1", new Dictionary<string, string>
        {
            ["Haltungsname"] = "H1",
            ["DN_mm"] = ""
        });
        var source = CreateRecord("H1", new Dictionary<string, string>
        {
            ["Haltungsname"] = "H1",
            ["DN_mm"] = "300"
        });

        var result = MergeEngine.MergeRecord(target, source, FieldSource.Xtf);

        Assert.True(result.Updated > 0);
        Assert.Equal("300", (target.GetFieldValue("DN_mm") ?? "").Trim());
    }

    private static HaltungRecord CreateRecord(string key, Dictionary<string, string> fields)
    {
        var record = new HaltungRecord();
        foreach (var (field, value) in fields)
        {
            record.SetFieldValue(field, value, FieldSource.Manual, userEdited: false);
        }
        return record;
    }
}
