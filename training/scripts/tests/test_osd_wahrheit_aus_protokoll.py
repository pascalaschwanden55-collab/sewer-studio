from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "osd_wahrheit_aus_protokoll.py"
SPEC = importlib.util.spec_from_file_location("osd_wahrheit_aus_protokoll", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
modul = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(modul)


def position(**werte) -> dict:
    basis = {
        "code": "BCCAY",
        "video_match": "exact_stem_same_folder",
        "video_counter_seconds": 64,
        "meter_start": 1.54,
        "video_path": "D:/Haltungen/H1/video.mp4",
        "source_pdf": "D:/Haltungen/H1/protokoll.pdf",
        "source_page": 1,
    }
    basis.update(werte)
    return basis


class OsdWahrheitAusProtokollTests(unittest.TestCase):
    def test_gesperrte_gegenrichtung_wird_ebenfalls_ausgeschlossen(self) -> None:
        scan = {"ergebnisse": [
            {"haltung": "100-200", "positionen": [position()]},
            {"haltung": "300-400", "positionen": [position()]},
        ]}

        faelle, zaehler = modul.faelle_aus_scan(scan, {"200-100"})

        self.assertEqual(["300-400"], [fall["haltung"] for fall in faelle])
        self.assertEqual(1, zaehler["haltung_gesperrt"])

    def test_unsichere_oder_unvollstaendige_positionen_werden_nicht_verwendet(self) -> None:
        scan = {"ergebnisse": [{"haltung": "H1", "positionen": [
            position(video_match="ambiguous_pdf_folder"),
            position(video_counter_seconds=None),
            position(),
        ]}]}

        faelle, zaehler = modul.faelle_aus_scan(scan, set())

        self.assertEqual(1, len(faelle))
        self.assertEqual(1, zaehler["video_unsicher"])
        self.assertEqual(1, zaehler["position_unvollstaendig"])

    def test_gleiche_haltung_landet_immer_im_selben_split(self) -> None:
        erster = modul.split_fuer_haltung("88218-88316", "saat")
        zweiter = modul.split_fuer_haltung("88316-88218", "saat")

        self.assertEqual(erster, zweiter)
        self.assertIn(erster, modul.SPLITS)

    def test_bildname_unterscheidet_zwei_befunde_an_gleicher_sekunde(self) -> None:
        erster = position(code="BCCAY", meter_start=1.54)
        erster["haltung"] = "H1"
        zweiter = position(code="BCCBY", meter_start=1.55)
        zweiter["haltung"] = "H1"

        self.assertNotEqual(modul.bildname(erster), modul.bildname(zweiter))

    def test_ziel_im_kundenordner_wird_erkannt(self) -> None:
        self.assertTrue(modul.liegt_unter(Path("D:/Haltungen/ausgabe"), Path("D:/Haltungen")))
        self.assertFalse(modul.liegt_unter(Path("C:/KI_BRAIN/ausgabe"), Path("D:/Haltungen")))


if __name__ == "__main__":
    unittest.main()
