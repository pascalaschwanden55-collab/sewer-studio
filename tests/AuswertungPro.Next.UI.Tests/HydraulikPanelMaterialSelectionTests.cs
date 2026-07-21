using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Hydraulik;
using AuswertungPro.Next.UI.ViewModels.Windows;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class HydraulikPanelMaterialSelectionTests : IDisposable
{
    [Fact]
    public void LoadFromRecord_ordnet_PVC_der_Kunststoffgruppe_zu()
    {
        var viewModel = CreateViewModel("Beton");

        viewModel.LoadFromRecord(dn: null, material: "PVC", wasserstand: null);

        Assert.Equal("PVC/PE", viewModel.SelectedMaterial.Key);
    }

    [Fact]
    public void LoadFromRecord_findet_Materialschluessel_ohne_Beachtung_der_Grossschreibung()
    {
        var viewModel = CreateViewModel("Beton");

        viewModel.LoadFromRecord(dn: null, material: "gfk", wasserstand: null);

        Assert.Equal("GFK", viewModel.SelectedMaterial.Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unbekannt")]
    public void LoadFromRecord_behaelt_bei_leerem_oder_unbekanntem_Material_die_aktuelle_Auswahl(string? material)
    {
        var viewModel = CreateViewModel("Steinzeug");
        var selectedBefore = viewModel.SelectedMaterial;

        viewModel.LoadFromRecord(dn: null, material, wasserstand: null);

        Assert.Same(selectedBefore, viewModel.SelectedMaterial);
    }

    [Fact]
    public void LoadFromRecord_behaelt_bei_unbekanntem_Wert_auch_eine_benutzerdefinierte_Auswahl()
    {
        var viewModel = CreateViewModel("Beton", out var settingsStore);
        var custom = new MaterialOption("Sonder", "Sondermaterial", 0.0004, 0.0009);
        viewModel.SelectedMaterial = custom;
        AppSettings.FlushPendingSave();
        settingsStore.Reset();

        viewModel.LoadFromRecord(dn: null, material: "Unbekannt", wasserstand: null);
        AppSettings.FlushPendingSave();

        Assert.Same(custom, viewModel.SelectedMaterial);
        Assert.Equal(0, settingsStore.Calls);
    }

    [Fact]
    public void LoadFromRecord_kann_aus_null_Auswahl_ein_bekanntes_Material_wiederherstellen()
    {
        var viewModel = CreateViewModel("Beton");
        viewModel.SelectedMaterial = null!;

        viewModel.LoadFromRecord(dn: null, material: "PVC", wasserstand: null);

        Assert.Equal("PVC/PE", viewModel.SelectedMaterial.Key);
    }

    [Fact]
    public void LoadFromRecord_belaesst_null_Auswahl_bei_unbekanntem_Material()
    {
        var viewModel = CreateViewModel("Beton");
        viewModel.SelectedMaterial = null!;

        viewModel.LoadFromRecord(dn: null, material: "Unbekannt", wasserstand: null);

        Assert.Null(viewModel.SelectedMaterial);
    }

    [Fact]
    public void LoadFromRecord_speichert_bei_echtem_Materialwechsel_genau_einmal()
    {
        var viewModel = CreateViewModel("Beton", out var settingsStore);
        AppSettings.FlushPendingSave();
        settingsStore.Reset();

        viewModel.LoadFromRecord(dn: null, material: "PVC", wasserstand: null);
        AppSettings.FlushPendingSave();

        Assert.Equal("PVC/PE", viewModel.SelectedMaterial.Key);
        Assert.Equal(1, settingsStore.Calls);
    }

    [Fact]
    public void ViewModel_delegiert_Materialaufloesung_an_den_zentralen_Katalog()
    {
        var viewModelPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "HydraulikPanelViewModel.cs");
        var source = File.ReadAllText(viewModelPath);
        var start = source.IndexOf("public void LoadFromRecord", StringComparison.Ordinal);
        var end = source.IndexOf("partial void OnDnChanged", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var method = source[start..end];

        Assert.Contains("HydraulikMaterialCatalog.ResolveRecordMaterial(material, SelectedMaterial)", method);
        Assert.DoesNotContain("foreach", method);
        Assert.DoesNotContain("Label.Contains", method);
        Assert.DoesNotContain("Key.Equals", method);
    }

    public void Dispose()
        => AppSettings.FlushPendingSave();

    private static HydraulikPanelViewModel CreateViewModel(string materialKey)
        => CreateViewModel(materialKey, out _);

    private static HydraulikPanelViewModel CreateViewModel(
        string materialKey,
        out RecordingSettingsFileStore settingsStore)
    {
        var settings = new AppSettings
        {
            HydraulikPanel = new HydraulikPanelSettings
            {
                MaterialKey = materialKey
            }
        };
        settingsStore = new RecordingSettingsFileStore();
        settings.UseSettingsFileStore(settingsStore);
        return new HydraulikPanelViewModel(settings);
    }

    private sealed class RecordingSettingsFileStore : ISettingsFileStore
    {
        public int Calls { get; private set; }

        public void Persist(
            string json,
            string settingsPath,
            string appDataDirectory,
            bool enableRestorePoints)
        {
            Calls++;
        }

        public void Reset() => Calls = 0;
    }
}
