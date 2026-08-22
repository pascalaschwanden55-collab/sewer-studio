using System.Net.Http;
using System.Runtime.CompilerServices;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterViewModelLifetimeTests
{
    [Fact]
    public void Dispose_gibt_den_eigenen_KbHttpClient_genau_einmal_frei()
    {
        var handler = new TrackingHandler();
        var client = new HttpClient(handler);
        var viewModel = (TrainingCenterViewModel)RuntimeHelpers.GetUninitializedObject(
            typeof(TrainingCenterViewModel));
        var field = typeof(TrainingCenterViewModel).GetField(
            "_kbHttpClient",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(viewModel, client);

        viewModel.Dispose();
        viewModel.Dispose();

        Assert.Equal(1, handler.DisposeCount);
        Assert.Null(field.GetValue(viewModel));
    }

    private sealed class TrackingHandler : HttpMessageHandler
    {
        public int DisposeCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCount++;
            base.Dispose(disposing);
        }
    }
}
