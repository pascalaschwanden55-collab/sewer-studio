from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))


def laden(name: str):
    pfad = SCRIPTS / f"{name}.py"
    spec = importlib.util.spec_from_file_location(name, pfad)
    assert spec is not None and spec.loader is not None
    modul = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(modul)
    return modul


queue_modul = laden("bcc_pdf_precision_queue")
bericht_modul = laden("bcc_pdf_precision_bericht")


class BccPdfPrecisionQueueTests(unittest.TestCase):
    def test_oeffentlicher_fall_enthaelt_keine_vorgabe_fuer_den_pruefer(self) -> None:
        fall = {
            "fall_id": "abc", "haltung": "H1", "start_s": 10, "ende_s": 12,
            "peak_s": 11, "video": "v.mp4", "max_conf": 0.93,
            "ist_treffer": True,
        }

        sichtbar = queue_modul.oeffentlicher_fall(fall, 1, "fall.mp4")

        self.assertEqual(
            {"nummer", "fall_id", "haltung", "start_s", "ende_s", "clip"},
            set(sichtbar),
        )

    def test_es_werden_nur_ausgewertete_haltungen_der_messhaelfte_geladen(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            wurzel = Path(temp)
            bestand = wurzel / "bestand.json"
            lauf = wurzel / "lauf"
            haltungen = lauf / "haltungen"
            haltungen.mkdir(parents=True)
            bestand.write_text(
                json.dumps({
                    "sd": {"eintraege": [
                        {"haltung": "M", "haelfte": "messung"},
                        {"haltung": "K", "haelfte": "kalibrierung"},
                    ]},
                    "hd": {"eintraege": []},
                }), encoding="utf-8")
            for name in ("M", "K"):
                (haltungen / f"{name}.json").write_text(
                    json.dumps({
                        "haltung": name, "zustand": "ausgewertet", "video": "v.mp4",
                        "einzelbilder": [
                            {"zeit": 10.0, "meter": None, "geschaetzt": False, "conf": 0.8},
                        ],
                    }), encoding="utf-8")

            faelle, belege = queue_modul.vorschlaege_laden(bestand, lauf, 0.4, 0.7)

            self.assertEqual(["M"], [fall["haltung"] for fall in faelle])
            self.assertEqual(1, len(belege))


class BccPdfPrecisionBerichtTests(unittest.TestCase):
    def test_unsichere_faelle_werden_als_grenzen_ausgewiesen(self) -> None:
        queue = {
            "population_vorschlaege": 4,
            "voller_bestand": True,
            "faelle": [{"fall_id": str(i)} for i in range(4)],
        }
        review = {"urteile": {
            "0": {"urteil": "bogen"},
            "1": {"urteil": "bogen"},
            "2": {"urteil": "kein_bogen"},
            "3": {"urteil": "unsicher"},
        }}

        bericht = bericht_modul.precision_berechnen(queue, review)

        self.assertEqual(0.6667, bericht["precision_ohne_unsichere"])
        self.assertEqual(0.5, bericht["precision_untere_grenze"])
        self.assertEqual(0.75, bericht["precision_obere_grenze"])

    def test_unvollstaendige_review_liefert_keine_precision(self) -> None:
        queue = {"faelle": [{"fall_id": "a"}, {"fall_id": "b"}]}
        review = {"urteile": {"a": {"urteil": "bogen"}}}

        with self.assertRaisesRegex(ValueError, "1 Faelle offen"):
            bericht_modul.precision_berechnen(queue, review)

    def test_fremde_fall_id_wird_abgewiesen(self) -> None:
        queue = {"faelle": [{"fall_id": "a"}]}
        review = {"urteile": {"fremd": {"urteil": "bogen"}}}

        with self.assertRaisesRegex(ValueError, "nicht zur Warteschlange"):
            bericht_modul.precision_berechnen(queue, review)


if __name__ == "__main__":
    unittest.main()
