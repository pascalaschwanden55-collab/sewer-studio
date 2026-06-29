using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportSamplePersistenceUiControllerTests
{
    [Fact]
    public void Apply_loggt_persistenz_und_aktualisiert_kb_zaehler_auf_ui_thread()
    {
        var persistence = new TrainingBatchImportSamplePersistenceResult(
            SampleCount: 12,
            CodesCovered: 4,
            CandidateLogMessage: "kandidaten gespeichert",
            StoredLogMessage: "gesamt gespeichert");
        var calls = new List<string>();

        TrainingBatchImportSamplePersistenceUiController.Apply(
            persistence,
            calls.Add,
            action =>
            {
                calls.Add("on-ui");
                action();
            },
            value => calls.Add($"samples:{value}"),
            value => calls.Add($"codes:{value}"));

        Assert.Equal(
            new[]
            {
                "kandidaten gespeichert",
                "on-ui",
                "samples:12",
                "codes:4",
                "gesamt gespeichert"
            },
            calls);
    }
}
