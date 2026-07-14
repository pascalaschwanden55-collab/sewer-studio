using System.Reflection;
using AuswertungPro.Next.UI.Mapping;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Mapsui.Layers;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KarteBasemapLayerDependencyTests
{
    [Fact]
    public void ServiceProvider_und_statische_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.BasemapLayers, KarteBasemapLayerFactory.CompatibilityService);
        Assert.Same(
            services.BasemapLayers,
            services.GetService(typeof(IKarteBasemapLayerFactory)));
    }

    [Fact]
    public void Karten_ViewModel_haelt_den_injizierbaren_Layer_Vertrag()
    {
        var field = typeof(KarteViewModel).GetField(
            "_basemapLayers",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(typeof(IKarteBasemapLayerFactory), field!.FieldType);
    }

    [Fact]
    public async Task Kartenaufbau_verwendet_die_injizierte_Layer_Fabrik()
    {
        var factory = new BasemapLayerFactoryFake();
        var viewModel = new KarteViewModel(
            shell: null!,
            new AppSettings(),
            new NetworkFeatureCache(),
            playVideo: (_, _) => { },
            basemapLayers: factory);

        var map = await viewModel.BuildMapAsync();

        Assert.Equal(1, factory.SatellitCalls);
        Assert.Equal(1, factory.AvCalls);
        Assert.Equal(1, factory.OsmCalls);
        Assert.Contains(factory.Satellit, map.Layers);
        Assert.Contains(factory.Av, map.Layers);
        Assert.Contains(factory.Osm, map.Layers);
    }

    private sealed class BasemapLayerFactoryFake : IKarteBasemapLayerFactory
    {
        public ILayer Satellit { get; } = new MemoryLayer(KarteBasemapLayerFactory.SatellitLayerName);
        public ILayer Av { get; } = new MemoryLayer(KarteBasemapLayerFactory.AvLayerName);
        public ILayer Osm { get; } = new MemoryLayer(KarteBasemapLayerFactory.OsmLayerName);
        public int SatellitCalls { get; private set; }
        public int AvCalls { get; private set; }
        public int OsmCalls { get; private set; }

        public ILayer? CreateOfflineSatellit(string? basePath)
        {
            SatellitCalls++;
            return Satellit;
        }

        public ILayer? CreateOfflineAv(string? basePath)
        {
            AvCalls++;
            return Av;
        }

        public ILayer CreateOsmOnline()
        {
            OsmCalls++;
            return Osm;
        }
    }
}
