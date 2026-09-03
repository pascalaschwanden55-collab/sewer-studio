using System;
using System.IO;
using System.Xml.Linq;
using AuswertungPro.Next.UI;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AppIdentityVersion45Tests
{
    [Fact]
    public void AppIdentity_verwendet_version_45_als_zentrale_versionsquelle()
    {
        Assert.Equal("4.5", AppIdentity.Version);
        Assert.Equal("v4.5", AppIdentity.DisplayVersion);
    }

    [Fact]
    public void UiProjekt_setzt_assembly_und_fileversion_auf_45()
    {
        var project = XDocument.Load(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "AuswertungPro.Next.UI.csproj"));

        var propertyValues = project.Root!
            .Elements("PropertyGroup")
            .Elements()
            .ToDictionary(element => element.Name.LocalName, element => element.Value, StringComparer.Ordinal);

        Assert.Equal("4.5.0", propertyValues["Version"]);
        Assert.Equal("4.5.0.0", propertyValues["FileVersion"]);
        Assert.Equal("4.5.0.0", propertyValues["AssemblyVersion"]);
    }

    [Fact]
    public void StartupSplash_zeigt_keine_versionsangabe_und_bleibt_bei_vsa_kek_2020()
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

        // Entscheid 2026-09-03: Der Startbildschirm traegt keine Versionsnummer mehr.
        // Die Version bleibt in AppIdentity und den Einstellungen; der Splash liest sie nicht.
        Assert.DoesNotContain("AppIdentity", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AppIdentity", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("VersionText", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("v4.", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("v4.", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("VSA-KEK 2023", codeBehind, StringComparison.Ordinal);
        Assert.Contains("VSA-KEK 2020", xaml, StringComparison.Ordinal);
        Assert.Contains("VSA-KEK 2020", codeBehind, StringComparison.Ordinal);
    }
}
