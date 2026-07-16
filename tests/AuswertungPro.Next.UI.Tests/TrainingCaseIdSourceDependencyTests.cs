using System.IO;
using System.Reflection;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCaseIdSourceDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_die_Trainingsfall_Quelle()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<TrainingCaseIdSource>(services.TrainingCases);
        Assert.Same(services.TrainingCases, services.GetService(typeof(ITrainingCaseIdSource)));
    }

    [Fact]
    public void DataPageViewModel_haelt_nur_den_Application_Vertrag()
    {
        var field = typeof(DataPageViewModel).GetField(
            "_trainingCases",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(typeof(ITrainingCaseIdSource), field!.FieldType);
    }

    [Fact]
    public async Task Quelle_liefert_die_Fall_Ids_aus_dem_TrainingCenterStore()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"training-case-source-{Guid.NewGuid():N}");
        var storePath = Path.Combine(testRoot, "training-center.json");

        try
        {
            var store = new TrainingCenterStore(storePath);
            await store.SaveAsync(new TrainingCenterState
            {
                Cases =
                [
                    new TrainingCase { CaseId = "Fall-1" },
                    new TrainingCase { CaseId = "Fall-2" },
                ],
            });

            ITrainingCaseIdSource source = new TrainingCaseIdSource(store);

            var ids = await source.LoadCaseIdsAsync();

            Assert.Equal(["Fall-1", "Fall-2"], ids);
        }
        finally
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }
}
