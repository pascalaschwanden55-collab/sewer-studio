using System;
using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsRestorePointArchitectureTests
{
    [Fact]
    public void SettingsRestorePointsUseCentralInstanceAndKeepStaticFacadeThin()
    {
        var root = TestRepoPaths.FindRepositoryRoot();
        var provider = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "ServiceProvider.cs"));
        var facade = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Services",
            "RestorePointService.cs"));
        var store = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.Infrastructure",
            "Settings",
            "SettingsRestorePointStore.cs"));

        Assert.Contains(
            "public ISettingsRestorePointStore SettingsRestorePoints",
            provider);
        Assert.Contains(
            "SettingsRestorePoints = new SettingsRestorePointStore()",
            provider);
        Assert.Contains(
            "SettingsStore.CreateDefault(SettingsRestorePoints)",
            provider);
        Assert.Contains(
            "private static readonly ISettingsRestorePointStore DefaultStore",
            facade);
        Assert.DoesNotContain("File.Copy", facade);
        Assert.Contains(
            "public sealed class SettingsRestorePointStore : ISettingsRestorePointStore",
            store);
    }

}
