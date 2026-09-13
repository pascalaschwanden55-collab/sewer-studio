using System;
using System.IO;
using System.Xml.Linq;
using AuswertungPro.Next.UI;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AppIdentityVersion50Tests
{
    [Fact]
    public void AppIdentity_verwendet_version_50_als_zentrale_versionsquelle()
    {
        Assert.Equal("5.0", AppIdentity.Version);
        Assert.Equal("v5.0", AppIdentity.DisplayVersion);
    }

    [Fact]
    public void UiProjekt_setzt_assembly_und_fileversion_auf_50()
    {
        var project = XDocument.Load(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "AuswertungPro.Next.UI.csproj"));

        var propertyValues = project.Root!
            .Elements("PropertyGroup")
            .Elements()
            .ToDictionary(element => element.Name.LocalName, element => element.Value, StringComparer.Ordinal);

        Assert.Equal("5.0.0", propertyValues["Version"]);
        Assert.Equal("5.0.0.0", propertyValues["FileVersion"]);
        Assert.Equal("5.0.0.0", propertyValues["AssemblyVersion"]);
    }

    [Fact]
    public void StartupSplash_zeigt_die_version_aus_AppIdentity_und_bleibt_bei_vsa_kek_2020()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "StartupSplashWindow.xaml"));
        var codeBehind = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "StartupSplashWindow.xaml.cs"));

        // Entscheid Pascal 13.09.2026 (hebt den Entscheid vom 03.09. auf): Der Startbildschirm zeigt
        // die Version wieder - aber nur aus AppIdentity, nie als zweite, hart geschriebene Zahl.
        Assert.Contains("AppIdentity.DisplayVersion", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AppIdentity", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("v4.", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("v5.", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("v4.", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("v5.", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("VSA-KEK 2023", codeBehind, StringComparison.Ordinal);
        Assert.Contains("VSA-KEK 2020", xaml, StringComparison.Ordinal);
        Assert.Contains("VSA-KEK 2020", codeBehind, StringComparison.Ordinal);
    }
}
