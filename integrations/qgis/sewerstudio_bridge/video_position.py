# -*- coding: utf-8 -*-
"""Live-Videoposition: zeigt in QGIS, wo das laufende Kanal-TV-Video gerade steht.

Aufbau
------
SewerStudio liefert unter /qgis/video_position.json die aktuelle Position als
Meterwert (und optional direkt als Koordinate). Dieses Modul holt den Wert in
einem eigenen, schnellen Takt (Standard 250 ms) und zeichnet daraus einen
Marker auf die Karte - ohne Layer und ohne Datei, damit nichts im Projektbaum
entsteht und das Zeichnen fluessig bleibt.

Meter -> Koordinate wird hier gerechnet, nicht in SewerStudio: die Geometrie der
aktiven Haltung liegt bereits als Layer "SewerStudio - Aktuelle Haltung" vor.
SewerStudio muss also nur wissen, bei welchem Meter das Video steht.

Endpunkt (GET, JSON):
    {
      "haltung":  "80475-80462",          # Pflicht: welche Haltung laeuft
      "meter":    12.4,                    # Pflicht: Position ab Startschacht
      "zeit":     "00:01:23.4",            # optional, nur fuer die Anzeige
      "laenge":   31.5,                    # optional: Bezugslaenge des Videos
      "richtung": "in_fliessrichtung",     # optional, sonst aus dem Layer
      "playing":  true,                    # optional
      "x": 2692813.7, "y": 1192416.4       # optional: fertige Koordinate LV95
    }
Fehlt der Endpunkt (404), bleibt das Modul still - die uebrige Bruecke laeuft weiter.
"""

import json
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import Request, urlopen

from qgis.PyQt.QtCore import Qt, QTimer
from qgis.PyQt.QtGui import QColor
from qgis.PyQt.QtWidgets import (QCheckBox, QGroupBox, QHBoxLayout, QLabel,
                                 QPushButton, QSpinBox, QVBoxLayout)
from qgis.core import (QgsGeometry, QgsPointXY, QgsProject, QgsWkbTypes,
                       QgsCoordinateTransform, QgsRectangle)
from qgis.gui import QgsRubberBand, QgsMapToolEmitPoint

from .bridge_http import TOKEN_HEADER, read_bridge_token

ENDPUNKT = "/qgis/video_position.json"
SEEK_ENDPUNKT = "/qgis/seek"
AKTIVE_HALTUNG_LAYER = "SewerStudio - Aktuelle Haltung"

FARBE_GEFAHREN = QColor(0, 150, 255)      # bereits abgefahrener Teil
FARBE_MARKER = QColor(255, 60, 0)         # aktuelle Position
FARBE_REST = QColor(120, 120, 120, 120)   # noch nicht gesehener Teil


def _ist_loopback(url):
    try:
        teile = urlsplit(url)
    except ValueError:
        return False
    return teile.scheme == "http" and (teile.hostname or "") in ("127.0.0.1", "localhost", "::1")


def hole_position(base_url, timeout=0.8):
    """Liest den Positions-Endpunkt. Gibt (daten, warnung) zurueck; 404 ist still."""
    base = (base_url or "").strip().rstrip("/")
    if not _ist_loopback(base):
        return None, "Bridge-URL muss lokal sein (http://127.0.0.1:8765)."
    kopf = {"Accept": "application/json"}
    token = read_bridge_token()
    if token:
        kopf[TOKEN_HEADER] = token
    try:
        with urlopen(Request(base + ENDPUNKT, headers=kopf), timeout=timeout) as antwort:
            if antwort.status != 200:
                return None, None
            return json.loads(antwort.read().decode("utf-8")), None
    except HTTPError as ex:
        if ex.code == 404:
            return None, None          # Endpunkt noch nicht gebaut - kein Fehler
        if ex.code == 401:
            return None, "Videoposition: Anmeldung fehlgeschlagen (Token)."
        return None, "Videoposition: HTTP %s" % ex.code
    except (URLError, TimeoutError, OSError, ValueError):
        return None, None              # SewerStudio laeuft gerade nicht


def _fehlertext(fehler):
    """Klartext aus der Fehlerantwort, oder None. Wirft nie."""
    try:
        rumpf = fehler.read()
    except Exception:
        return None
    if not rumpf:
        return None
    try:
        wert = json.loads(rumpf.decode("utf-8", "replace")).get("error")
    except (ValueError, AttributeError):
        return None
    wert = (wert or "").strip()
    return wert or None


def sende_sprung(base_url, haltung, meter, timeout=1.0):
    """Meldet SewerStudio, im Video an diesen Meter zu springen."""
    base = (base_url or "").strip().rstrip("/")
    if not _ist_loopback(base):
        return False, "Bridge-URL muss lokal sein."
    kopf = {"Content-Type": "application/json"}
    token = read_bridge_token()
    if token:
        kopf[TOKEN_HEADER] = token
    daten = json.dumps({"haltung": haltung, "meter": round(float(meter), 2)}).encode("utf-8")
    try:
        with urlopen(Request(base + SEEK_ENDPUNKT, data=daten, headers=kopf, method="POST"),
                     timeout=timeout) as antwort:
            return antwort.status in (200, 202, 204), None
    except HTTPError as ex:
        # SewerStudio legt den Grund als Klartext bei ("es laeuft kein Video",
        # "im Video laeuft eine andere Haltung"). Den zeigen wir, statt eine
        # Nummer zu melden oder zu raten, der Endpunkt fehle.
        grund = _fehlertext(ex)
        if grund:
            return False, grund
        if ex.code == 404:
            return False, "SewerStudio kennt %s noch nicht." % SEEK_ENDPUNKT
        return False, "Sprung fehlgeschlagen (HTTP %s)." % ex.code
    except (URLError, TimeoutError, OSError) as ex:
        return False, "SewerStudio nicht erreichbar (%s)." % ex


class VideoPositionAnzeige:
    """Zeichnet die Videoposition auf die Karte und haelt sie im Takt aktuell.

    Bewusst ohne eigenen Layer: drei Gummibaender auf dem Kartenleinwand.
    Das erzeugt keinen Eintrag im Projektbaum, kostet kein Datei-I/O und
    ueberlebt jedes Neuladen der uebrigen Brueckenebenen.
    """

    def __init__(self, iface, url_getter, log=None):
        self.iface = iface
        self.canvas = iface.mapCanvas()
        self._url = url_getter          # Funktion, liefert die Bridge-URL
        self._log = log or (lambda _m: None)

        self.timer = QTimer()
        self.timer.timeout.connect(self._takt)

        self.band_gefahren = QgsRubberBand(self.canvas, QgsWkbTypes.LineGeometry)
        self.band_gefahren.setColor(FARBE_GEFAHREN)
        self.band_gefahren.setWidth(5)
        self.band_rest = QgsRubberBand(self.canvas, QgsWkbTypes.LineGeometry)
        self.band_rest.setColor(FARBE_REST)
        self.band_rest.setWidth(5)
        self.marker = QgsRubberBand(self.canvas, QgsWkbTypes.PointGeometry)
        self.marker.setColor(FARBE_MARKER)
        self.marker.setIcon(QgsRubberBand.ICON_CIRCLE)
        self.marker.setIconSize(16)
        self.marker.setWidth(4)

        self.folgen = True
        self.letzte = {}
        self._sim = None                # Simulationszustand (Testbetrieb)
        self._status = ""

    # ------------------------------------------------------------------ Takt
    def starten(self, ms=250):
        if not self.timer.isActive():
            self.timer.start(max(50, int(ms)))

    def stoppen(self):
        if self.timer.isActive():
            self.timer.stop()
        self.leeren()

    def laeuft(self):
        return self.timer.isActive()

    def leeren(self):
        self.band_gefahren.reset(QgsWkbTypes.LineGeometry)
        self.band_rest.reset(QgsWkbTypes.LineGeometry)
        self.marker.reset(QgsWkbTypes.PointGeometry)

    def _takt(self):
        if self._sim is not None:
            daten = self._sim_schritt()
            warnung = None
        else:
            daten, warnung = hole_position(self._url())
        if warnung:
            self._log(warnung)
        if not daten:
            self._status = "keine Position gemeldet"
            self.leeren()
            return
        self.zeichnen(daten)

    # --------------------------------------------------------------- Zeichnen
    def _haltungs_geometrie(self, haltung):
        """Geometrie der aktiven Haltung aus dem Bruecken-Layer, in Kartenkoordinaten."""
        for layer in QgsProject.instance().mapLayers().values():
            if layer.name() != AKTIVE_HALTUNG_LAYER:
                continue
            felder = [f.name() for f in layer.fields()]
            for f in layer.getFeatures():
                if haltung and "haltung" in felder and f["haltung"] != haltung:
                    continue
                g = QgsGeometry(f.geometry())
                if g.isEmpty():
                    continue
                if layer.crs() != self.canvas.mapSettings().destinationCrs():
                    tr = QgsCoordinateTransform(layer.crs(),
                                                self.canvas.mapSettings().destinationCrs(),
                                                QgsProject.instance())
                    g.transform(tr)
                richtung = f["richtung"] if "richtung" in felder else None
                return g, richtung
        return None, None

    def zeichnen(self, daten):
        haltung = daten.get("haltung")
        meter = daten.get("meter")
        geom, richtung_layer = self._haltungs_geometrie(haltung)
        richtung = daten.get("richtung") or richtung_layer or "in_fliessrichtung"

        punkt = None
        if daten.get("x") is not None and daten.get("y") is not None:
            punkt = QgsPointXY(float(daten["x"]), float(daten["y"]))
        elif geom is not None and meter is not None:
            punkt = self._punkt_auf_haltung(geom, float(meter), daten.get("laenge"), richtung)

        self.leeren()
        if geom is not None and punkt is not None:
            strecke = self._teilstueck(geom, punkt, richtung)
            if strecke is not None:
                self.band_rest.setToGeometry(geom, None)
                self.band_gefahren.setToGeometry(strecke, None)
        if punkt is None:
            self._status = "Haltung %s: keine Geometrie" % (haltung or "?")
            return

        self.marker.addPoint(punkt)
        zeit = daten.get("zeit") or ""
        self._status = "%s  |  %.2f m%s" % (haltung or "?", float(meter or 0),
                                            ("  |  " + str(zeit)) if zeit else "")
        self.letzte = dict(daten)
        self.letzte["_punkt"] = punkt
        if self.folgen:
            self._karte_nachfuehren(punkt)

    @staticmethod
    def _punkt_auf_haltung(geom, meter, laenge_video, richtung):
        laenge_geom = geom.length()
        if laenge_geom <= 0:
            return None
        strecke = meter
        # Videolaenge und Katasterlaenge weichen fast immer leicht ab -> anteilig umrechnen.
        try:
            lv = float(laenge_video) if laenge_video else 0.0
        except (TypeError, ValueError):
            lv = 0.0
        if lv > 0:
            strecke = max(0.0, min(1.0, meter / lv)) * laenge_geom
        strecke = max(0.0, min(laenge_geom, strecke))
        if str(richtung).startswith("gegen"):
            strecke = laenge_geom - strecke
        p = geom.interpolate(strecke)
        return p.asPoint() if p and not p.isEmpty() else None

    @staticmethod
    def _stuetzpunkte(geom):
        pts = geom.asPolyline()
        if not pts:
            multi = geom.asMultiPolyline()
            pts = multi[0] if multi else []
        return pts

    @classmethod
    def _teilstueck(cls, geom, punkt, richtung):
        """Linienstueck vom Aufnahmestart bis zur aktuellen Position.

        Bewusst von Hand gerechnet: QgsGeometry.curveSubstring gibt es nicht in
        jeder QGIS-Version, asPolyline dagegen ueberall.
        """
        import math
        pts = cls._stuetzpunkte(geom)
        if len(pts) < 2:
            return None
        d = geom.lineLocatePoint(QgsGeometry.fromPointXY(punkt))
        if d < 0:
            return None
        gegen = str(richtung).startswith("gegen")
        von, bis = (d, geom.length()) if gegen else (0.0, d)

        teil = []
        lauf = 0.0
        for a, b in zip(pts, pts[1:]):
            seg = math.hypot(b.x() - a.x(), b.y() - a.y())
            if seg <= 0:
                continue
            ende = lauf + seg
            if ende < von or lauf > bis:
                lauf = ende
                continue
            t0 = max(0.0, (von - lauf) / seg)
            t1 = min(1.0, (bis - lauf) / seg)
            p0 = QgsPointXY(a.x() + (b.x() - a.x()) * t0, a.y() + (b.y() - a.y()) * t0)
            p1 = QgsPointXY(a.x() + (b.x() - a.x()) * t1, a.y() + (b.y() - a.y()) * t1)
            if not teil:
                teil.append(p0)
            teil.append(p1)
            lauf = ende
        if len(teil) < 2:
            return None
        return QgsGeometry.fromPolylineXY(teil)

    def _karte_nachfuehren(self, punkt):
        ext = self.canvas.extent()
        rand_x = ext.width() * 0.18
        rand_y = ext.height() * 0.18
        innen = QgsRectangle(ext.xMinimum() + rand_x, ext.yMinimum() + rand_y,
                             ext.xMaximum() - rand_x, ext.yMaximum() - rand_y)
        if innen.contains(punkt):
            return
        neu = QgsRectangle(punkt.x() - ext.width() / 2, punkt.y() - ext.height() / 2,
                           punkt.x() + ext.width() / 2, punkt.y() + ext.height() / 2)
        self.canvas.setExtent(neu)
        self.canvas.refresh()

    def status(self):
        return self._status

    # ------------------------------------------------------------ Simulation
    def simulation_starten(self, haltung=None, dauer_s=30.0):
        """Faehrt die aktive Haltung einmal ab - zum Pruefen ohne SewerStudio-Endpunkt."""
        import time as _t
        geom, richtung = self._haltungs_geometrie(haltung)
        if geom is None:
            return False
        if not haltung:
            for layer in QgsProject.instance().mapLayers().values():
                if layer.name() == AKTIVE_HALTUNG_LAYER:
                    f = next(layer.getFeatures(), None)
                    if f and "haltung" in [x.name() for x in layer.fields()]:
                        haltung = f["haltung"]
                    break
        self._sim = {"start": _t.time(), "dauer": float(dauer_s),
                     "haltung": haltung, "laenge": geom.length(), "richtung": richtung}
        return True

    def simulation_stoppen(self):
        self._sim = None

    def _sim_schritt(self):
        import time as _t
        s = self._sim
        anteil = ((_t.time() - s["start"]) % s["dauer"]) / s["dauer"]
        meter = anteil * s["laenge"]
        return {"haltung": s["haltung"], "meter": meter, "laenge": s["laenge"],
                "richtung": s["richtung"], "zeit": "%05.1f s (Simulation)" % (anteil * s["dauer"]),
                "playing": True}


class SprungWerkzeug(QgsMapToolEmitPoint):
    """Klick auf die Haltung -> SewerStudio springt im Video an diese Stelle."""

    def __init__(self, canvas, anzeige, url_getter, meldung=None):
        super().__init__(canvas)
        self.canvas = canvas
        self.anzeige = anzeige
        self._url = url_getter
        self._meldung = meldung or (lambda _t: None)

    def canvasReleaseEvent(self, e):
        punkt = self.toMapCoordinates(e.pos())
        haltung = (self.anzeige.letzte or {}).get("haltung")
        geom, richtung = self.anzeige._haltungs_geometrie(haltung)
        if geom is None:
            self._meldung("Keine aktive Haltung geladen.")
            return
        if not haltung:
            self._meldung("Kein Haltungsname bekannt.")
            return
        d = geom.lineLocatePoint(QgsGeometry.fromPointXY(punkt))
        if d < 0:
            self._meldung("Punkt liegt nicht auf der Haltung.")
            return
        laenge_geom = geom.length()
        laenge_video = (self.anzeige.letzte or {}).get("laenge") or laenge_geom
        if str(richtung).startswith("gegen"):
            d = laenge_geom - d
        meter = (d / laenge_geom) * float(laenge_video) if laenge_geom else 0.0
        ok, warnung = sende_sprung(self._url(), haltung, meter)
        if ok:
            self._meldung("Video: Sprung auf %.2f m" % meter)
        else:
            self._meldung(warnung or "Sprung nicht moeglich.")


def baue_bedienfeld(parent, anzeige, url_getter, meldung, settings=None, praefix=None):
    """Kompaktes Bedienfeld, das im Bruecken-Dock unten angehaengt wird.

    settings/praefix sind optional: Sind sie gesetzt, merkt sich das Feld
    "aktiv", Takt und "Karte folgt" ueber QGIS-Sitzungen hinweg. Ohne sie
    verhaelt es sich wie frueher (beim Start aus).
    """
    box = QGroupBox("Videoposition (live)", parent)
    lay = QVBoxLayout(box)
    lay.setSpacing(4)

    zeile = QHBoxLayout()
    schalter = QCheckBox("aktiv")
    takt = QSpinBox(); takt.setRange(50, 2000); takt.setSingleStep(50)
    takt.setValue(250); takt.setSuffix(" ms")
    folgen = QCheckBox("Karte folgt"); folgen.setChecked(True)
    zeile.addWidget(schalter); zeile.addWidget(takt); zeile.addWidget(folgen); zeile.addStretch()
    lay.addLayout(zeile)

    zeile2 = QHBoxLayout()
    b_sprung = QPushButton("In Karte klicken = Video springt")
    b_sprung.setCheckable(True)
    b_test = QPushButton("Test (Simulation)")
    b_test.setCheckable(True)
    zeile2.addWidget(b_sprung); zeile2.addWidget(b_test)
    lay.addLayout(zeile2)

    label = QLabel("aus")
    label.setStyleSheet("color:#555;")
    lay.addWidget(label)

    werkzeug = {"tool": None, "vorher": None}

    def an_aus(zustand):
        if zustand:
            anzeige.starten(takt.value())
            label.setText("laeuft ...")
        else:
            b_test.setChecked(False)
            anzeige.simulation_stoppen()
            anzeige.stoppen()
            label.setText("aus")
    schalter.toggled.connect(an_aus)

    def takt_geaendert(v):
        if anzeige.laeuft():
            anzeige.timer.setInterval(max(50, v))
    takt.valueChanged.connect(takt_geaendert)
    folgen.toggled.connect(lambda v: setattr(anzeige, "folgen", bool(v)))

    def sprung_modus(an):
        canvas = anzeige.canvas
        if an:
            werkzeug["vorher"] = canvas.mapTool()
            werkzeug["tool"] = SprungWerkzeug(canvas, anzeige, url_getter, meldung)
            canvas.setMapTool(werkzeug["tool"])
        else:
            if werkzeug["tool"] is not None:
                canvas.unsetMapTool(werkzeug["tool"])
                werkzeug["tool"] = None
            if werkzeug["vorher"] is not None:
                canvas.setMapTool(werkzeug["vorher"])
    b_sprung.toggled.connect(sprung_modus)

    def test_modus(an):
        if an:
            if not anzeige.simulation_starten():
                meldung("Simulation: keine aktive Haltung mit Geometrie gefunden.")
                b_test.setChecked(False)
                return
            schalter.setChecked(True)
            label.setText("Simulation laeuft")
        else:
            anzeige.simulation_stoppen()
    b_test.toggled.connect(test_modus)

    zeiger = QTimer(box)
    zeiger.timeout.connect(lambda: label.setText(anzeige.status() or "keine Position")
                           if anzeige.laeuft() else None)
    zeiger.start(400)
    box._zeiger = zeiger

    # Gespeicherten Stand ERST JETZT setzen — nach allen connect-Aufrufen, damit
    # das Setzen die Anzeige wirklich startet und nicht nur das Haekchen malt.
    #
    # Ohne dieses Merken war "aktiv" nach jedem QGIS-Start wieder aus, und die
    # Videoposition blieb still: Sie sah kaputt aus, obwohl alles lief. Der
    # Sprung-Modus wird bewusst NICHT wiederhergestellt — er uebernimmt das
    # Kartenwerkzeug, und das gehoert nicht ungefragt beim Start passiert.
    if settings is not None and praefix:
        def _bool(name, standard):
            wert = settings.value(f"{praefix}/{name}", standard)
            if isinstance(wert, bool):
                return wert
            return str(wert).strip().lower() in ("true", "1", "ja", "yes")

        try:
            gespeicherter_takt = int(settings.value(f"{praefix}/videoTaktMs", takt.value()))
        except (TypeError, ValueError):
            gespeicherter_takt = takt.value()
        takt.setValue(max(takt.minimum(), min(takt.maximum(), gespeicherter_takt)))
        folgen.setChecked(_bool("videoKarteFolgt", True))
        schalter.setChecked(_bool("videoAktiv", False))

        def _merken():
            settings.setValue(f"{praefix}/videoAktiv", schalter.isChecked())
            settings.setValue(f"{praefix}/videoKarteFolgt", folgen.isChecked())
            settings.setValue(f"{praefix}/videoTaktMs", takt.value())
        schalter.toggled.connect(lambda _=None: _merken())
        folgen.toggled.connect(lambda _=None: _merken())
        takt.valueChanged.connect(lambda _=None: _merken())
    else:
        # Ohne Speicher gilt weiterhin die Vorgabe des Widgets.
        anzeige.folgen = folgen.isChecked()

    return box
