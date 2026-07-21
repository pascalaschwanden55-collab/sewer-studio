using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectPageDropdownViewModelTests
{
    [Fact]
    public void Sanieren_Reset_ueber_oeffentlichen_Command_speichert_beide_Listen()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings(),
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var store = new RecordingDropdownOptionsStore();
        using var viewModel = new ProjectPageViewModel(shell, dropdownOptions: store);

        viewModel.ResetSanierenOptionsCommand.Execute(null);

        Assert.Equal(["Nein", "Ja"], viewModel.SanierenOptions);
        Assert.Equal(1, store.SaveSanierenCalls);
        Assert.Equal(1, store.SaveEigentuemerCalls);
        Assert.Equal(store.FixedEigentuemerOptions, store.LastSavedEigentuemer);
    }

    private sealed class RecordingDropdownOptionsStore : IDropdownOptionsStore
    {
        public IReadOnlyList<string> FixedEigentuemerOptions { get; }
            = ["Kanton", "Bund", "AWU", "Gemeinde", "Privat"];

        public int SaveSanierenCalls { get; private set; }
        public int SaveEigentuemerCalls { get; private set; }
        public IReadOnlyList<string> LastSavedEigentuemer { get; private set; } = [];

        public DropdownOptionsModel LoadOrDefault()
            => new()
            {
                SanierenOptions = LoadSanierenOptions(),
                EigentuemerOptions = LoadEigentuemerOptions()
            };

        public void Save(DropdownOptionsModel model)
        {
            SaveSanierenOptions(model.SanierenOptions);
            SaveEigentuemerOptions(model.EigentuemerOptions);
        }

        public List<string> LoadSanierenOptions() => ["Alt"];

        public void SaveSanierenOptions(IEnumerable<string> options)
        {
            _ = options.ToArray();
            SaveSanierenCalls++;
        }

        public List<string> LoadEigentuemerOptions() => [.. FixedEigentuemerOptions];

        public void SaveEigentuemerOptions(IEnumerable<string> options)
        {
            LastSavedEigentuemer = options.ToArray();
            SaveEigentuemerCalls++;
        }

        public List<string> LoadPruefungsresultatOptions() => [""];
        public void SavePruefungsresultatOptions(IEnumerable<string> options) => _ = options;
        public List<string> LoadReferenzpruefungOptions() => [""];
        public void SaveReferenzpruefungOptions(IEnumerable<string> options) => _ = options;
        public List<string> LoadEmpfohleneSanierungsmassnahmenOptions() => [""];
        public void SaveEmpfohleneSanierungsmassnahmenOptions(IEnumerable<string> options) => _ = options;
    }
}
