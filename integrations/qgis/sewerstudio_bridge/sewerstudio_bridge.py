import json
import os
import tempfile
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

try:
    from qgis.PyQt.QtGui import QAction
except ImportError:
    from qgis.PyQt.QtWidgets import QAction

from qgis.PyQt.QtCore import QSettings, QTimer, Qt
from qgis.PyQt.QtWidgets import (
    QCheckBox,
    QDockWidget,
    QFileDialog,
    QFormLayout,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QPushButton,
    QSpinBox,
    QVBoxLayout,
    QWidget,
)
from qgis.core import Qgis, QgsMessageLog, QgsProject, QgsVectorLayer


PLUGIN_MENU = "&SewerStudio"
DEFAULT_BRIDGE_URL = "http://127.0.0.1:8765"
DEFAULT_DATA_ROOT = r"D:\QGIS_V4.03\Export_Sewer_Studio"
SETTINGS_PREFIX = "SewerStudioBridge"

REMOTE_LAYERS = (
    ("current", "SewerStudio - Aktuelle Haltung", "/qgis/current.geojson"),
    ("damages", "SewerStudio - Schaeden", "/qgis/damages.geojson"),
    ("network", "SewerStudio - Netzbewertung", "/qgis/network.geojson"),
)

LOCAL_GEOJSON_LAYERS = (
    ("current", "SewerStudio - Aktuelle Haltung", ("current_haltung.geojson", "current.geojson")),
    ("damages", "SewerStudio - Schaeden", ("schaeden.geojson", "damages.geojson")),
    ("network", "SewerStudio - Netzbewertung", ("netzbewertung.geojson", "network.geojson")),
)

LOCAL_SHAPEFILE_PATTERNS = (
    ("Haltungen", "SewerStudio - Haltungen"),
    ("Schaechte", "SewerStudio - Schaechte"),
    ("Schaeden", "SewerStudio - Schaeden Export"),
)


class SewerStudioBridgePlugin:
    def __init__(self, iface):
        self.iface = iface
        self.action = None
        self.dock = None

    def initGui(self):
        self.action = QAction("SewerStudio Bridge", self.iface.mainWindow())
        self.action.setObjectName("SewerStudioBridgeAction")
        self.action.triggered.connect(self.show_dock)
        self.iface.addPluginToMenu(PLUGIN_MENU, self.action)
        self.iface.addToolBarIcon(self.action)

    def unload(self):
        if self.action is not None:
            self.iface.removePluginMenu(PLUGIN_MENU, self.action)
            self.iface.removeToolBarIcon(self.action)
            self.action = None

        if self.dock is not None:
            self.dock.stop()
            self.iface.removeDockWidget(self.dock)
            self.dock.deleteLater()
            self.dock = None

    def show_dock(self):
        if self.dock is None:
            self.dock = SewerStudioBridgeDock(self.iface)
            self.iface.addDockWidget(right_dock_widget_area(), self.dock)
        self.dock.show()
        self.dock.raise_()


class SewerStudioBridgeDock(QDockWidget):
    def __init__(self, iface):
        super().__init__("SewerStudio Bridge", iface.mainWindow())
        self.iface = iface
        self.settings = QSettings()
        self.timer = QTimer(self)
        self.timer.timeout.connect(self.refresh_remote_layers)
        self.cache_dir = Path(tempfile.gettempdir()) / "sewerstudio_qgis_bridge"
        self.cache_dir.mkdir(parents=True, exist_ok=True)
        self.setObjectName("SewerStudioBridgeDock")
        self._build_ui()
        self._load_settings()

    def _build_ui(self):
        root = QWidget(self)
        layout = QVBoxLayout(root)

        form = QFormLayout()
        self.url_edit = QLineEdit(DEFAULT_BRIDGE_URL)
        self.data_root_edit = QLineEdit(DEFAULT_DATA_ROOT)
        self.poll_seconds = QSpinBox()
        self.poll_seconds.setRange(1, 60)
        self.poll_seconds.setValue(3)
        self.auto_zoom_check = QCheckBox("Aktuelle Haltung automatisch zoomen")
        self.auto_zoom_check.setChecked(True)

        data_root_row = QWidget(root)
        data_root_layout = QHBoxLayout(data_root_row)
        data_root_layout.setContentsMargins(0, 0, 0, 0)
        data_root_layout.addWidget(self.data_root_edit, 1)
        browse_button = QPushButton("...")
        browse_button.setMaximumWidth(32)
        browse_button.clicked.connect(self._browse_data_root)
        data_root_layout.addWidget(browse_button)

        form.addRow("Bridge-URL", self.url_edit)
        form.addRow("Datenordner", data_root_row)
        form.addRow("Intervall (s)", self.poll_seconds)
        form.addRow("", self.auto_zoom_check)
        layout.addLayout(form)

        buttons = QHBoxLayout()
        self.connect_button = QPushButton("Verbinden")
        self.refresh_button = QPushButton("Aktualisieren")
        self.local_button = QPushButton("Lokale Export-Layer laden")
        self.connect_button.clicked.connect(self.toggle_connection)
        self.refresh_button.clicked.connect(self.refresh_remote_layers)
        self.local_button.clicked.connect(self.load_local_export_layers)
        buttons.addWidget(self.connect_button)
        buttons.addWidget(self.refresh_button)
        layout.addLayout(buttons)
        layout.addWidget(self.local_button)

        self.status_label = QLabel("Nicht verbunden.")
        self.status_label.setWordWrap(True)
        layout.addWidget(self.status_label)
        layout.addStretch(1)
        self.setWidget(root)

    def _load_settings(self):
        self.url_edit.setText(self.settings.value(f"{SETTINGS_PREFIX}/bridgeUrl", DEFAULT_BRIDGE_URL))
        self.data_root_edit.setText(self.settings.value(f"{SETTINGS_PREFIX}/dataRoot", DEFAULT_DATA_ROOT))
        self.poll_seconds.setValue(int(self.settings.value(f"{SETTINGS_PREFIX}/pollSeconds", 3)))
        auto_zoom = self.settings.value(f"{SETTINGS_PREFIX}/autoZoomCurrent", "true")
        self.auto_zoom_check.setChecked(str(auto_zoom).lower() in ("1", "true", "yes"))

    def _save_settings(self):
        self.settings.setValue(f"{SETTINGS_PREFIX}/bridgeUrl", self.url_edit.text().strip())
        self.settings.setValue(f"{SETTINGS_PREFIX}/dataRoot", self.data_root_edit.text().strip())
        self.settings.setValue(f"{SETTINGS_PREFIX}/pollSeconds", self.poll_seconds.value())
        self.settings.setValue(f"{SETTINGS_PREFIX}/autoZoomCurrent", self.auto_zoom_check.isChecked())

    def _browse_data_root(self):
        selected = QFileDialog.getExistingDirectory(
            self,
            "SewerStudio-Datenordner waehlen",
            self.data_root_edit.text().strip() or DEFAULT_DATA_ROOT,
        )
        if selected:
            self.data_root_edit.setText(selected)
            self._save_settings()

    def toggle_connection(self):
        self._save_settings()
        if self.timer.isActive():
            self.timer.stop()
            self.connect_button.setText("Verbinden")
            self._set_status("Verbindung gestoppt.")
            return

        self.refresh_remote_layers()
        self.timer.start(self.poll_seconds.value() * 1000)
        self.connect_button.setText("Trennen")

    def stop(self):
        if self.timer.isActive():
            self.timer.stop()

    def refresh_remote_layers(self):
        self._save_settings()
        loaded = 0

        status = self._fetch_json("/qgis/status.json")
        if status is not None:
            self._set_status_from_payload(status)

        for layer_key, layer_name, endpoint in REMOTE_LAYERS:
            data = self._fetch_bytes(endpoint)
            if data is None:
                continue

            target = self.cache_dir / f"{layer_key}.geojson"
            target.write_bytes(data)
            layer = self._replace_vector_layer(layer_name, target)
            if layer is not None:
                loaded += 1
                if layer_key == "current" and self.auto_zoom_check.isChecked():
                    self._zoom_to_layer(layer)

        if loaded == 0 and status is None:
            self._set_status("Kein Live-Bridge-Feed erreichbar. Lokale Export-Layer koennen trotzdem geladen werden.")
        elif loaded > 0:
            self._set_status(f"{loaded} Live-Layer aktualisiert.")

    def load_local_export_layers(self):
        self._save_settings()
        data_root = Path(self.data_root_edit.text().strip() or DEFAULT_DATA_ROOT)
        if not data_root.exists():
            self._set_status(f"Datenordner nicht gefunden: {data_root}")
            return

        loaded = 0
        for _, layer_name, filenames in LOCAL_GEOJSON_LAYERS:
            file_path = self._first_existing(data_root, filenames)
            if file_path is None:
                continue
            if self._replace_vector_layer(layer_name, file_path) is not None:
                loaded += 1

        export_dir = self._find_latest_shapefile_export(data_root)
        if export_dir is not None:
            for prefix, layer_name in LOCAL_SHAPEFILE_PATTERNS:
                shp = self._find_first_shapefile(export_dir, prefix)
                if shp is not None and self._replace_vector_layer(layer_name, shp) is not None:
                    loaded += 1

        if loaded == 0:
            self._set_status(f"Keine passenden GeoJSON- oder Shapefile-Layer gefunden: {data_root}")
        else:
            self._set_status(f"{loaded} lokale Layer geladen.")

    def _fetch_json(self, endpoint):
        data = self._fetch_bytes(endpoint)
        if data is None:
            return None
        try:
            return json.loads(data.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            self._log_warning(f"Ungueltiges JSON von {endpoint}")
            return None

    def _fetch_bytes(self, endpoint):
        base = (self.url_edit.text().strip() or DEFAULT_BRIDGE_URL).rstrip("/")
        url = f"{base}{endpoint}"
        try:
            request = Request(url, headers={"Accept": "application/json, application/geo+json"})
            with urlopen(request, timeout=1.5) as response:
                if response.status != 200:
                    return None
                return response.read()
        except HTTPError as ex:
            if ex.code != 404:
                self._log_warning(f"Bridge-Request fehlgeschlagen ({ex.code}): {url}")
            return None
        except (URLError, TimeoutError, OSError) as ex:
            self._log_warning(f"Bridge nicht erreichbar: {url} ({ex})")
            return None

    def _replace_vector_layer(self, layer_name, file_path):
        file_path = Path(file_path)
        if not file_path.exists():
            return None

        layer = QgsVectorLayer(str(file_path), layer_name, "ogr")
        if not layer.isValid():
            self._log_warning(f"Layer nicht ladbar: {file_path}")
            return None

        self._remove_layers_named(layer_name)
        QgsProject.instance().addMapLayer(layer)
        return layer

    def _remove_layers_named(self, layer_name):
        project = QgsProject.instance()
        for layer in list(project.mapLayers().values()):
            if layer.name() == layer_name:
                project.removeMapLayer(layer.id())

    def _zoom_to_layer(self, layer):
        extent = layer.extent()
        if extent is None or extent.isEmpty():
            return
        canvas = self.iface.mapCanvas()
        canvas.setExtent(extent)
        canvas.refresh()

    def _set_status_from_payload(self, payload):
        holding = payload.get("currentHolding") or payload.get("current_holding") or payload.get("haltung")
        if holding:
            self._set_status(f"Verbunden. Aktuelle Haltung: {holding}")
        else:
            self._set_status("Verbunden. Kein aktiver Haltungsname gemeldet.")

    def _set_status(self, text):
        self.status_label.setText(text)

    def _log_warning(self, message):
        warning = getattr(Qgis, "Warning", 1)
        QgsMessageLog.logMessage(message, "SewerStudio Bridge", warning)

    @staticmethod
    def _first_existing(root, filenames):
        for filename in filenames:
            candidate = root / filename
            if candidate.exists():
                return candidate
        return None

    @staticmethod
    def _find_latest_shapefile_export(root):
        candidates = []
        roots_to_scan = [root]
        plugin_test = root / "_plugin_test"
        if plugin_test.exists():
            roots_to_scan.append(plugin_test)

        for scan_root in roots_to_scan:
            try:
                for current_root, _, files in os.walk(scan_root):
                    if any(name.lower().endswith(".shp") for name in files):
                        path = Path(current_root)
                        candidates.append((path.stat().st_mtime, path))
            except OSError:
                continue

        if not candidates:
            return None

        candidates.sort(key=lambda item: item[0], reverse=True)
        return candidates[0][1]

    @staticmethod
    def _find_first_shapefile(folder, prefix):
        try:
            matches = sorted(folder.glob(f"{prefix}*.shp"))
        except OSError:
            return None
        return matches[0] if matches else None


def right_dock_widget_area():
    legacy_value = getattr(Qt, "RightDockWidgetArea", None)
    if legacy_value is not None:
        return legacy_value
    return Qt.DockWidgetArea.RightDockWidgetArea
