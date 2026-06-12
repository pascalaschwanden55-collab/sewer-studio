using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class TemplateStoreLoadGuardTests
{
    [Fact]
    public void MeasureTemplateSave_IsBlocked_WhenExistingUserOverrideCouldNotBeLoaded()
    {
        using var temp = new TempDir();
        var overridePath = Path.Combine(temp.Path, "measure_templates.user.json");
        File.WriteAllText(overridePath, "{ kaputt");
        var store = new MeasureTemplateStore(overridePath);

        _ = store.LoadUserOverrides();
        var ok = store.SaveUserOverrides(new MeasureTemplateCatalog(), out var error);

        Assert.False(ok);
        Assert.Contains("konnte nicht geladen werden", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PositionTemplateSave_IsBlocked_WhenExistingUserOverrideCouldNotBeLoaded()
    {
        using var temp = new TempDir();
        var overridePath = Path.Combine(temp.Path, "position_templates.user.json");
        File.WriteAllText(overridePath, "{ kaputt");
        var store = new PositionTemplateStore(overridePath);

        _ = store.LoadMerged(null);
        var ok = store.SaveUserOverride(new PositionTemplateCatalog(), out var error);

        Assert.False(ok);
        Assert.Contains("konnte nicht geladen werden", error, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "template_store_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }
}
