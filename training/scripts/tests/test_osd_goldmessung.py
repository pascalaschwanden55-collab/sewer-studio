import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

from PIL import Image

# Direkt ueber den Pfad geladen statt "from training.scripts...":
# Das Paket SAM-2 bringt im Sidecar-Umfeld einen eigenen Ordner "training" mit und
# verdeckt damit den Projektordner. Alle uebrigen Tests hier scheitern deshalb schon
# beim Import — dieser laeuft ueberall.
_QUELLE = Path(__file__).resolve().parents[1] / "osd_goldmessung.py"
_spec = importlib.util.spec_from_file_location("osd_goldmessung", _QUELLE)
_modul = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_modul)
messe_satz = _modul.messe_satz


def _satz_bauen(ordner: Path, eintraege: list[dict]) -> Path:
    """Ein kleiner Goldsatz mit echten Bildern und passenden Hashes."""
    satz = ordner / "osd_test_v1"
    (satz / "frames").mkdir(parents=True)
    manifest = {"schema_version": 1, "name": satz.name, "eintraege": []}
    for nr, e in enumerate(eintraege, start=1):
        datei = f"f{nr:04d}.jpg"
        bild = satz / "frames" / datei
        Image.new("RGB", (32, 32), (nr * 7 % 256, 0, 0)).save(bild)
        manifest["eintraege"].append({
            "nr": nr,
            "datei": datei,
            "haltung": e.get("haltung", "1-2"),
            "bild_sha256": hashlib.sha256(bild.read_bytes()).hexdigest(),
            "meter": e["soll"],
        })
    (satz / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False), encoding="utf-8")
    return satz


class OsdGoldmessungTests(unittest.TestCase):
    def test_die_drei_zustaende_werden_getrennt_gezaehlt(self):
        with tempfile.TemporaryDirectory() as temp:
            satz = _satz_bauen(Path(temp), [
                {"soll": 1.2},   # wird richtig gelesen
                {"soll": 3.4},   # wird falsch gelesen
                {"soll": 5.6},   # wird nicht gelesen
            ])
            werte = iter([1.2, 9.9, None])
            ergebnis = messe_satz(satz, lambda _: {"meter": next(werte)})

        self.assertEqual(3, ergebnis["bilder"])
        self.assertEqual(1, ergebnis["richtig"])
        self.assertEqual(1, ergebnis["falsch"])
        self.assertEqual(1, ergebnis["nicht_gelesen"])

    # Der wichtigste Unterschied der ganzen Messung: Ein falscher Wert wandert
    # unbemerkt ins Protokoll, ein fehlender faellt sofort auf. Die beiden duerfen
    # nie in einer Zahl zusammenfallen.
    def test_ein_falscher_wert_zaehlt_nicht_als_nicht_gelesen(self):
        with tempfile.TemporaryDirectory() as temp:
            satz = _satz_bauen(Path(temp), [{"soll": 1.0}])
            ergebnis = messe_satz(satz, lambda _: {"meter": 2.0})

        self.assertEqual(1, ergebnis["falsch"])
        self.assertEqual(0, ergebnis["nicht_gelesen"])
        self.assertEqual(0.0, ergebnis["trefferquote"])

    def test_kleine_gleitkomma_reste_gelten_als_richtig(self):
        with tempfile.TemporaryDirectory() as temp:
            satz = _satz_bauen(Path(temp), [{"soll": 0.1}])
            ergebnis = messe_satz(satz, lambda _: {"meter": 0.1 + 1e-9})

        self.assertEqual(1, ergebnis["richtig"])

    def test_negative_meterstaende_werden_verglichen(self):
        with tempfile.TemporaryDirectory() as temp:
            satz = _satz_bauen(Path(temp), [{"soll": -0.1}])
            ergebnis = messe_satz(satz, lambda _: {"meter": -0.1})

        self.assertEqual(1, ergebnis["richtig"])

    # Der Goldsatz ist eingefroren. Veraenderte Bildbytes machen die Messung mit
    # frueheren Laeufen unvergleichbar — dann wird abgebrochen, nicht gemessen.
    def test_veraenderte_bildbytes_stoppen_die_messung(self):
        with tempfile.TemporaryDirectory() as temp:
            satz = _satz_bauen(Path(temp), [{"soll": 1.0}])
            Image.new("RGB", (32, 32), (9, 9, 9)).save(satz / "frames" / "f0001.jpg")

            with self.assertRaises(SystemExit) as fehler:
                messe_satz(satz, lambda _: {"meter": 1.0})

        self.assertIn("Bildbytes", str(fehler.exception))

    def test_ein_fehlendes_bild_stoppt_die_messung(self):
        with tempfile.TemporaryDirectory() as temp:
            satz = _satz_bauen(Path(temp), [{"soll": 1.0}])
            (satz / "frames" / "f0001.jpg").unlink()

            with self.assertRaises(SystemExit):
                messe_satz(satz, lambda _: {"meter": 1.0})


if __name__ == "__main__":
    unittest.main()
