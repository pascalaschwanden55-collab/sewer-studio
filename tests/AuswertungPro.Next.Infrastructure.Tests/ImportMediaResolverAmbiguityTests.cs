using System.Reflection;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ImportMediaResolverAmbiguityTests
{
    [Fact]
    public void IbakResolveFile_ReturnsNull_WhenFileNameIsAmbiguous()
    {
        var result = InvokeResolveFile(
            "AuswertungPro.Next.Infrastructure.Import.Ibak.IbakExportImportService",
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["film.mp4"] = new() { @"C:\a\film.mp4", @"C:\b\film.mp4" }
            },
            "film.mp4");

        Assert.Null(result);
    }

    [Fact]
    public void WinCanResolveFile_ReturnsNull_WhenFileNameIsAmbiguous()
    {
        var result = InvokeResolveFile(
            "AuswertungPro.Next.Infrastructure.Import.WinCan.WinCanDbImportService",
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["film.mp4"] = new() { @"C:\a\Video\film.mp4", @"C:\b\Video\film.mp4" }
            },
            "film.mp4");

        Assert.Null(result);
    }

    private static string? InvokeResolveFile(
        string typeName,
        Dictionary<string, List<string>> index,
        string fileName)
    {
        var type = Type.GetType(typeName + ", AuswertungPro.Next.Infrastructure");
        Assert.NotNull(type);
        var method = type!.GetMethod("ResolveFile", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (string?)method!.Invoke(null, new object?[] { index, fileName });
    }
}
