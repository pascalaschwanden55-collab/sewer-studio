using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class GpuModelSelectionServiceTests
{
    [Fact]
    public void DetectAndSelect_waehlt_grosses_Modell_aus_Systemordner()
    {
        string? executable = null;
        IReadOnlyList<string>? arguments = null;
        TimeSpan timeout = default;
        var service = new GpuModelSelectionService(
            path => path == Path.Combine(@"C:\Windows\System32", "nvidia-smi.exe"),
            FolderPath,
            (fileName, processArguments, processTimeout) =>
            {
                executable = fileName;
                arguments = processArguments;
                timeout = processTimeout;
                return Success("32768, NVIDIA GeForce RTX 5090");
            });

        var profile = service.DetectAndSelect();

        Assert.NotNull(profile);
        Assert.Equal(GpuModelSelector.LargeModel, profile.ResolvedModel);
        Assert.Equal(GpuModelSelector.LargeModelNumCtx, profile.ResolvedNumCtx);
        Assert.Equal(32768, profile.VramTotalMb);
        Assert.Equal("NVIDIA GeForce RTX 5090", profile.GpuName);
        Assert.Equal(Path.Combine(@"C:\Windows\System32", "nvidia-smi.exe"), executable);
        Assert.Equal(
            ["--query-gpu=memory.total,name", "--format=csv,noheader,nounits"],
            arguments);
        Assert.Equal(TimeSpan.FromSeconds(5), timeout);
    }

    [Fact]
    public void DetectAndSelect_verwendet_PATH_und_kleines_Modell()
    {
        var calls = new List<(string FileName, IReadOnlyList<string> Arguments, TimeSpan Timeout)>();
        var service = new GpuModelSelectionService(
            _ => false,
            FolderPath,
            (fileName, arguments, timeout) =>
            {
                calls.Add((fileName, arguments, timeout));
                return arguments.Contains("--version")
                    ? Success("NVIDIA-SMI")
                    : Success("12000, NVIDIA RTX");
            });

        var profile = service.DetectAndSelect();

        Assert.NotNull(profile);
        Assert.Equal(GpuModelSelector.SmallModel, profile.ResolvedModel);
        Assert.Equal(12000, profile.VramTotalMb);
        Assert.Collection(
            calls,
            call =>
            {
                Assert.Equal("nvidia-smi", call.FileName);
                Assert.Equal(["--version"], call.Arguments);
                Assert.Equal(TimeSpan.FromSeconds(3), call.Timeout);
            },
            call =>
            {
                Assert.Equal("nvidia-smi", call.FileName);
                Assert.Equal(TimeSpan.FromSeconds(5), call.Timeout);
            });
    }

    [Fact]
    public void DetectAndSelect_faellt_ohne_nvidia_smi_auf_kleines_Modell_zurueck()
    {
        var service = new GpuModelSelectionService(
            _ => false,
            FolderPath,
            (_, _, _) => new ExternalProcessRunResult(
                false,
                null,
                false,
                string.Empty,
                string.Empty,
                "nicht gefunden"));

        var profile = service.DetectAndSelect();

        Assert.NotNull(profile);
        Assert.Equal(GpuModelSelector.SmallModel, profile.ResolvedModel);
        Assert.Equal(GpuModelSelector.SmallModelNumCtx, profile.ResolvedNumCtx);
        Assert.Equal(0, profile.VramTotalMb);
    }

    private static string FolderPath(Environment.SpecialFolder folder) => folder switch
    {
        Environment.SpecialFolder.System => @"C:\Windows\System32",
        Environment.SpecialFolder.ProgramFiles => @"C:\Program Files",
        _ => throw new ArgumentOutOfRangeException(nameof(folder))
    };

    private static ExternalProcessRunResult Success(string output) =>
        new(true, 0, false, output, string.Empty, null);
}
