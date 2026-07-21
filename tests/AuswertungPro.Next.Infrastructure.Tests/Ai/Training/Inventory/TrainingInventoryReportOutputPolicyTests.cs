using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.Inventory;

public sealed class TrainingInventoryReportOutputPolicyTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "training-inventory-output-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ValidateTarget_AkzeptiertNurJsonImBerichtsordnerUndSchreibtNochNichts()
    {
        var output = Path.Combine(_root, "training", "reports", "inventory.json");

        var paths = TrainingInventoryReportOutputPolicy.ValidateTarget(
            output,
            _root,
            [],
            []);

        Assert.Equal(Path.GetFullPath(output), paths.ReportPath);
        Assert.Equal(Path.GetFullPath(output) + ".sha256", paths.Sha256Path);
        Assert.False(Directory.Exists(_root));
    }

    [Theory]
    [InlineData("inventory.txt")]
    [InlineData("..\\inventory.json")]
    [InlineData("..\\reports-nebenan\\inventory.json")]
    public void ValidateTarget_LehntFalscheEndungUndAusbruchAusBerichtsordnerAb(string relativeOutput)
    {
        var reportRoot = Path.Combine(_root, "training", "reports");
        var output = Path.Combine(reportRoot, relativeOutput);

        Assert.Throws<InvalidOperationException>(() =>
            TrainingInventoryReportOutputPolicy.ValidateTarget(output, _root, [], []));
    }

    [Fact]
    public void ValidateTarget_LehntBerichtUnterSuchOderSchutzordnerAb()
    {
        var reportRoot = Path.Combine(_root, "training", "reports");
        var output = Path.Combine(reportRoot, "inventory.json");

        Assert.Throws<InvalidOperationException>(() =>
            TrainingInventoryReportOutputPolicy.ValidateTarget(
                output,
                _root,
                [Path.Combine(_root, "training")],
                []));

        Assert.Throws<InvalidOperationException>(() =>
            TrainingInventoryReportOutputPolicy.ValidateTarget(
                output,
                _root,
                [],
                [reportRoot]));
    }

    [Fact]
    public void EnsureNoSourceCollision_SchuetztBerichtPruefsummeUndSicherungen()
    {
        var output = Path.Combine(_root, "training", "reports", "inventory.json");
        var paths = TrainingInventoryReportOutputPolicy.ValidateTarget(output, _root, [], []);

        foreach (var collision in new[]
                 {
                     paths.ReportPath,
                     paths.Sha256Path,
                     paths.ReportPath + ".bak",
                     paths.Sha256Path + ".bak"
                 })
        {
            Assert.Throws<InvalidOperationException>(() =>
                TrainingInventoryReportOutputPolicy.EnsureNoSourceCollision(paths, [collision]));
        }
    }

    [Fact]
    public void ValidateTarget_LehntSchutzwurzelAlsVerknuepfungAb()
    {
        var reportRoot = Directory.CreateDirectory(Path.Combine(_root, "training", "reports")).FullName;
        var alias = Path.Combine(_root, "protected-alias");
        try
        {
            Directory.CreateSymbolicLink(alias, reportRoot);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                   or IOException
                                   or PlatformNotSupportedException)
        {
            return;
        }

        Assert.Throws<InvalidOperationException>(() =>
            TrainingInventoryReportOutputPolicy.ValidateTarget(
                Path.Combine(reportRoot, "inventory.json"),
                _root,
                [],
                [alias]));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);

        GC.SuppressFinalize(this);
    }
}
