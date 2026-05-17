using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Diagnostics;

namespace AuswertungPro.Next.Pipeline.Tests;

public class AiDiagnosticsRecorderTests
{
    [Fact]
    public void Record_StoresEvent_AndSnapshotReturnsNewestFirst()
    {
        var rec = new AiDiagnosticsRecorder(capacity: 10);
        rec.Record(new AiDiagnosticEvent { Stage = AiDiagnosticStage.QwenRaw, Summary = "first" });
        rec.Record(new AiDiagnosticEvent { Stage = AiDiagnosticStage.QwenMapped, Summary = "second" });

        var snap = rec.Snapshot();

        Assert.Equal(2, snap.Count);
        Assert.Equal("second", snap[0].Summary);
        Assert.Equal("first",  snap[1].Summary);
    }

    [Fact]
    public void Record_BeyondCapacity_DropsOldestEntries()
    {
        var rec = new AiDiagnosticsRecorder(capacity: 3);
        for (int i = 0; i < 6; i++)
            rec.Record(new AiDiagnosticEvent { Stage = AiDiagnosticStage.QwenRaw, Summary = $"e{i}" });

        var snap = rec.Snapshot();

        Assert.Equal(3, snap.Count);
        // Juengster zuerst, die ersten drei (e0..e2) sind weg.
        Assert.Equal("e5", snap[0].Summary);
        Assert.Equal("e4", snap[1].Summary);
        Assert.Equal("e3", snap[2].Summary);
    }

    [Fact]
    public void Record_TruncatesRawOutputBeyondLimit()
    {
        var rec = new AiDiagnosticsRecorder(capacity: 5, maxRawOutputChars: 50);
        var longRaw = new string('x', 300);

        rec.Record(new AiDiagnosticEvent
        {
            Stage = AiDiagnosticStage.QwenRaw,
            RawOutput = longRaw
        });

        var stored = rec.Snapshot()[0];
        Assert.NotNull(stored.RawOutput);
        Assert.StartsWith(new string('x', 50), stored.RawOutput);
        Assert.Contains("truncated", stored.RawOutput);
        Assert.True(stored.RawOutput.Length < 200); // Original war 300, jetzt deutlich kleiner
    }

    [Fact]
    public void Record_KeepsShortRawOutputUntouched()
    {
        var rec = new AiDiagnosticsRecorder(capacity: 5, maxRawOutputChars: 100);
        var shortRaw = "compact";

        rec.Record(new AiDiagnosticEvent { Stage = AiDiagnosticStage.QwenRaw, RawOutput = shortRaw });

        Assert.Equal(shortRaw, rec.Snapshot()[0].RawOutput);
    }

    [Fact]
    public void Snapshot_LimitClampsResult()
    {
        var rec = new AiDiagnosticsRecorder(capacity: 50);
        for (int i = 0; i < 20; i++)
            rec.Record(new AiDiagnosticEvent { Stage = AiDiagnosticStage.QwenRaw, Summary = $"e{i}" });

        Assert.Equal(5, rec.Snapshot(5).Count);
        Assert.Equal(20, rec.Snapshot(50).Count);
        Assert.Empty(rec.Snapshot(0));
    }

    [Fact]
    public void Record_NullEvent_DoesNotThrow()
    {
        var rec = new AiDiagnosticsRecorder();
        rec.Record(null!);
        Assert.Empty(rec.Snapshot());
    }

    [Fact]
    public void Clear_EmptiesRingbuffer()
    {
        var rec = new AiDiagnosticsRecorder();
        rec.Record(new AiDiagnosticEvent { Stage = AiDiagnosticStage.QwenRaw, Summary = "x" });

        rec.Clear();

        Assert.Empty(rec.Snapshot());
    }

    [Fact]
    public async Task Record_IsThreadSafe_UnderParallelLoad()
    {
        var rec = new AiDiagnosticsRecorder(capacity: 1000);

        // 200 Tasks á 50 Records = 10000 parallel — Recorder darf nicht crashen
        // und der finale Snapshot muss <= Capacity bleiben.
        var tasks = Enumerable.Range(0, 200).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < 50; i++)
            {
                rec.Record(new AiDiagnosticEvent
                {
                    Stage = AiDiagnosticStage.QwenRaw,
                    Summary = $"t{t}-i{i}"
                });
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        var snap = rec.Snapshot(int.MaxValue);
        Assert.True(snap.Count <= 1000, $"Snapshot Count {snap.Count} ueberschreitet Capacity 1000");
        Assert.True(snap.Count > 0);
    }

    [Fact]
    public void Provider_DefaultReturnsNullRecorder_RecordIsNoOp()
    {
        AiDiagnosticsRecorderProvider.Reset();

        // Darf nicht crashen, liefert null-Recorder.
        AiDiagnosticsRecorderProvider.Current.Record(new AiDiagnosticEvent { Stage = AiDiagnosticStage.QwenRaw });

        Assert.False(AiDiagnosticsRecorderProvider.HasRecorder);
        Assert.Empty(AiDiagnosticsRecorderProvider.Current.Snapshot());
    }

    [Fact]
    public void Provider_AfterSet_DelegatesToInstance()
    {
        AiDiagnosticsRecorderProvider.Reset();
        var real = new AiDiagnosticsRecorder(capacity: 5);
        AiDiagnosticsRecorderProvider.Set(real);

        AiDiagnosticsRecorderProvider.Current.Record(new AiDiagnosticEvent
        {
            Stage = AiDiagnosticStage.QwenRaw,
            Summary = "via-provider"
        });

        Assert.True(AiDiagnosticsRecorderProvider.HasRecorder);
        Assert.Equal("via-provider", real.Snapshot()[0].Summary);

        AiDiagnosticsRecorderProvider.Reset();
    }

    [Fact]
    public void Event_MetadataIsImmutable()
    {
        var meta = new Dictionary<string, string> { ["meter"] = "0.71" };
        var evt = new AiDiagnosticEvent
        {
            Stage = AiDiagnosticStage.QwenRaw,
            Metadata = meta
        };

        // Eigentlich IReadOnlyDictionary — Compile-Time-Safe, aber prueft auch
        // Konsistenz beim Auslesen.
        Assert.Equal("0.71", evt.Metadata!["meter"]);
        Assert.Single(evt.Metadata);
    }
}
