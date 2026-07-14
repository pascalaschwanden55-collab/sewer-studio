using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ServiceProviderPipelineTraceDependencyTests
{
    [Fact]
    public void ServiceProvider_stellt_Pipeline_Trace_zentral_bereit()
    {
        var property = typeof(ServiceProvider)
            .GetProperty(nameof(ServiceProvider.PipelineTrace));

        Assert.NotNull(property);
        Assert.Equal(typeof(IPipelineTraceWriter), property.PropertyType);
        Assert.False(property.CanWrite);
    }
}
