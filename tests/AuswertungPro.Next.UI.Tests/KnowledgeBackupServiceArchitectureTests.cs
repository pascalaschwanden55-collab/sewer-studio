using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.ViewModels.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KnowledgeBackupServiceArchitectureTests
{
    [Fact]
    public void Zentraler_Dienst_und_Einstellungen_verwenden_den_Instanzvertrag()
    {
        Assert.Equal(
            typeof(IKnowledgeBackupService),
            typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.KnowledgeBackup))?.PropertyType);

        var fieldTypes = typeof(SettingsPageViewModel)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(field => field.FieldType)
            .ToArray();
        Assert.Contains(typeof(IKnowledgeBackupService), fieldTypes);

        var trainingFieldTypes = typeof(TrainingCenterViewModel)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(field => field.FieldType)
            .ToArray();
        Assert.Contains(typeof(IKnowledgeBackupService), trainingFieldTypes);
    }

    [Fact]
    public void Statische_Fassade_enthaelt_keine_Export_oder_Import_Dateilogik_mehr()
    {
        var source = File.ReadAllText(TestRepoPaths.RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Services",
            "KnowledgeBackupFacade.cs"));

        Assert.DoesNotContain("ZipFile.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FileStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.Contains("KnowledgeBackupTransferService", source, StringComparison.Ordinal);
    }
}
