from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from PIL import Image

from tools.EvalVisibilityReview.osd_handlabel_server import (
    SEITE,
    OsdHandlabelStore,
    create_server,
)


class OsdHandlabelStoreTests(unittest.TestCase):
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
                "boxen": [[10, 10, 20, 30], [22, 10, 32, 30]],
                "stil": "dunkel",
            }],
        }), encoding="utf-8")
        self.output = self.root / "review.json"

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
                 "bild_pfad": str(bild1), "boxen": [[1, 1, 5, 5], [6, 1, 10, 5]],
                 "stil": "dunkel"},
                {"id": "f2", "bild_sha256": sha2, "haltung": "20001-20002",
                 "bild_pfad": str(bild2), "boxen": [[1, 1, 5, 5]], "stil": "dunkel"},
            ],
        }), encoding="utf-8")
        output = root2 / "review.json"
        return OsdHandlabelStore(queue_root, output, "Pascal"), output

    # -----------------------------------------------------------------
    # Grundfluss: uebernehmen, unleserlich, boxen passen nicht.
    # -----------------------------------------------------------------

    def test_uebernommen_speichert_zeichenfolge_atomar(self) -> None:
        store = self.store()
        stand = store.entscheiden("f1", "uebernommen", "94", 0)

        self.assertEqual(0, stand["offen"])
        daten = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual("94", daten["entscheidungen"]["f1"]["zeichenfolge"])
        self.assertEqual("uebernommen", daten["entscheidungen"]["f1"]["aktion"])
        self.assertFalse(self.output.with_suffix(".json.tmp").exists())

    def test_unleserlich_braucht_keine_zeichenfolge(self) -> None:
        store = self.store()
        stand = store.entscheiden("f1", "unleserlich", "", 0)

        self.assertEqual(0, stand["offen"])
        daten = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual("unleserlich", daten["entscheidungen"]["f1"]["aktion"])
        self.assertNotIn("zeichenfolge", daten["entscheidungen"]["f1"])

    def test_boxen_passen_nicht_braucht_keine_zeichenfolge(self) -> None:
        store = self.store()
        store.entscheiden("f1", "boxen_passen_nicht", "", 0)

        daten = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual("boxen_passen_nicht", daten["entscheidungen"]["f1"]["aktion"])

    def test_unbekannte_aktion_wird_abgewiesen(self) -> None:
        store = self.store()
        with self.assertRaises(ValueError):
            store.entscheiden("f1", "irgendwas", "94", 0)

    # -----------------------------------------------------------------
    # Links-nach-rechts-Zuordnung: Zeichenzahl MUSS zur Boxenzahl passen.
    # -----------------------------------------------------------------

    def test_zu_wenige_zeichen_werden_abgewiesen(self) -> None:
        store = self.store()
        with self.assertRaises(ValueError):
            store.entscheiden("f1", "uebernommen", "9", 0)  # 2 Boxen, 1 Zeichen

    def test_zu_viele_zeichen_werden_abgewiesen(self) -> None:
        store = self.store()
        with self.assertRaises(ValueError):
            store.entscheiden("f1", "uebernommen", "945", 0)  # 2 Boxen, 3 Zeichen

    def test_leerzeichen_werden_vor_dem_zaehlen_entfernt(self) -> None:
        store = self.store()
        store.entscheiden("f1", "uebernommen", "9 4", 0)

        daten = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual("94", daten["entscheidungen"]["f1"]["zeichenfolge"])

    def test_nichts_wird_gespeichert_wenn_die_zahl_nicht_passt(self) -> None:
        store = self.store()
        try:
            store.entscheiden("f1", "uebernommen", "9", 0)
        except ValueError:
            pass
        self.assertFalse(self.output.exists())

    # -----------------------------------------------------------------
    # Nur Zeichen aus osd_meter.ZEICHEN sind erlaubt.
    # -----------------------------------------------------------------

    def test_unbekanntes_zeichen_wird_mit_seinem_namen_abgewiesen(self) -> None:
        store = self.store()
        with self.assertRaisesRegex(ValueError, "X"):
            store.entscheiden("f1", "uebernommen", "9X", 0)

    def test_alle_zeichen_aus_zeichen_werden_akzeptiert(self) -> None:
        from sidecar import osd_meter

        for zeichen in osd_meter.ZEICHEN:
            with self.subTest(zeichen=zeichen):
                if self.output.exists():
                    self.output.unlink()
                store = self.store()
                stand = store.entscheiden("f1", "uebernommen", zeichen * 2, 0)
                self.assertEqual(0, stand["offen"])

    # -----------------------------------------------------------------
    # Zwei-Tabs-Schutz ueber die Revision.
    # -----------------------------------------------------------------

    def test_veraltete_revision_wird_abgewiesen(self) -> None:
        store, output = self._store_mit_zwei_faellen()

        # Tab A speichert zuerst (Revision 0 -> 1).
        store.entscheiden("f1", "unleserlich", "", 0)

        # Tab B hatte die Seite vorher geladen (kennt noch Revision 0) und
        # versucht jetzt zu speichern - muss abgewiesen werden.
        with self.assertRaises(ValueError):
            store.entscheiden("f2", "unleserlich", "", 0)

        # Der zweite Fall bleibt unentschieden; Tab B muss neu laden.
        daten = json.loads(output.read_text(encoding="utf-8"))
        self.assertNotIn("f2", daten["entscheidungen"])

    def test_bereits_entschiedener_fall_wird_nicht_zweimal_gespeichert(self) -> None:
        store = self.store()
        store.entscheiden("f1", "uebernommen", "94", 0)
        with self.assertRaises(ValueError):
            store.entscheiden("f1", "unleserlich", "", 1)

    # -----------------------------------------------------------------
    # "Zurueck": zur zuletzt entschiedenen Karte zurueckspringen (Nachtrag
    # auf Wunsch von Pascal - ein Vertipper ist bei ~200 Karten mit je rund
    # acht Zeichen wahrscheinlich, und ein falsches Etikett ist teurer als
    # ein fehlendes).
    # -----------------------------------------------------------------

    def test_zurueck_von_erster_karte_tut_nichts(self) -> None:
        store = self.store()
        vorher = store.stand()

        nachher = store.zuruecknehmen(0)

        self.assertEqual(vorher["offen"], nachher["offen"])
        self.assertEqual(0, nachher["revision"])
        self.assertFalse(nachher["kann_zurueck"])
        # Nichts hat sich geaendert - keine Review-Datei angelegt.
        self.assertFalse(self.output.exists())

    def test_zurueck_stellt_eigene_vorherige_zeichenfolge_ins_feld(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "uebernommen", "94", 0)

        stand = store.zuruecknehmen(1)

        self.assertEqual("f1", stand["naechster"]["id"])
        self.assertEqual(
            {"aktion": "uebernommen", "zeichenfolge": "94"},
            stand["naechster"]["vorherige_eingabe"])

    def test_zurueck_bei_unleserlich_zeigt_hinweis_ohne_zeichenfolge(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "unleserlich", "", 0)

        stand = store.zuruecknehmen(1)

        self.assertEqual({"aktion": "unleserlich"}, stand["naechster"]["vorherige_eingabe"])

    def test_zurueck_bei_boxen_passen_nicht_zeigt_hinweis_ohne_zeichenfolge(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "boxen_passen_nicht", "", 0)

        stand = store.zuruecknehmen(1)

        self.assertEqual(
            {"aktion": "boxen_passen_nicht"}, stand["naechster"]["vorherige_eingabe"])

    def test_neue_entscheidung_nach_zurueck_ersetzt_die_alte(self) -> None:
        """Der Punkt, an dem sowas schiefgeht: pro Fall darf am Ende genau
        ein Eintrag in der Reviewdatei stehen, keine Dublette."""
        store, output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "uebernommen", "94", 0)
        store.zuruecknehmen(1)

        store.entscheiden("f1", "uebernommen", "77", 2)

        daten = json.loads(output.read_text(encoding="utf-8"))
        self.assertEqual(["f1"], list(daten["entscheidungen"]))
        self.assertEqual("77", daten["entscheidungen"]["f1"]["zeichenfolge"])

    def test_fortschritt_zaehlt_nach_zurueck_und_neuentscheidung_nicht_doppelt(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        self.assertEqual(2, store.stand()["offen"])

        store.entscheiden("f1", "uebernommen", "94", 0)
        self.assertEqual(1, store.stand()["offen"])

        store.zuruecknehmen(1)
        self.assertEqual(2, store.stand()["offen"])

        store.entscheiden("f1", "uebernommen", "77", 2)
        self.assertEqual(1, store.stand()["offen"])

    def test_zurueck_prueft_die_revision_wie_entscheiden(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "unleserlich", "", 0)  # Revision 0 -> 1

        # Ein Tab, der noch Revision 0 kennt, darf auch ueber "zurueck"
        # nicht mehr durchkommen - sonst waere der Zwei-Tabs-Schutz auf
        # diesem Weg ausgehebelt.
        with self.assertRaises(ValueError):
            store.zuruecknehmen(0)

    def test_zurueck_erhoeht_die_revision(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "unleserlich", "", 0)  # Revision 0 -> 1
        store.zuruecknehmen(1)  # Revision 1 -> 2

        # Wer jetzt noch Revision 1 kennt (z.B. ein zweiter Tab), ist
        # veraltet - auch fuer eine normale Entscheidung.
        with self.assertRaises(ValueError):
            store.entscheiden("f1", "unleserlich", "", 1)

    def test_mehrfaches_zurueck_geht_schrittweise_weiter_zurueck(self) -> None:
        store, _output = self._store_mit_zwei_faellen()
        store.entscheiden("f1", "uebernommen", "94", 0)   # Revision 0 -> 1
        store.entscheiden("f2", "unleserlich", "", 1)     # Revision 1 -> 2

        erster_rueck = store.zuruecknehmen(2)   # nimmt f2 zurueck
        self.assertEqual("f2", erster_rueck["naechster"]["id"])

        zweiter_rueck = store.zuruecknehmen(3)  # nimmt jetzt f1 zurueck
        self.assertEqual("f1", zweiter_rueck["naechster"]["id"])
        self.assertEqual(
            {"aktion": "uebernommen", "zeichenfolge": "94"},
            zweiter_rueck["naechster"]["vorherige_eingabe"])

    # -----------------------------------------------------------------
    # Resumability und Manipulationsschutz (wie das Layout-Review-Pendant).
    # -----------------------------------------------------------------

    def test_bereits_entschiedener_fall_bleibt_nach_neustart_erhalten(self) -> None:
        store = self.store()
        store.entscheiden("f1", "uebernommen", "94", 0)

        neu = self.store()
        self.assertEqual(0, neu.stand()["offen"])
        self.assertEqual(1, neu.stand()["revision"])

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
    # /stand liefert Boxen als Bildanteile - fuer die client-seitige
    # Ueberlagerung ohne Serverzeichnung.
    # -----------------------------------------------------------------

    def test_stand_liefert_boxen_als_bildanteile(self) -> None:
        store = self.store()
        naechster = store.stand()["naechster"]

        self.assertEqual(2, naechster["anzahl_boxen"])
        breite, hoehe = 100, 60
        erwartet_erste_box = [10 / breite, 10 / hoehe, 10 / breite, 20 / hoehe]
        for wert, soll in zip(naechster["boxen_anteil"][0], erwartet_erste_box):
            self.assertAlmostEqual(wert, soll)

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
        # Der Zurueck-Knopf muss verdrahtet sein ...
        self.assertIn("/zurueck", SEITE)
        self.assertIn("zurueck()", SEITE)
        # ... darf aber weiterhin keine Modell-/Vorlagenvermutung zeigen -
        # nur die eigene fruehere Eingabe (vorherige_eingabe).
        verboten = ("konfidenz", "vorschlag", "template", "modell", "leseweg",
                    "suggestion", "prediction")
        seite_klein = SEITE.lower()
        for wort in verboten:
            with self.subTest(wort=wort):
                self.assertNotIn(wort, seite_klein)

    def test_seite_bindet_revision_gegen_zwei_tabs(self) -> None:
        self.assertIn("revision", SEITE)

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


def _rmtree_ignore(pfad: Path) -> None:
    import shutil
    shutil.rmtree(pfad, ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
