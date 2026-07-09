import hashlib
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

from qgis.PyQt.QtCore import QRectF, QSettings, QTimer, Qt
from qgis.PyQt.QtGui import QBrush, QColor, QIcon, QPainter, QPen, QPixmap
from qgis.PyQt.QtWidgets import (
    QCheckBox,
    QDockWidget,
    QFileDialog,
    QFormLayout,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QMenu,
    QPushButton,
    QSpinBox,
    QToolButton,
    QVBoxLayout,
    QWidget,
)
from qgis.core import (
    Qgis,
    QgsCoordinateTransform,
    QgsMessageLog,
    QgsProject,
    QgsRectangle,
    QgsVectorLayer,
)


PLUGIN_MENU = "&SewerStudio"
DEFAULT_BRIDGE_URL = "http://127.0.0.1:8765"
DEFAULT_DATA_ROOT = r"D:\QGIS_V4.03\Export_Sewer_Studio"
# Fester Ordner, in den die Live-Ebenen als SewerStudio_<key>.geojson geschrieben
# werden — damit vom Nutzer vor-gestylte QGIS-Ebenen, die auf genau diese Dateien
# zeigen (z.B. nach ausgefuehrt_durch eingefaerbt), sich automatisch aktualisieren.
DEFAULT_LAYER_DIR = r"D:\QGIS_V4.03\Layer"
SETTINGS_PREFIX = "SewerStudioBridge"

REMOTE_LAYERS = (
    ("current", "SewerStudio - Aktuelle Haltung", "/qgis/current.geojson"),
    ("current_schacht", "SewerStudio - Aktueller Schacht", "/qgis/current_schacht.geojson"),
    ("damages", "SewerStudio - Schaeden", "/qgis/damages.geojson"),
    ("network", "SewerStudio - Netzbewertung", "/qgis/network.geojson"),
    # "Ausgefuehrt durch": Linien der Haltungen mit gesetztem Ausfuehrenden,
    # Feld ausgefuehrt_durch = Baumeister/Sanierer/Gartenbauer (in QGIS einmalig
    # kategorisiert einfaerben; der Stil bleibt beim In-Place-Reload erhalten).
    ("sanierungstyp", "SewerStudio - Ausgefuehrt durch", "/qgis/sanierungstyp.geojson"),
    # Schacht-Pendant zu "Ausgefuehrt durch": Punkte der Schaechte mit gesetztem
    # Ausfuehrenden, Feld ausgefuehrt_durch = Baumeister/Sanierer/Gartenbauer (in QGIS
    # einmalig kategorisiert einfaerben; der Stil bleibt beim In-Place-Reload erhalten).
    ("schacht_sanierungstyp", "SewerStudio - Schacht Ausgefuehrt durch", "/qgis/schacht_sanierungstyp.geojson"),
    ("schaechte", "SewerStudio - Schaechte (live)", "/qgis/schaechte.geojson"),
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
    # Statusfarben des Werkzeugleisten-Symbols:
    #   gruen = verbunden & SewerStudio erreichbar, rot = verbunden aber App nicht
    #   erreichbar, grau = getrennt.
    _STATUS_COLORS = {"ok": "#16A34A", "error": "#DC2626", "off": "#9CA3AF"}
    _STATUS_TOOLTIPS = {
        "ok": "SewerStudio Bridge: verbunden",
        "error": "SewerStudio Bridge: verbunden, aber SewerStudio nicht erreichbar",
        "off": "SewerStudio Bridge: getrennt — klicken zum Verbinden",
    }

    def __init__(self, iface):
        self.iface = iface
        self.toolbar = None
        self.tool_button = None
        self.menu = None
        self.toggle_action = None
        self.settings_action = None
        self.dock = None
        self._last_status = None
        self._auto_connect_pending = False

    def initGui(self):
        # Dock im Hintergrund (versteckt) — traegt die Verbindungslogik, erscheint
        # nur, wenn der Nutzer die Einstellungen oeffnet.
        self.dock = SewerStudioBridgeDock(self.iface, status_callback=self._on_bridge_status)
        self.iface.addDockWidget(right_dock_widget_area(), self.dock)
        self.dock.hide()

        # Aufklapp-Menue mit allen Funktionen (wie beim MCP-Plugin, ueber den Pfeil).
        self.menu = QMenu(self.iface.mainWindow())
        self.toggle_action = self.menu.addAction("Verbinden", self.toggle)
        self.menu.addAction("Aktualisieren", self._refresh_now)
        self.menu.addSeparator()
        self.menu.addAction("Lokale Export-Layer laden", self._load_local)
        self.menu.addAction("Einstellungen…", self.show_dock)

        # Werkzeugleisten-Knopf: professionelles Symbol (Schacht-Haltung-Schacht,
        # Verbindung in Statusfarbe) + Aufklapp-Pfeil fuer das Menue. Klick aufs
        # Symbol selbst verbindet/trennt direkt.
        self.tool_button = QToolButton(self.iface.mainWindow())
        self.tool_button.setIcon(self._make_status_icon("off"))
        self.tool_button.setToolTip(self._STATUS_TOOLTIPS["off"])
        self.tool_button.setAutoRaise(True)
        self.tool_button.setMenu(self.menu)
        self.tool_button.setPopupMode(self._menu_button_popup_mode())
        self.tool_button.clicked.connect(self.toggle)

        self.toolbar = self.iface.addToolBar("SewerStudio Bridge")
        self.toolbar.setObjectName("SewerStudioBridgeToolbar")
        self.toolbar.addWidget(self.tool_button)

        # Zusaetzlich im Erweiterungen-Menue auffindbar.
        self.settings_action = QAction("SewerStudio Bridge…", self.iface.mainWindow())
        self.settings_action.triggered.connect(self.show_dock)
        self.iface.addPluginToMenu(PLUGIN_MENU, self.settings_action)

        # War beim letzten Mal verbunden? Dann erst nach vollstaendigem QGIS-Start
        # automatisch wieder verbinden. QGIS 4 kann abstuerzen, wenn Plugins schon
        # waehrend QgisApp::QgisApp Layer per addMapLayer einfuegen.
        if self.dock.was_connected():
            self._schedule_auto_connect_after_qgis_startup()

    @staticmethod
    def _menu_button_popup_mode():
        # Qt5: QToolButton.MenuButtonPopup, Qt6: QToolButton.ToolButtonPopupMode.MenuButtonPopup.
        mode = getattr(QToolButton, "MenuButtonPopup", None)
        if mode is None:
            mode = QToolButton.ToolButtonPopupMode.MenuButtonPopup
        return mode

    def _refresh_now(self):
        if self.dock is not None:
            self.dock.refresh_remote_layers()

    def _load_local(self):
        if self.dock is not None:
            self.dock.load_local_export_layers()

    def _schedule_auto_connect_after_qgis_startup(self):
        self._auto_connect_pending = True
        initialization_completed = getattr(self.iface, "initializationCompleted", None)
        if initialization_completed is not None:
            try:
                initialization_completed.connect(self._on_qgis_initialization_completed)
                return
            except Exception:
                pass

        # Fallback fuer sehr alte/abweichende QGIS-APIs ohne Signal.
        QTimer.singleShot(5000, self._auto_connect)

    def _disconnect_qgis_initialization_completed(self):
        initialization_completed = getattr(self.iface, "initializationCompleted", None)
        if initialization_completed is not None:
            try:
                initialization_completed.disconnect(self._on_qgis_initialization_completed)
            except Exception:
                pass

    def _on_qgis_initialization_completed(self):
        self._disconnect_qgis_initialization_completed()

        # Ein weiterer Event-Loop-Durchlauf laesst Projekt-/Dock-Signale abklingen,
        # bevor Live-Layer in das Projekt eingefuegt werden.
        QTimer.singleShot(250, self._auto_connect)

    def _auto_connect(self):
        if not self._auto_connect_pending:
            return
        self._auto_connect_pending = False
        if self.dock is not None and not self.dock.is_connected():
            self.dock.start_connection()

    def toggle(self):
        if self.dock is None:
            return
        self._auto_connect_pending = False
        if self.dock.is_connected():
            self.dock.stop_connection()
        else:
            self.dock.start_connection()

    def show_dock(self):
        if self.dock is not None:
            self.dock.show()
            self.dock.raise_()

    def _on_bridge_status(self, state):
        # Vom Dock nach jedem Poll aufgerufen: Symbol, Tooltip und Menue-Text
        # aktualisieren — nur bei echtem Zustandswechsel (nicht bei jedem 3s-Poll).
        if state == self._last_status:
            return
        self._last_status = state
        if self.tool_button is not None:
            self.tool_button.setIcon(self._make_status_icon(state))
            self.tool_button.setToolTip(self._STATUS_TOOLTIPS.get(state, self._STATUS_TOOLTIPS["off"]))
        if self.toggle_action is not None:
            self.toggle_action.setText("Trennen" if state != "off" else "Verbinden")

    def _make_status_icon(self, state):
        # Symbol: Kettenglied ("Link") in Statusfarbe — gruen = verbunden,
        # rot = App nicht erreichbar, grau = getrennt.
        color = self._STATUS_COLORS.get(state, self._STATUS_COLORS["off"])
        render = 64  # hoehere Aufloesung -> in der Werkzeugleiste (16/24 px) scharf
        pixmap = QPixmap(render, render)
        pixmap.fill(QColor(0, 0, 0, 0))  # transparenter Hintergrund
        painter = QPainter(pixmap)
        try:
            painter.setRenderHint(QPainter.RenderHint.Antialiasing, True)  # Qt6
        except AttributeError:
            painter.setRenderHint(QPainter.Antialiasing, True)  # Qt5
        painter.scale(render / 32.0, render / 32.0)  # in 32er-Logik zeichnen

        pen = QPen(QColor(color))
        pen.setWidthF(3.0)
        try:
            pen.setCapStyle(Qt.PenCapStyle.RoundCap)     # Qt6
            pen.setJoinStyle(Qt.PenJoinStyle.RoundJoin)
        except AttributeError:
            pen.setCapStyle(Qt.RoundCap)                 # Qt5
            pen.setJoinStyle(Qt.RoundJoin)
        painter.setPen(pen)
        painter.setBrush(QColor(0, 0, 0, 0))  # nur Kontur, keine Fuellung

        # Zwei diagonal verschlungene Glieder = "verbunden".
        for cx, cy in ((13, 19), (19, 13)):
            painter.save()
            painter.translate(cx, cy)
            painter.rotate(-45)
            painter.drawRoundedRect(QRectF(-8.5, -4.0, 17.0, 8.0), 4.0, 4.0)
            painter.restore()

        painter.end()
        return QIcon(pixmap)

    def unload(self):
        if self.settings_action is not None:
            self.iface.removePluginMenu(PLUGIN_MENU, self.settings_action)
            self.settings_action = None

        if self.toolbar is not None:
            self.toolbar.deleteLater()
            self.toolbar = None
        self.tool_button = None
        self.menu = None
        self.toggle_action = None
        self._auto_connect_pending = False
        self._disconnect_qgis_initialization_completed()

        if self.dock is not None:
            self.dock.stop()
            self.iface.removeDockWidget(self.dock)
            self.dock.deleteLater()
            self.dock = None


class SewerStudioBridgeDock(QDockWidget):
    def __init__(self, iface, status_callback=None):
        super().__init__("SewerStudio Bridge", iface.mainWindow())
        self.iface = iface
        # Rueckmeldung des Verbindungszustands an das Werkzeugleisten-Symbol.
        self._status_callback = status_callback
        self.settings = QSettings()
        self.timer = QTimer(self)
        self.timer.timeout.connect(self.refresh_remote_layers)
        self.cache_dir = Path(tempfile.gettempdir()) / "sewerstudio_qgis_bridge"
        self.cache_dir.mkdir(parents=True, exist_ok=True)
        # Auto-Zoom bei jedem Auswahl-Klick in SewerStudio (Stempel zaehlt hoch),
        # aber nicht bei jedem Poll ohne neue Auswahl.
        self._last_zoomed_holding = None
        self._last_zoom_stamp = None
        # Dasselbe fuer die Schacht-Auswahl (eigener Kanal, eigener Stempel).
        self._last_zoomed_schacht = None
        self._last_schacht_zoom_stamp = None
        # Speichercache: Hash der zuletzt geladenen Daten je Layer.
        # Unveraenderte Antworten werden uebersprungen (kein Reload, kein Neuzeichnen).
        self._last_payload_hash = {}
        self.setObjectName("SewerStudioBridgeDock")
        self._build_ui()
        self._load_settings()

    def _build_ui(self):
        root = QWidget(self)
        layout = QVBoxLayout(root)

        form = QFormLayout()
        self.url_edit = QLineEdit(DEFAULT_BRIDGE_URL)
        self.data_root_edit = QLineEdit(DEFAULT_DATA_ROOT)
        self.layer_dir_edit = QLineEdit(DEFAULT_LAYER_DIR)
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

        layer_dir_row = QWidget(root)
        layer_dir_layout = QHBoxLayout(layer_dir_row)
        layer_dir_layout.setContentsMargins(0, 0, 0, 0)
        layer_dir_layout.addWidget(self.layer_dir_edit, 1)
        layer_browse_button = QPushButton("...")
        layer_browse_button.setMaximumWidth(32)
        layer_browse_button.clicked.connect(self._browse_layer_dir)
        layer_dir_layout.addWidget(layer_browse_button)

        form.addRow("Bridge-URL", self.url_edit)
        form.addRow("Datenordner", data_root_row)
        form.addRow("Layer-Ordner (feste Dateien)", layer_dir_row)
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
        self.layer_dir_edit.setText(self.settings.value(f"{SETTINGS_PREFIX}/layerDir", DEFAULT_LAYER_DIR))
        self.poll_seconds.setValue(int(self.settings.value(f"{SETTINGS_PREFIX}/pollSeconds", 3)))
        auto_zoom = self.settings.value(f"{SETTINGS_PREFIX}/autoZoomCurrent", "true")
        self.auto_zoom_check.setChecked(str(auto_zoom).lower() in ("1", "true", "yes"))

    def _save_settings(self):
        self.settings.setValue(f"{SETTINGS_PREFIX}/bridgeUrl", self.url_edit.text().strip())
        self.settings.setValue(f"{SETTINGS_PREFIX}/dataRoot", self.data_root_edit.text().strip())
        self.settings.setValue(f"{SETTINGS_PREFIX}/layerDir", self.layer_dir_edit.text().strip())
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

    def _browse_layer_dir(self):
        selected = QFileDialog.getExistingDirectory(
            self,
            "Ordner fuer die festen SewerStudio-Layerdateien waehlen",
            self.layer_dir_edit.text().strip() or DEFAULT_LAYER_DIR,
        )
        if selected:
            self.layer_dir_edit.setText(selected)
            self._save_settings()

    def _layer_target(self, layer_key):
        # Zieldatei fuer die Live-Ebene: fester Ordner (SewerStudio_<key>.geojson),
        # damit vor-gestylte QGIS-Ebenen sie lesen. Ohne gesetzten Ordner -> Temp-Cache.
        layer_dir = self.layer_dir_edit.text().strip()
        if layer_dir:
            return Path(layer_dir) / f"SewerStudio_{layer_key}.geojson"
        return self.cache_dir / f"{layer_key}.geojson"

    def _write_layer_file(self, target, layer_key, data):
        # Gibt den TATSAECHLICH geschriebenen Pfad zurueck. Ist der feste Ordner nicht
        # schreibbar, weicht das Plugin auf den Temp-Cache aus (nie den Poll abbrechen).
        try:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
            return target
        except OSError as ex:
            self._log_warning(f"Layer-Datei nicht schreibbar ({target}): {ex} -> Temp-Cache")
            fallback = self.cache_dir / f"{layer_key}.geojson"
            fallback.write_bytes(data)
            return fallback

    def toggle_connection(self):
        if self.timer.isActive():
            self.stop_connection()
        else:
            self.start_connection()

    def start_connection(self):
        self._save_settings()
        # Merken, dass verbunden war -> naechster QGIS-Start verbindet automatisch.
        self.settings.setValue(f"{SETTINGS_PREFIX}/connected", True)
        self.connect_button.setText("Trennen")
        # Timer VOR dem ersten Refresh starten, damit is_connected() bereits True ist
        # und die Statusanzeige schon beim Verbinden auf gruen/rot geht.
        if not self.timer.isActive():
            self.timer.start(self.poll_seconds.value() * 1000)
        self.refresh_remote_layers()

    def stop_connection(self):
        if self.timer.isActive():
            self.timer.stop()
        self.settings.setValue(f"{SETTINGS_PREFIX}/connected", False)
        self.connect_button.setText("Verbinden")
        self._set_status("Verbindung gestoppt.")
        self._notify_status("off")

    def is_connected(self):
        return self.timer.isActive()

    def was_connected(self):
        return str(self.settings.value(f"{SETTINGS_PREFIX}/connected", "false")).lower() in (
            "1", "true", "yes")

    def _notify_status(self, state):
        # Verbindungszustand ans Werkzeugleisten-Symbol melden (gruen/rot/grau).
        if self._status_callback is not None:
            try:
                self._status_callback(state)
            except Exception:
                pass

    def stop(self):
        if self.timer.isActive():
            self.timer.stop()

    def refresh_remote_layers(self):
        self._save_settings()
        loaded = 0

        status = self._fetch_json("/qgis/status.json")
        holding = self._holding_from_payload(status) if status is not None else None
        stamp = status.get("selectionStamp") if status is not None else None
        schacht = status.get("currentSchacht") if status is not None else None
        schacht_stamp = status.get("schachtSelectionStamp") if status is not None else None
        if status is not None:
            self._set_status_from_payload(status)

        # Werkzeugleisten-Symbol nur aktualisieren, wenn TATSAECHLICH verbunden
        # (Poll-Timer laeuft). Sonst wuerde ein einmaliges "Aktualisieren" im
        # getrennten Zustand das Symbol faelschlich auf gruen setzen.
        if self.is_connected():
            self._notify_status("ok" if status is not None else "error")

        updated = 0
        for layer_key, layer_name, endpoint in REMOTE_LAYERS:
            data = self._fetch_bytes(endpoint)
            if data is None:
                continue

            digest = hashlib.sha256(data).hexdigest()
            target = self._layer_target(layer_key)
            # Bestehende Ebene bevorzugt ueber die QUELLE finden — so wird auch die
            # vom Nutzer vor-gestylte Ebene (die auf dieselbe Datei zeigt) getroffen.
            existing = self._find_layer_by_source(target) or self._find_layer_named(layer_name)

            # Layer, die anfangs LEER sein koennen, NICHT neu anlegen, solange sie leer
            # sind: ein leer erstellter GeoJSON-Layer bekommt in QGIS den Geometrietyp
            # "Unbekannt" und rendert/zoomt danach nicht mehr zuverlaessig. Existiert
            # die Ebene aber schon (mit korrekter Geometrie), DARF sie auf leer gesetzt
            # werden — so verschwindet z.B. eine entfernte "Ausgefuehrt durch"-Linie
            # wieder, statt stehenzubleiben.
            if (layer_key in ("current", "current_schacht", "sanierungstyp", "schacht_sanierungstyp")
                    and b'"features":[]' in data
                    and existing is None):
                continue

            if existing is not None and self._last_payload_hash.get(layer_key) == digest:
                # Unveraendert: nichts schreiben, nichts neu laden, nichts neu zeichnen.
                layer = existing
            else:
                written = self._write_layer_file(target, layer_key, data)
                layer = self._update_or_create_layer(layer_name, written)
                if layer is not None:
                    self._last_payload_hash[layer_key] = digest
                    updated += 1

            if layer is not None:
                loaded += 1
                # Zoomen bei jedem Auswahl-Klick in SewerStudio (neuer Stempel) oder
                # bei Wechsel — aber nie einfach bei jedem Poll. Haltung und Schacht
                # haben getrennte Kanaele/Stempel.
                if layer_key == "current" and self.auto_zoom_check.isChecked() and holding:
                    if holding != self._last_zoomed_holding or (stamp is not None and stamp != self._last_zoom_stamp):
                        self._zoom_to_layer(layer)
                        self._last_zoomed_holding = holding
                        self._last_zoom_stamp = stamp
                elif layer_key == "current_schacht" and self.auto_zoom_check.isChecked() and schacht:
                    if schacht != self._last_zoomed_schacht or (schacht_stamp is not None and schacht_stamp != self._last_schacht_zoom_stamp):
                        self._zoom_to_layer(layer)
                        self._last_zoomed_schacht = schacht
                        self._last_schacht_zoom_stamp = schacht_stamp

        if loaded == 0 and status is None:
            self._set_status("Kein Live-Bridge-Feed erreichbar. Lokale Export-Layer koennen trotzdem geladen werden.")
        elif loaded > 0:
            self._set_status(f"{loaded} Live-Layer aktuell ({updated} neu geladen).")

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
            if self._update_or_create_layer(layer_name, file_path) is not None:
                loaded += 1

        export_dir = self._find_latest_shapefile_export(data_root)
        if export_dir is not None:
            for prefix, layer_name in LOCAL_SHAPEFILE_PATTERNS:
                shp = self._find_first_shapefile(export_dir, prefix)
                if shp is not None and self._update_or_create_layer(layer_name, shp) is not None:
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

    def _update_or_create_layer(self, layer_name, file_path):
        # WICHTIG: Bestehende Layer werden NIE entfernt und neu angelegt.
        # Ein geloeschter Layer laesst offene QGIS-Dialoge (z.B. Layer-Eigenschaften)
        # mit einem toten Zeiger zurueck -> Access Violation / QGIS-Absturz.
        # Stattdessen: Datenquelle in-place neu laden; Styling bleibt so auch erhalten.
        file_path = Path(file_path)
        if not file_path.exists():
            return None

        # Zuerst die Ebene suchen, die auf DIESELBE Datei zeigt (auch eine vom Nutzer
        # gestylte) — dann bleibt beim Neuladen ihr Stil erhalten. Sonst per Name.
        existing = self._find_layer_by_source(file_path) or self._find_layer_named(layer_name)
        if existing is not None:
            if self._same_source(existing, file_path):
                existing.reload()
                existing.updateExtents()
                existing.triggerRepaint()
                return existing

            if self._switch_layer_source(existing, file_path, layer_name):
                return existing

            self._log_warning(
                f"Layer '{layer_name}' behaelt alte Quelle (setDataSource nicht verfuegbar); "
                f"bitte Layer manuell entfernen und neu laden: {file_path}"
            )
            return None

        layer = QgsVectorLayer(str(file_path), layer_name, "ogr")
        if not layer.isValid():
            self._log_warning(f"Layer nicht ladbar: {file_path}")
            return None

        QgsProject.instance().addMapLayer(layer)
        return layer

    @staticmethod
    def _find_layer_named(layer_name):
        for layer in QgsProject.instance().mapLayers().values():
            if layer.name() == layer_name:
                return layer
        return None

    @staticmethod
    def _find_layer_by_source(file_path):
        # Findet eine geladene Ebene, deren Datenquelle auf DIESELBE Datei zeigt —
        # unabhaengig vom Ebenennamen. So wird die vom Nutzer gestylte Ebene getroffen.
        target = os.path.normcase(os.path.normpath(str(file_path)))
        for layer in QgsProject.instance().mapLayers().values():
            source = (layer.source() or "").split("|")[0]
            if os.path.normcase(os.path.normpath(source)) == target:
                return layer
        return None

    @staticmethod
    def _same_source(layer, file_path):
        # ogr-Quellen koennen Optionen anhaengen ("pfad|layername=..."), daher nur den Pfadteil vergleichen.
        source = (layer.source() or "").split("|")[0]
        return os.path.normcase(os.path.normpath(source)) == os.path.normcase(os.path.normpath(str(file_path)))

    def _switch_layer_source(self, layer, file_path, layer_name):
        # Quelle wechseln, ohne den Layer zu zerstoeren (z.B. neuer Shapefile-Exportordner).
        try:
            layer.setDataSource(str(file_path), layer_name, "ogr")
        except TypeError:
            try:
                from qgis.core import QgsDataProvider

                layer.setDataSource(str(file_path), layer_name, "ogr", QgsDataProvider.ProviderOptions())
            except Exception as ex:
                self._log_warning(f"setDataSource fehlgeschlagen: {ex}")
                return False
        except Exception as ex:
            self._log_warning(f"setDataSource fehlgeschlagen: {ex}")
            return False

        if not layer.isValid():
            self._log_warning(f"Layer nach Quellwechsel ungueltig: {file_path}")
            return False

        layer.updateExtents()
        layer.triggerRepaint()
        return True

    def _zoom_to_layer(self, layer):
        extent = layer.extent()
        # Fallback: Ein Layer, der zuerst LEER geladen wurde, behaelt in QGIS den
        # Geometrietyp "Unbekannt" und meldet nach dem Nachladen von Daten oft eine
        # leere Ausdehnung. Dann die Ausdehnung direkt aus den Features bauen —
        # unabhaengig vom (falschen) Geometrietyp des Layers.
        if extent is None or extent.isNull():
            extent = self._extent_from_features(layer)
        # isNull() (nicht isEmpty()!) als Abbruch: ein einzelner Punkt (current_schacht)
        # hat Breite/Hoehe 0 und gilt bei isEmpty() als leer -> der Zoom wuerde nie ausloesen.
        if extent is None or extent.isNull():
            return

        # Punkt- oder entarteter Linien-Extent (Flaeche 0): vor der Transformation um
        # 25 m (LV95-Meter) aufblasen, sonst degeneriert der Zoom auf einen Punkt.
        if extent.width() == 0 or extent.height() == 0:
            extent.grow(25)

        canvas = self.iface.mapCanvas()
        # Layer-Ausdehnung in das Karten-CRS umrechnen — sonst zoomt die Karte
        # bei abweichendem Projekt-CRS an eine voellig falsche Stelle.
        try:
            canvas_crs = canvas.mapSettings().destinationCrs()
            layer_crs = layer.crs()
            if layer_crs.isValid() and canvas_crs.isValid() and layer_crs != canvas_crs:
                transform = QgsCoordinateTransform(layer_crs, canvas_crs, QgsProject.instance())
                extent = transform.transformBoundingBox(extent)
        except Exception as ex:  # Zoom ist Komfort — nie den Poll deswegen abbrechen
            self._log_warning(f"Zoom-Transformation fehlgeschlagen: {ex}")
            return

        extent.scale(1.3)
        canvas.setExtent(extent)
        canvas.refresh()
        # Aufblinken wie beim QGIS-"Objekte hervorheben": macht die gezoomte
        # Haltung bzw. den Schacht sofort sichtbar (mehrfaches Blinken).
        self._flash_layer(canvas, layer)

    @staticmethod
    def _flash_layer(canvas, layer):
        try:
            geometries = [
                feature.geometry()
                for feature in layer.getFeatures()
                if feature.hasGeometry() and not feature.geometry().isEmpty()
            ]
            if geometries:
                canvas.flashGeometries(geometries, layer.crs())
        except Exception:
            # Aufblinken ist reiner Komfort — Fehler nie eskalieren lassen.
            pass

    @staticmethod
    def _extent_from_features(layer):
        # Ausdehnung aus den Feature-Geometrien selbst — greift, wenn layer.extent()
        # leer ist (z.B. Layer mit Geometrietyp "Unbekannt" nach Leer-Erstladung).
        rect = QgsRectangle()
        rect.setMinimal()
        found = False
        for feature in layer.getFeatures():
            geometry = feature.geometry()
            if geometry is None or geometry.isEmpty():
                continue
            rect.combineExtentWith(geometry.boundingBox())
            found = True
        return rect if found else None

    @staticmethod
    def _holding_from_payload(payload):
        return payload.get("currentHolding") or payload.get("current_holding") or payload.get("haltung")

    def _set_status_from_payload(self, payload):
        holding = self._holding_from_payload(payload)
        if not holding:
            self._set_status("Verbunden. Kein aktiver Haltungsname gemeldet.")
            return

        # Ehrliches Feedback: ohne aufloesbare Geometrie gibt es nichts zu zoomen.
        has_geometry = payload.get("currentHoldingHasGeometry")
        if has_geometry is False:
            self._set_status(
                f"Verbunden. Aktuelle Haltung: {holding} — keine Geometrie im Kataster (kein Zoom moeglich)."
            )
        else:
            self._set_status(f"Verbunden. Aktuelle Haltung: {holding}")

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
