using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Collection("EnvironmentVars")]
public sealed class AppDataPathResolverTests
{
    [Fact]
    public void Resolve_uses_explicit_override()
    {
        using var scope = new AppDataEnvScope(@"C:\tmp\sewer-appdata");

        var path = AppDataPathResolver.Resolve();

        Assert.Equal(@"C:\tmp\sewer-appdata", path);
    }

    [Fact]
    public void Resolve_falls_back_to_local_appdata_product_folder()
    {
        using var scope = new AppDataEnvScope("  ");

        var path = AppDataPathResolver.Resolve();

        Assert.Equal(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SewerStudio"),
            path);
    }

    [Fact]
    public void Resolve_accepts_custom_product_name()
    {
        using var scope = new AppDataEnvScope(null);

        var path = AppDataPathResolver.Resolve("CustomProduct");

        Assert.Equal(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CustomProduct"),
            path);
    }

    private sealed class AppDataEnvScope : IDisposable
    {
        private readonly string? _previous;

        public AppDataEnvScope(string? value)
        {
            _previous = Environment.GetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar);
            Environment.SetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar, _previous);
        }
    }
}
