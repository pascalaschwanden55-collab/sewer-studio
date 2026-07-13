using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Tests;

[CollectionDefinition("EnvironmentVars", DisableParallelization = true)]
public sealed class EnvironmentVarsCollection;

[Collection("EnvironmentVars")]
public sealed class KnowledgeBasePathsTests
{
    [Fact]
    public void GetRoot_uses_persisted_settings_when_environment_override_is_missing()
    {
        var previousRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT");
        var settingsRoot = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"), "SettingsKnowledge");
        Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", null);
        KnowledgeBasePaths.ConfigureSettingsRoot(settingsRoot);

        try
        {
            Assert.Equal(settingsRoot, KnowledgeBasePaths.GetRoot());
            var resolution = KnowledgeBasePaths.GetResolution();
            Assert.Equal(KnowledgeBasePaths.RootSource.PersistedSettings, resolution.Source);
            Assert.Equal(settingsRoot, resolution.PersistedSettingsRoot);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", previousRoot);
            KnowledgeBasePaths.ConfigureSettingsRoot(null);
            if (Directory.Exists(settingsRoot))
                Directory.Delete(settingsRoot, recursive: true);
        }
    }

    [Fact]
    public void GetRoot_environment_override_wins_over_persisted_settings()
    {
        var previousRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT");
        var baseRoot = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        var envRoot = Path.Combine(baseRoot, "Environment");
        var settingsRoot = Path.Combine(baseRoot, "Settings");
        Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", envRoot);
        KnowledgeBasePaths.ConfigureSettingsRoot(settingsRoot);

        try
        {
            Assert.Equal(envRoot, KnowledgeBasePaths.GetRoot());
            var resolution = KnowledgeBasePaths.GetResolution();
            Assert.Equal(KnowledgeBasePaths.RootSource.EnvironmentOverride, resolution.Source);
            Assert.True(resolution.HasEnvironmentSettingsMismatch);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", previousRoot);
            KnowledgeBasePaths.ConfigureSettingsRoot(null);
            if (Directory.Exists(baseRoot))
                Directory.Delete(baseRoot, recursive: true);
        }
    }

    [Fact]
    public void GetRoot_ignores_relative_environment_override_and_reports_warning()
    {
        var previousRoot = Environment.GetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName);
        var settingsRoot = Path.Combine(
            Path.GetTempPath(),
            "AuswertungPro.Next.Tests",
            Guid.NewGuid().ToString("N"),
            "SettingsKnowledge");
        var relativeRoot = Path.Combine("relative-knowledge", Guid.NewGuid().ToString("N"));
        var accidentallyCreatedRoot = Path.GetFullPath(relativeRoot);
        var warnings = new List<string>();
        Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, relativeRoot);
        KnowledgeBasePaths.ConfigureSettingsRoot(settingsRoot);
        BestEffort.ConfigureDefaultErrorSink(warnings.Add);

        try
        {
            Assert.Equal(settingsRoot, KnowledgeBasePaths.GetRoot());
            Assert.Equal(
                KnowledgeBasePaths.RootSource.PersistedSettings,
                KnowledgeBasePaths.GetResolution().Source);
            Assert.Contains(
                warnings,
                warning => warning.Contains(KnowledgeBasePaths.EnvironmentVariableName, StringComparison.Ordinal)
                           && warning.Contains("ignoriert", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            BestEffort.ConfigureDefaultErrorSink(null);
            Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, previousRoot);
            KnowledgeBasePaths.ConfigureSettingsRoot(null);
            if (Directory.Exists(settingsRoot))
                Directory.Delete(settingsRoot, recursive: true);
            if (Directory.Exists(accidentallyCreatedRoot))
                Directory.Delete(accidentallyCreatedRoot, recursive: true);
        }
    }

    [Fact]
    public void GetResolution_does_not_report_mismatch_for_equivalent_paths()
    {
        var previousRoot = Environment.GetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName);
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, root + Path.DirectorySeparatorChar);
        KnowledgeBasePaths.ConfigureSettingsRoot(root);

        try
        {
            var resolution = KnowledgeBasePaths.GetResolution();

            Assert.Equal(KnowledgeBasePaths.RootSource.EnvironmentOverride, resolution.Source);
            Assert.False(resolution.HasEnvironmentSettingsMismatch);
        }
        finally
        {
            Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, previousRoot);
            KnowledgeBasePaths.ConfigureSettingsRoot(null);
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Active_resolution_stays_stable_until_cache_is_explicitly_invalidated()
    {
        var previousRoot = Environment.GetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName);
        var baseRoot = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        var firstRoot = Path.Combine(baseRoot, "First");
        var changedRoot = Path.Combine(baseRoot, "ChangedAfterStartup");
        Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, firstRoot);
        KnowledgeBasePaths.ConfigureSettingsRoot(null);

        try
        {
            Assert.Equal(firstRoot, KnowledgeBasePaths.GetRoot());

            Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, changedRoot);

            Assert.Equal(firstRoot, KnowledgeBasePaths.GetRoot());
            Assert.Equal(firstRoot, KnowledgeBasePaths.GetResolution().Root);
        }
        finally
        {
            Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, previousRoot);
            KnowledgeBasePaths.ConfigureSettingsRoot(null);
            if (Directory.Exists(baseRoot))
                Directory.Delete(baseRoot, recursive: true);
        }
    }

    [Fact]
    public void GetRoot_defaults_to_local_appdata_knowledge_not_build_output()
    {
        var previousRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT");
        var previousAppData = Environment.GetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR");
        var appDataRoot = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));

        Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", null);
        Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", appDataRoot);
        KnowledgeBasePaths.ConfigureSettingsRoot(null);

        try
        {
            var root = KnowledgeBasePaths.GetRoot();

            Assert.Equal(Path.Combine(appDataRoot, "Knowledge"), root);
            Assert.Equal(
                KnowledgeBasePaths.RootSource.DefaultFallback,
                KnowledgeBasePaths.GetResolution().Source);
            Assert.False(
                root.StartsWith(AppDomain.CurrentDomain.BaseDirectory, StringComparison.OrdinalIgnoreCase),
                $"Knowledge root must not live under build output: {root}");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", previousRoot);
            Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", previousAppData);
            KnowledgeBasePaths.ConfigureSettingsRoot(null);
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
            Assert.False(
                Directory.Exists(Path.Combine(explicitRoot, "frames")),
                "Explicit knowledge roots must not copy legacy frame archives into temp/test roots.");
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
