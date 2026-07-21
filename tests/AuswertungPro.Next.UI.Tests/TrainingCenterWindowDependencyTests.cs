using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Diagnostics;
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
        Assert.Contains("_trainingServices.GetReviewSamWorkflow().ExecuteAsync", source);
        Assert.DoesNotContain("File.Exists(card.FramePath)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateTrainingSegmentationMask", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildSamStatus", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppSettingsAiSettingsProvider", source, StringComparison.Ordinal);
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
            typeof(Func<TrainingReviewSamSegmentationService>)
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
        var samples = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.TrainingSamples));
        var frames = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.TrainingFrames));
        var previewFrames = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.TrainingPreviewFrames));
        var review = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.TrainingReviewQueue));
        var samFactory = typeof(ServiceProvider).GetMethod(nameof(ServiceProvider.CreateTrainingReviewSam));

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
        Assert.NotNull(samples);
        Assert.Equal(typeof(ITrainingSampleStore), samples.PropertyType);
        Assert.False(samples.CanWrite);
        Assert.NotNull(frames);
        Assert.Equal(typeof(ITrainingFrameStore), frames.PropertyType);
        Assert.False(frames.CanWrite);
        Assert.NotNull(previewFrames);
        Assert.Equal(typeof(ITrainingPreviewFrameExtractor), previewFrames.PropertyType);
        Assert.False(previewFrames.CanWrite);
        Assert.NotNull(review);
        Assert.Equal(typeof(ReviewQueueService), review.PropertyType);
        Assert.False(review.CanWrite);
        Assert.Equal(typeof(TrainingReviewSamSegmentationService), samFactory?.ReturnType);
    }

    [Fact]
    public void Fensterpaket_verwendet_die_zentral_registrierten_Dienste()
    {
        using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        var dependencies = TrainingCenterWindowDependencyFactory.Create(services);

        Assert.Same(services.Dialogs, dependencies.Dialogs);
        Assert.Same(services.TrainingCenterStore, dependencies.Store);
        Assert.Same(services.TrainingCenterImport, dependencies.Import);
        Assert.Same(services.KnowledgeBaseDiagnostics, dependencies.KnowledgeBaseDiagnostics);
        Assert.Same(services.TrainingReviewQueue, dependencies.CreateReviewQueue());
    }

    [Fact]
    public void LazyDienste_werden_erst_bei_Bedarf_erzeugt_und_sinnvoll_wiederverwendet()
    {
        var reviewCount = 0;
        var samCount = 0;
        var diameterCount = 0;
        var review = new ReviewQueueService();
        var sam = new TrainingReviewSamSegmentationService(new UnusedSamClient());
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
            resolveReviewPipeDiameterMm: () =>
            {
                diameterCount++;
                return 300;
            });

        Assert.Equal(0, reviewCount);
        Assert.Equal(0, samCount);
        Assert.Equal(0, diameterCount);

        Assert.Same(review, services.GetReviewQueue());
        Assert.Same(review, services.GetReviewQueue());
        Assert.Same(services.GetReviewSamWorkflow(), services.GetReviewSamWorkflow());
        Assert.Equal(0, samCount);
        Assert.Equal(0, diameterCount);
        Assert.Same(sam, services.GetReviewSam());
        Assert.Same(sam, services.GetReviewSam());

        Assert.Equal(1, reviewCount);
        Assert.Equal(1, samCount);
        Assert.Equal(0, diameterCount);
    }

    [Fact]
    public void Alter_Bild_FewShot_Weg_bleibt_entfernt_aber_in_der_Sicherung_erhalten()
    {
        Assert.False(File.Exists(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Ai", "Training", "FewShotExampleStore.cs")));
        Assert.False(File.Exists(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Ai", "Training", "FewShotExampleBuilder.cs")));
        Assert.False(File.Exists(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Ai", "Training", "FewShotExampleClassifier.cs")));

        var xaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingCenterWindow.xaml"));
        Assert.DoesNotContain("BtnTeacherAddFewShot", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Zu FewShot", xaml, StringComparison.Ordinal);

        var backupCatalog = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Services", "KnowledgeBackupFileCatalog.cs"));
        Assert.Contains("\"fewshot_examples.json\"", backupCatalog, StringComparison.Ordinal);
        Assert.Contains("\"fewshot_images\"", backupCatalog, StringComparison.Ordinal);
    }

    private sealed class UnusedSamClient : ITrainingReviewSamClient
    {
        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
            => throw new InvalidOperationException("Der Client wird in diesem Lebensdauertest nicht aufgerufen.");
    }
}
