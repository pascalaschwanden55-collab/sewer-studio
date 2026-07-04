using System.IO;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class QgisPluginPackagingTests
{
    [Fact]
    public void Plugin_has_required_qgis_entry_points()
    {
        var init = ReadPluginFile("__init__.py");
        var metadata = ReadPluginFile("metadata.txt");

        Assert.Contains("classFactory", init);
        Assert.Contains("SewerStudioBridgePlugin", init);
        Assert.Contains("[general]", metadata);
        Assert.Contains("name=SewerStudio Bridge", metadata);
        Assert.Contains("qgisMinimumVersion=3.28", metadata);
    }

    [Fact]
    public void Metadata_is_versioned_for_qgis3_and_qgis4_restore()
    {
        var metadata = ReadPluginFile("metadata.txt");

        Assert.Contains("qgisMaximumVersion=4.99", metadata);
        Assert.DoesNotContain("supportsQt6", metadata);
    }

    [Fact]
    public void Plugin_knows_live_bridge_and_local_export_contract()
    {
        var source = ReadPluginFile("sewerstudio_bridge.py");

        Assert.Contains("QSettings", source);
        Assert.Contains(@"D:\QGIS_V4.03\Export_Sewer_Studio", source);
        Assert.Contains("/qgis/status.json", source);
        Assert.Contains("/qgis/current.geojson", source);
        Assert.Contains("/qgis/damages.geojson", source);
        Assert.Contains("/qgis/network.geojson", source);
        Assert.Contains("current_haltung.geojson", source);
        Assert.Contains("schaeden.geojson", source);
        Assert.Contains("netzbewertung.geojson", source);
        Assert.Contains("Haltungen", source);
        Assert.Contains("Schaechte", source);
    }

    [Fact]
    public void Plugin_refreshes_layers_in_place_instead_of_replacing()
    {
        var source = ReadPluginFile("sewerstudio_bridge.py");

        // Absturz-Schutz: Layer duerfen beim Poll NIE entfernt/neu angelegt werden.
        // removeMapLayer zerstoert das Layer-Objekt; offene Dialoge (Layer-Eigenschaften)
        // halten dann tote Zeiger -> Access Violation beim Schliessen (QGIS-Crash 04.07.2026).
        Assert.DoesNotContain("removeMapLayer", source);
        Assert.Contains("def _update_or_create_layer", source);
        Assert.Contains(".reload()", source);

        // Auto-Zoom nur bei Haltungs-Wechsel, nicht bei jedem 3-s-Poll.
        Assert.Contains("_last_zoomed_holding", source);
    }

    [Fact]
    public void Install_script_can_copy_plugin_to_qgis_profiles()
    {
        var script = File.ReadAllText(RepoFile("integrations", "qgis", "install-sewerstudio-bridge.ps1"));
        var readme = File.ReadAllText(RepoFile("integrations", "qgis", "README.md"));

        Assert.Contains("QGIS3", script);
        Assert.Contains("QGIS*", script);
        Assert.Contains("python\\plugins", script);
        Assert.Contains("Copy-Item", script);
        Assert.Contains("sewerstudio_bridge", script);
        Assert.Contains("Nach QGIS-Update", readme);
        Assert.Contains("Datenvertrag", readme);
    }

    private static string ReadPluginFile(string fileName)
        => File.ReadAllText(RepoFile("integrations", "qgis", "sewerstudio_bridge", fileName));
}
