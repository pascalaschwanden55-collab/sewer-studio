using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Windows;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterWindowDependencyTests
{
    [Fact]
    public void Fenster_reicht_seinen_Lebensdauer_Abbruch_an_Sam_und_ViewModel_weiter()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingCenterWindow.xaml.cs"));

        Assert.Contains("private readonly TrainingCenterWindowLifetime _lifetime = new();", source);
        Assert.Contains("Closing += TrainingCenterWindow_Closing;", source);
        Assert.Contains("Vm.CancelOutstandingOperations();", source);
        Assert.Contains("_lifetime.Dispose();", source);
        Assert.Contains("catch (OperationCanceledException) when (ct.IsCancellationRequested)", source);
        Assert.Contains("ResolveReviewPipeDiameterMm(),\n                ct", source.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Fenster_kann_zentrale_kernabhaengigkeiten_entgegennehmen()
    {
        var compatibilityConstructor = typeof(TrainingCenterWindow).GetConstructor(
        [
            typeof(ServiceProvider),
            typeof(IDialogService),
            typeof(TrainingCenterStore),
            typeof(TrainingCenterImportService),
            typeof(IKnowledgeBaseDiagnosticsRunner)
        ]);
        var constructor = typeof(TrainingCenterWindow).GetConstructor(
        [
            typeof(ServiceProvider),
            typeof(IDialogService),
            typeof(TrainingCenterStore),
            typeof(TrainingCenterImportService),
            typeof(IKnowledgeBaseDiagnosticsRunner),
            typeof(Func<ReviewQueueService>),
            typeof(Func<TrainingReviewSamSegmentationService>),
            typeof(Func<FewShotExampleStore>)
        ]);

        Assert.NotNull(compatibilityConstructor);
        Assert.NotNull(constructor);
    }

    [Fact]
    public void ServiceProvider_stellt_alle_TrainingCenter_Dienste_zentral_bereit()
    {
        var store = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.TrainingCenterStore));
        var import = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.TrainingCenterImport));
        var settings = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.TrainingSettings));
        var history = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.SelfTrainingHistory));
        var annotations = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.TeacherAnnotations));
        var review = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.TrainingReviewQueue));
        var samFactory = typeof(ServiceProvider).GetMethod(nameof(ServiceProvider.CreateTrainingReviewSam));
        var fewShotFactory = typeof(ServiceProvider).GetMethod(nameof(ServiceProvider.CreateFewShotStore));

        Assert.NotNull(store);
        Assert.Equal(typeof(TrainingCenterStore), store.PropertyType);
        Assert.False(store.CanWrite);
        Assert.NotNull(import);
        Assert.Equal(typeof(TrainingCenterImportService), import.PropertyType);
        Assert.False(import.CanWrite);
        Assert.NotNull(settings);
        Assert.Equal(typeof(ITrainingCenterSettingsStore), settings.PropertyType);
        Assert.False(settings.CanWrite);
        Assert.NotNull(history);
        Assert.Equal(typeof(ISelfTrainingHistoryStore), history.PropertyType);
        Assert.False(history.CanWrite);
        Assert.NotNull(annotations);
        Assert.Equal(typeof(ITeacherAnnotationStore), annotations.PropertyType);
        Assert.False(annotations.CanWrite);
        Assert.NotNull(review);
        Assert.Equal(typeof(ReviewQueueService), review.PropertyType);
        Assert.False(review.CanWrite);
        Assert.Equal(typeof(TrainingReviewSamSegmentationService), samFactory?.ReturnType);
        Assert.Equal(typeof(FewShotExampleStore), fewShotFactory?.ReturnType);
    }

    [Fact]
    public void LazyDienste_werden_erst_bei_Bedarf_erzeugt_und_sinnvoll_wiederverwendet()
    {
        var reviewCount = 0;
        var samCount = 0;
        var fewShotCount = 0;
        var review = new ReviewQueueService();
        var sam = new TrainingReviewSamSegmentationService(new UnusedSamClient());
        var fewShot = new FewShotExampleStore();
        var services = new TrainingCenterLazyServices(
            createReviewQueue: () =>
            {
                reviewCount++;
                return review;
            },
            createReviewSam: () =>
            {
                samCount++;
                return sam;
            },
            createFewShotStore: () =>
            {
                fewShotCount++;
                return fewShot;
            });

        Assert.Equal(0, reviewCount);
        Assert.Equal(0, samCount);
        Assert.Equal(0, fewShotCount);

        Assert.Same(review, services.GetReviewQueue());
        Assert.Same(review, services.GetReviewQueue());
        Assert.Same(sam, services.GetReviewSam());
        Assert.Same(sam, services.GetReviewSam());
        Assert.Same(fewShot, services.CreateFewShotStore());
        Assert.Same(fewShot, services.CreateFewShotStore());

        Assert.Equal(1, reviewCount);
        Assert.Equal(1, samCount);
        Assert.Equal(2, fewShotCount);
    }

    private sealed class UnusedSamClient : ITrainingReviewSamClient
    {
        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
            => throw new InvalidOperationException("Der Client wird in diesem Lebensdauertest nicht aufgerufen.");
    }
}
