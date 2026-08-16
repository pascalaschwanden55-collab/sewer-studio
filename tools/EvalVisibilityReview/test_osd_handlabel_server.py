from __future__ import annotations

import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from PIL import Image, ImageDraw, ImageFont

WURZEL = Path(__file__).resolve().parents[2]
if str(WURZEL / "training" / "scripts") not in sys.path:
    sys.path.insert(0, str(WURZEL / "training" / "scripts"))

import osd_crop  # noqa: E402

from tools.EvalVisibilityReview import osd_handlabel_server
from tools.EvalVisibilityReview.osd_handlabel_server import (
    SEITE,
    ZONEN_SKALA,
    OsdHandlabelStore,
    create_server,
)


def _rmtree_ignore(pfad: Path) -> None:
    import shutil
    shutil.rmtree(pfad, ignore_errors=True)


class OsdHandlabelStoreTests(unittest.TestCase):
    """Deckt Grundfluss, Revisionsschutz, Zurueck und Resumability ab.

    zeichen_in_kasten() wird hier bewusst auf zwei feste Boxen gemockt - die
    echte Segmentierung an echtem Bildinhalt ist eigene Sache von
    OsdHandlabelStoreEchteSegmentierungTests unten (und ausfuehrlich in
    sidecar/tests/test_osd_handlabel.py). Hier geht es um die Store-Logik:
    Kasten -> Boxen -> Zeichenzahl-Pruefung -> Speichern, unabhaengig davon,
    was die Segmentierung im Detail liefert.
    """

    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.bild_pfad = self.root / "bild.jpg"
        Image.new("RGB", (100, 60), (10, 10, 10)).save(self.bild_pfad, quality=90)
        self.bild_sha256 = hashlib.sha256(self.bild_pfad.read_bytes()).hexdigest()

        self.queue_root = self.root / "queue"
        self.queue_root.mkdir()
        (self.queue_root / "queue.json").write_text(json.dumps({
            "schema": "osd_handlabel_queue_v1",
            "faelle": [{
                "id": "f1",
                "bild_sha256": self.bild_sha256,
                "haltung": "10261-10262",
                "bild_pfad": str(self.bild_pfad),
                "stil": "dunkel",
            }],
        }), encoding="utf-8")
        self.output = self.root / "review.json"

        # Irgendein gueltiges, skaliertes Rechteck - der Inhalt ist wegen des
        # Mocks unten egal, nur Form (vier Zahlen) zaehlt.
        self.kasten = [10, 10, 60, 40]
        self._mock_boxen = [(30, 10, 40, 30), (42, 10, 52, 30)]
        patcher = mock.patch.object(
            osd_handlabel_server.osd_handlabel, "zeichen_in_kasten",
            new=lambda _bild, _kasten: self._mock_boxen)
        patcher.start()
        self.addCleanup(patcher.stop)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def store(self) -> OsdHandlabelStore:
        return OsdHandlabelStore(self.queue_root, self.output, "Pascal")

    def _store_mit_zwei_faellen(self) -> tuple[OsdHandlabelStore, Path]:
        """Eigener Wurzelordner mit zwei Faellen - fuer Tests, die eine
        Reihenfolge (erst f1, dann f2) brauchen, z.B. den Zurueck-Weg."""
        root2 = Path(tempfile.mkdtemp())
        self.addCleanup(lambda: _rmtree_ignore(root2))
        bild1 = root2 / "b1.jpg"
        Image.new("RGB", (80, 60), (1, 1, 1)).save(bild1)
        bild2 = root2 / "b2.jpg"
        Image.new("RGB", (80, 60), (2, 2, 2)).save(bild2)
        sha1 = hashlib.sha256(bild1.read_bytes()).hexdigest()
        sha2 = hashlib.sha256(bild2.read_bytes()).hexdigest()

        queue_root = root2 / "queue"
        queue_root.mkdir()
        (queue_root / "queue.json").write_text(json.dumps({
            "schema": "osd_handlabel_queue_v1",
            "faelle": [
                {"id": "f1", "bild_sha256": sha1, "haltung": "10261-10262",
                 "bild_pfad": str(bild1), "stil": "dunkel"},
                {"id": "f2", "bild_sha256": sha2, "haltung": "20001-20002",
                 "bild_pfad": str(bild2), "stil": "dunkel"},
            ],
        }), encoding="utf-8")
        output = root2 / "review.json"
        return OsdHandlabelStore(queue_root, output, "Pascal"), output

    # -----------------------------------------------------------------
    # Grundfluss: uebernehmen, unleserlich, boxen passen nicht.
    # -----------------------------------------------------------------

    def test_uebernommen_speichert_zeichenfolge_und_boxen_atomar(self) -> None:
        store = self.store()
        stand = store.entscheiden("f1", "uebernommen", "94", self.kasten, 0)

        self.assertEqual(0, stand["offen"])
        daten = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual("94", daten["entscheidungen"]["f1"]["zeichenfolge"])
        self.assertEqual("uebernommen", daten["entscheidungen"]["f1"]["aktion"])
        self.assertEqual(
            [[30, 10, 40, 30], [42, 10, 52, 30]],
            daten["entscheidungen"]["f1"]["boxen"])
        self.assertFalse(self.output.with_suffix(".json.tmp").exists())

    def test_unleserlich_braucht_weder_zeichenfolge_noch_kasten(self) -> None:
        store = self.store()
        stand = store.entscheiden("f1", "unleserlich", "", None, 0)

        self.assertEqual(0, stand["offen"])
        daten = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual("unleserlich", daten["entscheidungen"]["f1"]["aktion"])
        self.assertNotIn("zeichenfolge", daten["entscheidungen"]["f1"])
        self.assertNotIn("boxen", daten["entscheidungen"]["f1"])

    def test_boxen_passen_nicht_braucht_weder_zeichenfolge_noch_kasten(self) -> None:
        store = self.store()
        store.entscheiden("f1", "boxen_passen_nicht", "", None, 0)

        daten = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual("boxen_passen_nicht", daten["entscheidungen"]["f1"]["aktion"])

    def test_unbekannte_aktion_wird_abgewiesen(self) -> None:
        store = self.store()
        with self.assertRaises(ValueError):
            store.entscheiden("f1", "irgendwas", "94", self.kasten, 0)

    # -----------------------------------------------------------------
    # Kein Kasten -> kein Speichern (die neue Vorbedingung fuer "uebernommen").
    # -----------------------------------------------------------------

    def test_uebernehmen_ohne_kasten_wird_abgewiesen(self) -> None:
        store = self.store()
        with self.assertRaises(ValueError):
            store.entscheiden("f1", "uebernommen", "94", None, 0)
        self.assertFalse(self.output.exists())

    def test_uebernehmen_mit_ungueltigem_kasten_wird_abgewiesen(self) -> None:
        store = self.store()
        with self.assertRaises(ValueError):
            store.entscheiden("f1", "uebernommen", "94", [1, 2, 3], 0)  # nur 3 Werte

    def test_uebernehmen_mit_leerer_zeichenfolge_wird_abgewiesen(self) -> None:
        store = self.store()
        with self.assertRaises(ValueError):
            store.entscheiden("f1", "uebernommen", "", self.kasten, 0)

    # -----------------------------------------------------------------
    # Links-nach-rechts-Zuordnung: Zeichenzahl MUSS zur (server-seitig neu
    # berechneten) Boxenzahl passen.
    # -----------------------------------------------------------------

    def test_zu_wenige_zeichen_werden_abgewiesen(self) -> None:
        store = self.store()
        with self.assertRaises(ValueError):
            store.entscheiden("f1", "uebernommen", "9", self.kasten, 0)  # 2 Boxen, 1 Zeichen

    def test_zu_viele_zeichen_werden_abgewiesen(self) -> None:
        store = self.store()
        with self.assertRaises(ValueError):
            store.entscheiden("f1", "uebernommen", "945", self.kasten, 0)  # 2 Boxen, 3 Zeichen

    def test_leerzeichen_werden_vor_dem_zaehlen_entfernt(self) -> None:
        store = self.store()
        store.entscheiden("f1", "uebernommen", "9 4", self.kasten, 0)

        daten = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual("94", daten["entscheidungen"]["f1"]["zeichenfolge"])

    def test_nichts_wird_gespeichert_wenn_die_zahl_nicht_passt(self) -> None:
        store = self.store()
        try:
            store.entscheiden("f1", "uebernommen", "9", self.kasten, 0)
        except ValueError:
            pass
        self.assertFalse(self.output.exists())

    def test_boxen_werden_server_seitig_neu_berechnet_nicht_vom_client_uebernommen(self) -> None:
        """Der Client kann keine eigene Boxenliste einschmuggeln - nur den
        Kasten senden, aus dem der Server selbst segmentiert."""
        store = self.store()
        store.entscheiden("f1", "uebernommen", "94", self.kasten, 0)

        daten = json.loads(self.output.read_text(encoding="utf-8"))
        # Exakt die vom (gemockten) zeichen_in_kasten gelieferten Boxen -
        # unabhaengig davon, was im Request sonst noch gestanden haette.
        self.assertEqual(
            [[30, 10, 40, 30], [42, 10, 52, 30]],
            daten["entscheidungen"]["f1"]["boxen"])

    # -----------------------------------------------------------------
    # Nur Zeichen aus osd_meter.ZEICHEN sind erlaubt; Verstoesse werden
    # zusaetzlich fuer publizieren()'s Zusammenfassung vermerkt.
    # -----------------------------------------------------------------

    def test_unbekanntes_zeichen_wird_mit_seinem_namen_abgewiesen(self) -> None:
        store = self.store()
        with self.assertRaisesRegex(ValueError, "X"):
            store.entscheiden("f1", "uebernommen", "9X", self.kasten, 0)

    def test_unbekanntes_zeichen_wird_fuer_publizieren_vermerkt(self) -> None:
        store = self.store()
        try:
            store.entscheiden("f1", "uebernommen", "9X", self.kasten, 0)
        except ValueError:
            pass

        # Nicht als Entscheidung gespeichert - der Fall bleibt offen ...
        self.assertNotIn("f1", store.entscheidungen)
        self.assertEqual(0, store._revision)
        # ... aber persistiert, damit publizieren() den Materialverlust zaehlen kann.
        daten = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual(["X"], daten["zeichen_ausserhalb_satz"]["f1"])
        self.assertNotIn("f1", daten["entscheidungen"])

    def test_alle_unbekannten_zeichen_eines_versuchs_werden_vermerkt(self) -> None:
        """Nicht nur das erste in der Fehlermeldung genannte Zeichen -
        ein Versuch mit zwei unerlaubten Zeichen darf keines verlieren."""
        store = self.store()
        try:
            store.entscheiden("f1", "uebernommen", "+X", self.kasten, 0)
        except ValueError:
            pass

        daten = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual(["+", "X"], daten["zeichen_ausserhalb_satz"]["f1"])

    def test_alle_zeichen_aus_zeichen_werden_akzeptiert(self) -> None:
        from sidecar import osd_meter

        for zeichen in osd_meter.ZEICHEN:
            with self.subTest(zeichen=zeichen):
                if self.output.exists():
                    self.output.unlink()
                store = self.store()
                stand = store.entscheiden(
                    "f1", "uebernommen", zeichen * 2, self.kasten, 0)
                self.assertEqual(0, stand["offen"])

    # -----------------------------------------------------------------
    # Zwei-Tabs-Schutz ueber die Revision.
    # -----------------------------------------------------------------

    def test_veraltete_revision_wird_abgewiesen(self) -> None:
        store, output = self._store_mit_zwei_faellen()

        # Tab A speichert zuerst (Revision 0 -> 1).
        store.entscheiden("f1", "unleserlich", "", None, 0)

        # Tab B hatte die Seite vorher geladen (kennt noch Revision 0) und
        # versucht jetzt zu speichern - muss abgewiesen werden.
        with self.assertRaises(ValueError):
            store.entscheiden("f2", "unleserlich", "", None, 0)

        # Der zweite Fall bleibt unentschieden; Tab B muss neu laden.
        daten = json.loads(output.read_text(encoding="utf-8"))
        self.assertNotIn("f2", daten["entscheidungen"])

    def test_bereits_entschiedener_fall_wird_nicht_zweimal_gespeichert(self) -> None:
        store = self.store()
        store.entscheiden("f1", "uebernommen", "94", self.kasten, 0)
        with self.assertRaises(ValueError):
            store.entscheiden("f1", "unleserlich", "", None, 1)

    # -----------------------------------------------------------------
    # "Zurueck": zur zuletzt entschiedenen Karte zurueckspringen. Der Kasten
    # wird bewusst NICHT wiederhergestellt - nur die eigene Zeichenfolge.
    # -----------------------------------------------------------------

    def test_zurueck_von_erster_karte_tut_nichts(self) -> None:
        store = self.store()
        vorher = store.stand()

        nachher = store.zuruecknehmen(0)

        self.assertEqual(vorher["offen"], nachher["offen"])
        self.assertEqual(0, nachher["revision"])
        self.assertFalse(nachher["kann_zurueck"])
        self.assertFalse(self.output.exists())

    def test_zurueck_stellt_eigene_vorherige_zeichenfolge_ins_feld(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "uebernommen", "94", self.kasten, 0)

        stand = store.zuruecknehmen(1)

        self.assertEqual("f1", stand["naechster"]["id"])
        self.assertEqual(
            {"aktion": "uebernommen", "zeichenfolge": "94"},
            stand["naechster"]["vorherige_eingabe"])

    def test_zurueck_bei_unleserlich_zeigt_hinweis_ohne_zeichenfolge(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "unleserlich", "", None, 0)

        stand = store.zuruecknehmen(1)

        self.assertEqual({"aktion": "unleserlich"}, stand["naechster"]["vorherige_eingabe"])

    def test_zurueck_bei_boxen_passen_nicht_zeigt_hinweis_ohne_zeichenfolge(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "boxen_passen_nicht", "", None, 0)

        stand = store.zuruecknehmen(1)

        self.assertEqual(
            {"aktion": "boxen_passen_nicht"}, stand["naechster"]["vorherige_eingabe"])

    def test_neue_entscheidung_nach_zurueck_ersetzt_die_alte(self) -> None:
        """Der Punkt, an dem sowas schiefgeht: pro Fall darf am Ende genau
        ein Eintrag in der Reviewdatei stehen, keine Dublette."""
        store, output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "uebernommen", "94", self.kasten, 0)
        store.zuruecknehmen(1)

        store.entscheiden("f1", "uebernommen", "77", self.kasten, 2)

        daten = json.loads(output.read_text(encoding="utf-8"))
        self.assertEqual(["f1"], list(daten["entscheidungen"]))
        self.assertEqual("77", daten["entscheidungen"]["f1"]["zeichenfolge"])

    def test_fortschritt_zaehlt_nach_zurueck_und_neuentscheidung_nicht_doppelt(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        self.assertEqual(2, store.stand()["offen"])

        store.entscheiden("f1", "uebernommen", "94", self.kasten, 0)
        self.assertEqual(1, store.stand()["offen"])

        store.zuruecknehmen(1)
        self.assertEqual(2, store.stand()["offen"])

        store.entscheiden("f1", "uebernommen", "77", self.kasten, 2)
        self.assertEqual(1, store.stand()["offen"])

    def test_zurueck_prueft_die_revision_wie_entscheiden(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "unleserlich", "", None, 0)  # Revision 0 -> 1

        with self.assertRaises(ValueError):
            store.zuruecknehmen(0)

    def test_zurueck_erhoeht_die_revision(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "unleserlich", "", None, 0)  # Revision 0 -> 1
        store.zuruecknehmen(1)  # Revision 1 -> 2

        with self.assertRaises(ValueError):
            store.entscheiden("f1", "unleserlich", "", None, 1)

    def test_mehrfaches_zurueck_geht_schrittweise_weiter_zurueck(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "uebernommen", "94", self.kasten, 0)   # Revision 0 -> 1
        store.entscheiden("f2", "unleserlich", "", None, 1)            # Revision 1 -> 2

        erster_rueck = store.zuruecknehmen(2)   # nimmt f2 zurueck
        self.assertEqual("f2", erster_rueck["naechster"]["id"])

        zweiter_rueck = store.zuruecknehmen(3)  # nimmt jetzt f1 zurueck
        self.assertEqual("f1", zweiter_rueck["naechster"]["id"])
        self.assertEqual(
            {"aktion": "uebernommen", "zeichenfolge": "94"},
            zweiter_rueck["naechster"]["vorherige_eingabe"])

    # -----------------------------------------------------------------
    # Ein Erkennungsversuch mit unbekanntem Zeichen darf die "zurueck"-
    # Vorschau nicht verfaelschen: er ist keine Entscheidung.
    # -----------------------------------------------------------------

    def test_fehlgeschlagener_uebernehmen_versuch_zaehlt_nicht_als_vorherige_eingabe(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "unleserlich", "", None, 0)
        try:
            store.entscheiden("f2", "uebernommen", "9X", self.kasten, 1)
        except ValueError:
            pass

        stand = store.zuruecknehmen(1)
        self.assertEqual({"aktion": "unleserlich"}, stand["naechster"]["vorherige_eingabe"])

    # -----------------------------------------------------------------
    # Resumability und Manipulationsschutz (wie das Layout-Review-Pendant).
    # -----------------------------------------------------------------

    def test_bereits_entschiedener_fall_bleibt_nach_neustart_erhalten(self) -> None:
        store = self.store()
        store.entscheiden("f1", "uebernommen", "94", self.kasten, 0)

        neu = self.store()
        self.assertEqual(0, neu.stand()["offen"])
        self.assertEqual(1, neu.stand()["revision"])

    def test_zeichen_ausserhalb_satz_bleibt_nach_neustart_erhalten(self) -> None:
        store = self.store()
        try:
            store.entscheiden("f1", "uebernommen", "9X", self.kasten, 0)
        except ValueError:
            pass

        neu = self.store()
        self.assertEqual(["X"], neu._zeichen_ausserhalb_satz["f1"])

    def test_veraendertes_bild_sperrt_die_review(self) -> None:
        self.bild_pfad.write_bytes(self.bild_pfad.read_bytes() + b"geaendert")
        with self.assertRaises(SystemExit):
            self.store()

    def test_fremder_fall_in_vorhandener_review_wird_abgewiesen(self) -> None:
        queue_hash = hashlib.sha256(
            (self.queue_root / "queue.json").read_bytes()).hexdigest()
        self.output.write_text(json.dumps({
            "queue_sha256": queue_hash,
            "entscheidungen": {"fremd": {"aktion": "unleserlich"}},
        }), encoding="utf-8")
        with self.assertRaises(SystemExit):
            self.store()

    def test_review_einer_anderen_queue_wird_abgewiesen(self) -> None:
        self.output.write_text(json.dumps({
            "queue_sha256": "ff" * 32,
            "entscheidungen": {},
        }), encoding="utf-8")
        with self.assertRaises(SystemExit):
            self.store()

    # -----------------------------------------------------------------
    # /stand traegt keine vorausberechneten Boxen mehr (Rework: die
    # entstehen erst nach dem Kastenziehen, siehe vorschau()).
    # -----------------------------------------------------------------

    def test_stand_naechster_enthaelt_keine_boxen(self) -> None:
        store = self.store()
        naechster = store.stand()["naechster"]

        self.assertNotIn("boxen", naechster)
        self.assertNotIn("boxen_anteil", naechster)
        self.assertNotIn("anzahl_boxen", naechster)
        self.assertEqual({"id", "haltung", "vorherige_eingabe"}, set(naechster))

    # -----------------------------------------------------------------
    # vorschau(): reine Berechnung, speichert nichts.
    # -----------------------------------------------------------------

    def test_vorschau_liefert_boxen_ohne_zu_speichern(self) -> None:
        store = self.store()
        ergebnis = store.vorschau("f1", self.kasten)

        self.assertEqual(2, len(ergebnis["boxen"]))
        self.assertFalse(self.output.exists())
        self.assertEqual(0, store._revision)

    def test_vorschau_verweigert_unbekannten_fall(self) -> None:
        store = self.store()
        with self.assertRaises(ValueError):
            store.vorschau("unbekannt", self.kasten)

    def test_vorschau_verweigert_ungueltigen_kasten(self) -> None:
        store = self.store()
        with self.assertRaises(ValueError):
            store.vorschau("f1", [1, 2, 3])

    # -----------------------------------------------------------------
    # Nie eine KI-/Vorlagen-Vermutung anzeigen - weder im Text noch in
    # versteckten Feldern der Oberflaeche.
    # -----------------------------------------------------------------

    def test_seite_zeigt_keine_modell_oder_vorlagen_vermutung(self) -> None:
        verboten = ("konfidenz", "vorschlag", "template", "modell", "leseweg",
                    "suggestion", "prediction")
        seite_klein = SEITE.lower()
        for wort in verboten:
            with self.subTest(wort=wort):
                self.assertNotIn(wort, seite_klein)

    def test_seite_bindet_zurueck_button_ohne_maschinenvermutung(self) -> None:
        self.assertIn("/zurueck", SEITE)
        self.assertIn("zurueck()", SEITE)
        verboten = ("konfidenz", "vorschlag", "template", "modell", "leseweg",
                    "suggestion", "prediction")
        seite_klein = SEITE.lower()
        for wort in verboten:
            with self.subTest(wort=wort):
                self.assertNotIn(wort, seite_klein)

    def test_seite_bindet_revision_gegen_zwei_tabs(self) -> None:
        self.assertIn("revision", SEITE)

    def test_seite_zeigt_nur_die_zone_und_zieht_einen_kasten(self) -> None:
        # Der Kasten wird gezogen (Pointer-Events) und ueber /vorschau
        # segmentiert - kein Boxen-Overlay aus vorausberechneten Daten mehr.
        self.assertIn("/vorschau", SEITE)
        self.assertIn("pointerdown", SEITE)
        self.assertIn("pointerup", SEITE)
        self.assertIn("kasten", SEITE.lower())

    # -----------------------------------------------------------------
    # Server bindet nur an localhost.
    # -----------------------------------------------------------------

    def test_server_bindet_nur_an_localhost(self) -> None:
        store = self.store()
        server = create_server(store, 0)
        try:
            self.assertEqual("127.0.0.1", server.server_address[0])
        finally:
            server.server_close()


class OsdHandlabelStoreEchteSegmentierungTests(unittest.TestCase):
    """End-zu-Ende mit echtem, gerendertem Bildinhalt - KEIN Mock von
    zeichen_in_kasten(). Bestaetigt, dass Kasten-Skalierung, Vollbild-
    Ruecktransformation und die echte Segmentierung zusammenpassen."""

    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def _bild_kasten_und_store(self, text: str) -> tuple[OsdHandlabelStore, list[float]]:
        groesse = (720, 576)
        bild = Image.new("RGB", groesse, (15, 15, 15))
        zone = osd_crop.zonen_box(*groesse)
        zeichner = ImageDraw.Draw(bild)
        schrift = ImageFont.truetype(r"C:\Windows\Fonts\arial.ttf", 24)
        position = (zone[0] + 10, zone[1] + 10)
        zeichner.text(position, text, fill=(230, 230, 230), font=schrift)
        bbox = zeichner.textbbox(position, text, font=schrift)
        kasten_skaliert = [
            (bbox[0] - zone[0]) * ZONEN_SKALA - 4, (bbox[1] - zone[1]) * ZONEN_SKALA - 4,
            (bbox[2] - zone[0]) * ZONEN_SKALA + 4, (bbox[3] - zone[1]) * ZONEN_SKALA + 4,
        ]

        bild_pfad = self.root / "bild.jpg"
        bild.save(bild_pfad, quality=95)
        bild_sha256 = hashlib.sha256(bild_pfad.read_bytes()).hexdigest()

        queue_root = self.root / "queue"
        queue_root.mkdir()
        (queue_root / "queue.json").write_text(json.dumps({
            "schema": "osd_handlabel_queue_v1",
            "faelle": [{
                "id": "f1", "bild_sha256": bild_sha256, "haltung": "10261-10262",
                "bild_pfad": str(bild_pfad), "stil": "dunkel",
            }],
        }), encoding="utf-8")
        output = self.root / "review.json"
        return OsdHandlabelStore(queue_root, output, "Pascal"), kasten_skaliert

    def test_vorschau_findet_alle_gerenderten_zeichen(self) -> None:
        store, kasten = self._bild_kasten_und_store("9.42m")

        ergebnis = store.vorschau("f1", kasten)

        self.assertEqual(5, len(ergebnis["boxen"]))  # '9', '.', '4', '2', 'm'

    def test_uebernehmen_akzeptiert_bei_passender_zeichenzahl(self) -> None:
        store, kasten = self._bild_kasten_und_store("9.42m")

        stand = store.entscheiden("f1", "uebernommen", "9.42m", kasten, 0)

        self.assertEqual(0, stand["offen"])

    def test_zone_bild_bytes_ist_vierfach_skaliert(self) -> None:
        store, _kasten = self._bild_kasten_und_store("9.42m")

        daten = store.zone_bild_bytes("f1")

        import io
        with Image.open(io.BytesIO(daten)) as bild:
            zone = osd_crop.zonen_box(720, 576)
            erwartete_breite = (zone[2] - zone[0]) * ZONEN_SKALA
            erwartete_hoehe = (zone[3] - zone[1]) * ZONEN_SKALA
            self.assertEqual((erwartete_breite, erwartete_hoehe), bild.size)


if __name__ == "__main__":
    unittest.main()
