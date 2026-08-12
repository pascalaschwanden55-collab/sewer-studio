from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path


SCRIPTS = Path(__file__).resolve().parents[1]


def laden(name: str):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / f"{name}.py")
    assert spec is not None and spec.loader is not None
    modul = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(modul)
    return modul


queue = laden("osd_protokoll_qa_queue")
bericht = laden("osd_protokoll_qa_bericht")


class OsdProtokollQaTests(unittest.TestCase):
    def test_auswahl_nimmt_hoechstens_ein_bild_je_haltung(self) -> None:
        eintraege = [
            {"id": "b", "physische_haltung": "h1"},
            {"id": "a", "physische_haltung": "h1"},
            {"id": "c", "physische_haltung": "h2"},
        ]
        auswahl = queue.auswaehlen(eintraege, 2, "saat")
        self.assertEqual(2, len({e["physische_haltung"] for e in auswahl}))

    def test_bericht_trennt_gleich_abweichend_und_unleserlich(self) -> None:
        manifest = {"faelle": [
            {"nr": 1, "haltung": "H1", "soll_meter": 1.54},
            {"nr": 2, "haltung": "H2", "soll_meter": 2.0},
            {"nr": 3, "haltung": "H3", "soll_meter": 3.0},
        ]}
        ergebnis = bericht.auswerten(manifest, {1: "1,54", 2: "2.5", 3: "?"})
        self.assertEqual((1, 1, 1), (
            ergebnis["gleich"], ergebnis["abweichend"], ergebnis["unleserlich"]))

    def test_kleine_protokollrundung_ist_auf_zehntel_passend(self) -> None:
        manifest = {"faelle": [
            {"nr": 1, "haltung": "H1", "soll_meter": 9.2},
            {"nr": 2, "haltung": "H2", "soll_meter": 0.5},
        ]}

        ergebnis = bericht.auswerten(manifest, {1: "9.27", 2: "2.98"})

        self.assertEqual(1, ergebnis["auf_zehntel_passend"])
        self.assertEqual(1, ergebnis["grob_abweichend"])
        self.assertEqual(0.5, ergebnis["quote_auf_zehntel"])
        self.assertIn("nicht die Genauigkeit des Lesers", ergebnis["einordnung"])

    def test_offene_sichtprobe_liefert_keinen_bericht(self) -> None:
        manifest = {"faelle": [{"nr": 1, "haltung": "H1", "soll_meter": 1.0}]}
        with self.assertRaisesRegex(ValueError, "1 Bilder offen"):
            bericht.auswerten(manifest, {1: ""})


if __name__ == "__main__":
    unittest.main()
