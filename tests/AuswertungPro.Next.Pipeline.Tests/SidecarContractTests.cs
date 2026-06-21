using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

public class SidecarContractTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    public static IEnumerable<object[]> SidecarContracts()
    {
        yield return Contract<YoloRequest>("sidecar/sidecar/schemas/detection.py", "YoloRequest");
        yield return Contract<YoloDetectionDto>("sidecar/sidecar/schemas/detection.py", "YoloDetection");
        yield return Contract<YoloResponse>("sidecar/sidecar/schemas/detection.py", "YoloResponse");
        yield return Contract<DinoRequest>("sidecar/sidecar/schemas/detection.py", "DinoRequest");
        yield return Contract<DinoDetectionDto>("sidecar/sidecar/schemas/detection.py", "DinoDetection");
        yield return Contract<DinoResponse>("sidecar/sidecar/schemas/detection.py", "DinoResponse");
        yield return Contract<SamBoundingBox>("sidecar/sidecar/schemas/detection.py", "BoundingBox");
        yield return Contract<YoloClassifyRequest>("sidecar/sidecar/schemas/detection.py", "YoloClassifyRequest");
        yield return Contract<YoloClassifyPrediction>("sidecar/sidecar/schemas/detection.py", "YoloClassifyPrediction");
        yield return Contract<YoloClassifyResponse>("sidecar/sidecar/schemas/detection.py", "YoloClassifyResponse");
        yield return Contract<SamRequest>("sidecar/sidecar/schemas/segmentation.py", "SamRequest");
        yield return Contract<SamMaskResult>("sidecar/sidecar/schemas/segmentation.py", "MaskResult");
        yield return Contract<SamResponse>("sidecar/sidecar/schemas/segmentation.py", "SamResponse");
        yield return Contract<TrainingExportSample>("sidecar/sidecar/schemas/segmentation.py", "TrainingSample");
        yield return Contract<TrainingExportRequestDto>("sidecar/sidecar/schemas/segmentation.py", "TrainingExportRequest");
        yield return Contract<TrainingExportResponseDto>("sidecar/sidecar/schemas/segmentation.py", "TrainingExportResponse");
    }

    [Theory]
    [MemberData(nameof(SidecarContracts))]
    public void CSharpDto_JsonFields_MatchPythonPydanticSchema(
        Type csharpType,
        string pythonSchemaPath,
        string pythonClassName)
    {
        var csharpFields = JsonFieldNames(csharpType);
        var pythonFields = PythonPydanticFields(pythonSchemaPath, pythonClassName);

        Assert.Equal(csharpFields, pythonFields);
    }

    private static object[] Contract<T>(string pythonSchemaPath, string pythonClassName)
        => [typeof(T), pythonSchemaPath, pythonClassName];

    private static IReadOnlyList<string> JsonFieldNames(Type type)
        => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property =>
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> PythonPydanticFields(string relativePath, string className)
    {
        var fullPath = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var source = File.ReadAllText(fullPath);
        var classMatch = Regex.Match(
            source,
            $@"(?ms)^class\s+{Regex.Escape(className)}\(BaseModel\):(?<body>.*?)(?=^class\s+\w+\(BaseModel\):|\z)");

        Assert.True(classMatch.Success, $"Python schema class not found: {className} in {relativePath}");

        return Regex.Matches(
                classMatch.Groups["body"].Value,
                @"^\s{4}([a-zA-Z_][a-zA-Z0-9_]*)\s*:",
                RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git"))
                && Directory.Exists(Path.Combine(dir.FullName, "sidecar")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
