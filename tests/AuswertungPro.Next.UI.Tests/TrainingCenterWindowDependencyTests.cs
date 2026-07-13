using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterWindowDependencyTests
{
    [Fact]
    public void Fenster_kann_zentrale_kernabhaengigkeiten_entgegennehmen()
    {
        var constructor = typeof(TrainingCenterWindow).GetConstructor(
        [
            typeof(ServiceProvider),
            typeof(IDialogService),
            typeof(TrainingCenterStore),
            typeof(TrainingCenterImportService),
            typeof(IKnowledgeBaseDiagnosticsRunner)
        ]);

        Assert.NotNull(constructor);
    }

    [Fact]
    public void ServiceProvider_stellt_store_und_importdienst_einmalig_bereit()
    {
        var store = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.TrainingCenterStore));
        var import = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.TrainingCenterImport));

        Assert.NotNull(store);
        Assert.Equal(typeof(TrainingCenterStore), store.PropertyType);
        Assert.False(store.CanWrite);
        Assert.NotNull(import);
        Assert.Equal(typeof(TrainingCenterImportService), import.PropertyType);
        Assert.False(import.CanWrite);
    }
}
