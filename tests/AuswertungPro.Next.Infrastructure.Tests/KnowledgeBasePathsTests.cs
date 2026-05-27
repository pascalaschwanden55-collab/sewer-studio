using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Tests;

[CollectionDefinition("EnvironmentVars", DisableParallelization = true)]
public sealed class EnvironmentVarsCollection;

[Collection("EnvironmentVars")]
public sealed class KnowledgeBasePathsTests
{
    [Fact]
    public void GetRoot_defaults_to_local_appdata_knowledge_not_build_output()
    {
        var previousRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT");
        var previousAppData = Environment.GetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR");
        var appDataRoot = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));

        Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", null);
        Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", appDataRoot);
        KnowledgeBasePaths.InvalidateCache();

        try
        {
            var root = KnowledgeBasePaths.GetRoot();

            Assert.Equal(Path.Combine(appDataRoot, "Knowledge"), root);
            Assert.False(
                root.StartsWith(AppDomain.CurrentDomain.BaseDirectory, StringComparison.OrdinalIgnoreCase),
                $"Knowledge root must not live under build output: {root}");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", previousRoot);
            Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", previousAppData);
            KnowledgeBasePaths.InvalidateCache();
            if (Directory.Exists(appDataRoot))
                Directory.Delete(appDataRoot, recursive: true);
        }
    }

    [Fact]
    public void GetRoot_keeps_explicit_knowledge_root_override()
    {
        var previousRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT");
        var previousAppData = Environment.GetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR");
        var explicitRoot = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"), "ExplicitKnowledge");
        var appDataRoot = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"), "AppData");

        Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", explicitRoot);
        Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", appDataRoot);
        KnowledgeBasePaths.InvalidateCache();

        try
        {
            Assert.Equal(explicitRoot, KnowledgeBasePaths.GetRoot());
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", previousRoot);
            Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", previousAppData);
            KnowledgeBasePaths.InvalidateCache();
            if (Directory.Exists(explicitRoot))
                Directory.Delete(explicitRoot, recursive: true);
            if (Directory.Exists(appDataRoot))
                Directory.Delete(appDataRoot, recursive: true);
        }
    }
}
