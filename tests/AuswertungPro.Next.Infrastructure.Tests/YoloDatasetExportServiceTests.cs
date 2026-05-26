using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class YoloDatasetExportServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-yolo-export-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best effort test cleanup
        }
    }

    [Fact]
    public async Task ExportAsync_sperrt_unbekannte_und_observed_codes_aus_Katalog()
    {
        Directory.CreateDirectory(_root);
        var frameRoot = Path.Combine(_root, "frames");
        Directory.CreateDirectory(frameRoot);

        var validFrame = Path.Combine(frameRoot, "valid.png");
        var unknownFrame = Path.Combine(frameRoot, "unknown.png");
        var observedFrame = Path.Combine(frameRoot, "observed.png");
        await File.WriteAllBytesAsync(validFrame, [1, 2, 3]);
        await File.WriteAllBytesAsync(unknownFrame, [4, 5, 6]);
        await File.WriteAllBytesAsync(observedFrame, [7, 8, 9]);

        var samples = new[]
        {
            MakeSample("valid", "BBAA", validFrame),
            MakeSample("unknown", "BZZZ", unknownFrame),
            MakeSample("observed", "BCCYY", observedFrame)
        };

        var output = Path.Combine(_root, "out");
        var service = new YoloDatasetExportService(CreateTestCatalog());

        var result = await service.ExportAsync(samples, output);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, result.TotalExported);
        Assert.Single(Directory.EnumerateFiles(Path.Combine(output, "images"), "*.png", SearchOption.AllDirectories));

        var yaml = await File.ReadAllTextAsync(result.YamlPath!);
        Assert.Contains("'BBA'", yaml);
        Assert.DoesNotContain("BZZ", yaml);
        Assert.DoesNotContain("BCC", yaml);
    }

    private static TrainingSample MakeSample(string sampleId, string code, string framePath)
        => new()
        {
            SampleId = sampleId,
            Code = code,
            FramePath = framePath,
            Status = TrainingSampleStatus.Approved,
            InspectionDate = new DateTime(2022, 1, 1),
            TrainingEligible = true
        };

    private static ICodeCatalogProvider CreateTestCatalog()
        => new InMemoryCodeCatalogProvider(
        [
            new CodeDefinition { Code = "BBAA", IsSelectable = true },
            new CodeDefinition { Code = "BCCYY", IsSelectable = false, IsObservedExtension = true }
        ]);

    private sealed class InMemoryCodeCatalogProvider : ICodeCatalogProvider
    {
        private readonly IReadOnlyList<CodeDefinition> _codes;

        public InMemoryCodeCatalogProvider(IReadOnlyList<CodeDefinition> codes)
            => _codes = codes;

        public IReadOnlyList<CodeDefinition> GetAll()
            => _codes;

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = _codes.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
                ?? new CodeDefinition();
            return !string.IsNullOrWhiteSpace(def.Code);
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new InvalidOperationException("Test catalog is read-only.");

        public IReadOnlyList<string> AllowedCodes()
            => _codes.Where(c => c.IsSelectable && !c.IsObservedExtension).Select(c => c.Code).ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => Array.Empty<string>();
    }
}
