using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Infrastructure.Projects;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectPhotoReferenceNormalizerDependencyTests
{
    [Fact]
    public void Projektablage_verwendet_den_zentralen_Foto_Normalisierer()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var repository = Assert.IsType<JsonProjectRepository>(services.Projects);
        var normalizerField = typeof(JsonProjectRepository).GetField(
            "_photoReferenceNormalizer",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(normalizerField);
        Assert.IsType<ProjectPhotoReferenceNormalizationService>(services.ProjectPhotoReferences);
        Assert.Same(services.ProjectPhotoReferences, normalizerField!.GetValue(repository));
        Assert.Same(
            services.ProjectPhotoReferences,
            services.GetService(typeof(IProjectPhotoReferenceNormalizer)));
    }
}
