using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ServiceProviderKnowledgeWalCheckpointDependencyTests
{
    [Fact]
    public void ServiceProvider_stellt_den_WAL_Pruefschritt_zentral_bereit()
    {
        var property = typeof(ServiceProvider)
            .GetProperty(nameof(ServiceProvider.KnowledgeWalCheckpoint));

        Assert.NotNull(property);
        Assert.Equal(typeof(IKnowledgeWalCheckpoint), property.PropertyType);
        Assert.False(property.CanWrite);
    }
}
