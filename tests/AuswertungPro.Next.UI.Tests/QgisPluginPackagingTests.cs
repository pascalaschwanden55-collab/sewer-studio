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
        // Schacht-Bridge (analog Haltungen): Live-Schacht-Layer + Auto-Zoom-Punkt.
        Assert.Contains("/qgis/schaechte.geojson", source);
        Assert.Contains("/qgis/current_schacht.geojson", source);
        // "Ausgefuehrt durch" live: Layer aktualisiert sich wie die anderen alle paar Sekunden.
        Assert.Contains("/qgis/sanierungstyp.geojson", source);
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

        // Speichercache: unveraenderte Antworten werden per Hash erkannt und uebersprungen.
        Assert.Contains("hashlib", source);
        Assert.Contains("_last_payload_hash", source);

        // Auto-Zoom muss die Layer-Ausdehnung ins Karten-CRS transformieren.
        Assert.Contains("QgsCoordinateTransform", source);

        // Zoom bei jedem Auswahl-Klick (Stempel), nicht nur bei Haltungswechsel.
        Assert.Contains("selectionStamp", source);
        Assert.Contains("_last_zoom_stamp", source);

        // Schacht-Auswahl hat eigenen Stempel/Zoom-Kanal.
        Assert.Contains("schachtSelectionStamp", source);
        Assert.Contains("_last_zoomed_schacht", source);

        // Punkt-Extent-Fix: ein einzelner Schacht-Punkt darf den Zoom nicht per isEmpty()
        // abwuergen; Extent wird vor der Transformation aufgeblasen.
        Assert.Contains("extent.grow", source);
        Assert.Contains("isNull()", source);

        // Ehrliches Feedback, wenn die aktuelle Haltung keine Geometrie hat.
        Assert.Contains("currentHoldingHasGeometry", source);
    }

    [Fact]
    public void Plugin_hat_status_symbol_und_blinkt_beim_zoom()
    {
        var source = ReadPluginFile("sewerstudio_bridge.py");

        // Professionelles Werkzeugleisten-Symbol (Schacht-Haltung-Schacht, Verbindung
        // in Statusfarbe) + Aufklapp-Pfeil mit Menue (Verbinden/Aktualisieren/…).
        Assert.Contains("_make_status_icon", source);
        Assert.Contains("QToolButton", source);
        Assert.Contains("setMenu", source);
        Assert.Contains("setPopupMode", source);
        Assert.Contains("#16A34A", source); // gruen = verbunden

        // Nach QGIS-Neustart automatisch wieder verbinden (Symbol wird von selbst gruen).
        Assert.Contains("was_connected", source);
        Assert.Contains("start_connection", source);
        // QGIS 4 darf Live-Layer nicht schon waehrend QgisApp::QgisApp einfuegen.
        Assert.Contains("initializationCompleted", source);
        Assert.Contains("_schedule_auto_connect_after_qgis_startup", source);
        Assert.DoesNotContain("QTimer.singleShot(1500, self._auto_connect)", source);

        // Gezoomte Haltung/Schacht blinkt auf (wie das QGIS-Highlight-Werkzeug).
        Assert.Contains("flashGeometries", source);
        Assert.Contains("_flash_layer", source);
    }

    [Fact]
    public void Plugin_schreibt_in_feste_layer_dateien_und_findet_gestylte_ebene()
    {
        var source = ReadPluginFile("sewerstudio_bridge.py");

        // Live-Ebenen werden in feste Dateien SewerStudio_<key>.geojson geschrieben,
        // damit vom Nutzer vor-gestylte QGIS-Ebenen (auf genau diese Dateien) sich
        // automatisch aktualisieren. Ordner einstellbar, Default D:\QGIS_V4.03\Layer.
        Assert.Contains(@"D:\QGIS_V4.03\Layer", source);
        Assert.Contains("SewerStudio_", source);
        Assert.Contains("_layer_target", source);
        // Bestehende (gestylte) Ebene ueber die Quelle finden -> Stil bleibt beim Reload.
        Assert.Contains("_find_layer_by_source", source);
    }

    [Fact]
    public void Plugin_beschriftet_schacht_live_layer_mit_sewerstudio_zeilennummer()
    {
        var source = ReadPluginFile("sewerstudio_bridge.py");

        Assert.Contains("_ensure_schacht_nr_labels", source);
        Assert.Contains("layer_key != \"schacht_sanierungstyp\"", source);
        Assert.Contains("settings.fieldName = \"nr\"", source);
        Assert.Contains("QgsVectorLayerSimpleLabeling", source);
        Assert.Contains("QgsTextBufferSettings", source);
        Assert.Contains("layer.labelsEnabled()", source);
        Assert.Contains("schacht_nr_labels_initialized", source);
    }

    [Fact]
    public void Plugin_haelt_zoom_ziel_layer_robust_gegen_leer_erstladung()
    {
        var source = ReadPluginFile("sewerstudio_bridge.py");

        // Ursache "kein Zoom": Ein zuerst LEER geladener GeoJSON-Layer bekommt in QGIS
        // den Geometrietyp "Unbekannt" und meldet danach eine leere Ausdehnung.
        // Schutz 1: Zoom-Ziel-Layer nie leer anlegen (leere FeatureCollection ueberspringen).
        Assert.Contains("\"features\":[]", source);
        // Schutz 2: Ausdehnung notfalls direkt aus den Features bauen.
        Assert.Contains("_extent_from_features", source);
        Assert.Contains("combineExtentWith", source);
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

        // Zentrale Plugin-Ablage: jede Installation sichert Ordner + versioniertes ZIP
        // nach D:\QGIS_V4.03\AWU_Plugins (Nutzer-Konvention fuer ALLE QGIS-Plugins).
        Assert.Contains(@"D:\QGIS_V4.03\AWU_Plugins", script);
        Assert.Contains("Compress-Archive", script);
    }

    private static string ReadPluginFile(string fileName)
        => File.ReadAllText(RepoFile("integrations", "qgis", "sewerstudio_bridge", fileName));
}
