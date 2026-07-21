using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using SewerStudio.Tools.StageAExporter;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class StageAExporterCliTests
{
    [Fact]
    public async Task Hilfe_prueft_keine_Dateien_und_startet_keinen_Export()
    {
        var runner = new FakeRunner(Result(TrainingYoloExportResultStatus.Planned));
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await StageAExporterApp.RunAsync(
            ["--help", "--catalog", @"Z:\fehlt.json"],
            runner,
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(0, runner.Calls);
        Assert.Contains("AP-0.3", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task DryRun_wird_als_echter_PlanOnly_Lauf_weitergegeben()
    {
        var runner = new FakeRunner(Result(TrainingYoloExportResultStatus.Planned));
        var output = new StringWriter();
        var root = AbsoluteTestPath("knowledge");

        var exitCode = await StageAExporterApp.RunAsync(
            ["--knowledge-root", root, "--dry-run"],
            runner,
            output,
            new StringWriter());

        Assert.Equal(0, exitCode);
        Assert.Equal(1, runner.Calls);
        Assert.True(runner.LastOptions!.PlanOnly);
        Assert.Equal(Path.Combine(root, "training_samples.json"), runner.LastOptions.SourceSamplesPath);
        Assert.Equal(Path.Combine(root, "training", "datasets"), runner.LastOptions.DatasetRoot);
        Assert.Contains("STATUS: PLAN_GEPRUEFT", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("keine Datensatzdateien", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("--val-ratio", "0.2")]
    [InlineData("--allow-dummy-bbox", null)]
    public async Task Unsichere_Altoptionen_werden_vor_dem_Runner_abgelehnt(
        string option,
        string? value)
    {
        var runner = new FakeRunner(Result(TrainingYoloExportResultStatus.Planned));
        var args = value is null ? new[] { option } : new[] { option, value };
        var error = new StringWriter();

        var exitCode = await StageAExporterApp.RunAsync(
            args,
            runner,
            new StringWriter(),
            error);

        Assert.Equal(2, exitCode);
        Assert.Equal(0, runner.Calls);
        Assert.StartsWith("CLI-FEHLER:", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fremde_Quelldatei_und_fremder_Ausgabeordner_werden_abgelehnt()
    {
        var root = AbsoluteTestPath("knowledge");
        var runner = new FakeRunner(Result(TrainingYoloExportResultStatus.Planned));
        var foreignSourceError = new StringWriter();
        var foreignOutputError = new StringWriter();

        var sourceExit = await StageAExporterApp.RunAsync(
            ["--knowledge-root", root, "--source", Path.Combine(root, "andere.json")],
            runner,
            new StringWriter(),
            foreignSourceError);
        var outputExit = await StageAExporterApp.RunAsync(
            ["--knowledge-root", root, "--out", Path.Combine(root, "stage_a_clean")],
            runner,
            new StringWriter(),
            foreignOutputError);

        Assert.Equal(2, sourceExit);
        Assert.Equal(2, outputExit);
        Assert.Equal(0, runner.Calls);
        Assert.Contains("aktive Datei", foreignSourceError.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("zentrale Datensatzordner", foreignOutputError.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Kanonische_Kompatibilitaetspfade_bleiben_erlaubt()
    {
        var root = AbsoluteTestPath("knowledge");
        var runner = new FakeRunner(Result(TrainingYoloExportResultStatus.NoImages));

        var exitCode = await StageAExporterApp.RunAsync(
            [
                "--knowledge-root", root,
                "--source", Path.Combine(root, "training_samples.json"),
                "--out", Path.Combine(root, "training", "datasets"),
                "--require-bbox"
            ],
            runner,
            new StringWriter(),
            new StringWriter());

        Assert.Equal(0, exitCode);
        Assert.Equal(1, runner.Calls);
    }

    [Fact]
    public async Task Kontrollierter_Exportfehler_liefert_stabil_Exitcode_zwei()
    {
        var runner = new FakeRunner(new TrainingExportPlanException("Register fehlt"));
        var error = new StringWriter();

        var exitCode = await StageAExporterApp.RunAsync(
            ["--knowledge-root", AbsoluteTestPath("knowledge")],
            runner,
            new StringWriter(),
            error);

        Assert.Equal(2, exitCode);
        Assert.Contains("EXPORT-GESPERRT", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Benutzerabbruch_liefert_Exitcode_130()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var runner = new FakeRunner(new OperationCanceledException(cancellation.Token));

        var exitCode = await StageAExporterApp.RunAsync(
            ["--knowledge-root", AbsoluteTestPath("knowledge")],
            runner,
            new StringWriter(),
            new StringWriter(),
            cancellation.Token);

        Assert.Equal(130, exitCode);
    }

    private static string AbsoluteTestPath(string name)
        => Path.Combine(Path.GetTempPath(), "stage-a-cli-tests", name);

    private static TrainingYoloExportResult Result(TrainingYoloExportResultStatus status)
    {
        var plan = new TrainingExportPlan(
            TrainingExportPlan.CurrentSchemaVersion,
            new string('a', 64),
            DateTimeOffset.Parse("2026-07-17T08:00:00Z"),
            "inventory-run",
            new Dictionary<string, string>(),
            2,
            new string('b', 64),
            new string('c', 64),
            [],
            [],
            [],
            [],
            new Dictionary<string, int>(),
            [],
            []);
        return new TrainingYoloExportResult(
            status,
            plan,
            null,
            new TrainingExportCompletionResult(0, []));
    }

    private sealed class FakeRunner : IStageAExporterRunner
    {
        private readonly TrainingYoloExportResult? _result;
        private readonly Exception? _error;

        public FakeRunner(TrainingYoloExportResult result) => _result = result;

        public FakeRunner(Exception error) => _error = error;

        public int Calls { get; private set; }
        public StageAExporterCliOptions? LastOptions { get; private set; }

        public Task<TrainingYoloExportResult> RunAsync(
            StageAExporterCliOptions options,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastOptions = options;
            return _error is null
                ? Task.FromResult(_result!)
                : Task.FromException<TrainingYoloExportResult>(_error);
        }
    }
}
