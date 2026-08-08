from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "bcc_schwellenkurve.py"
SPEC = importlib.util.spec_from_file_location("bcc_schwellenkurve", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
kurve_modul = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(kurve_modul)

VERGLEICH_PATH = Path(__file__).resolve().parents[1] / "bcc_video_messung_vergleich.py"
VERGLEICH_SPEC = importlib.util.spec_from_file_location("bcc_video_messung_vergleich", VERGLEICH_PATH)
assert VERGLEICH_SPEC is not None and VERGLEICH_SPEC.loader is not None
vergleich_modul = importlib.util.module_from_spec(VERGLEICH_SPEC)
VERGLEICH_SPEC.loader.exec_module(vergleich_modul)


def gruppe(haltung: str, start: int, ende: int, conf: float, ki: str = "bogen") -> dict:
    return {
        "id": f"{haltung}|{start}-{ende}",
        "haltung": haltung,
        "start_s": start,
        "ende_s": ende,
        "max_conf": conf,
        "ki_einstufung": ki,
        "kachel": None,
        "urteil_pascal": None,
    }


class TemplateFuellenTests(unittest.TestCase):
    def test_urteile_werden_ueber_haltung_und_zeitfenster_verbunden(self) -> None:
        template = {"gruppen": [gruppe("H1", 100, 110, 0.8), gruppe("H2", 20, 20, 0.3)]}
        urteile = {"H1|100-110": "bogen", "H2|20-20": "kein_bogen"}

        gefuellt, fehlend = kurve_modul.template_fuellen(template, urteile)

        self.assertEqual([], fehlend)
        self.assertEqual(["bogen", "kein_bogen"], [g["urteil_pascal"] for g in gefuellt])

    def test_eine_gruppe_ohne_urteil_wird_gemeldet_statt_geraten(self) -> None:
        template = {"gruppen": [gruppe("H1", 100, 110, 0.8)]}

        gefuellt, fehlend = kurve_modul.template_fuellen(template, {})

        self.assertEqual(["H1|100-110"], fehlend)
        self.assertIsNone(gefuellt[0]["urteil_pascal"])

    def test_schachtanfang_wird_erkannt(self) -> None:
        template = {
            "gruppen": [
                gruppe("H1", 0, 3, 0.9),                    # ueber die Sekunde
                gruppe("H2", 300, 305, 0.9, ki="schacht"),  # ueber die Einstufung
                gruppe("H3", 300, 305, 0.9),
            ]
        }

        gefuellt, _ = kurve_modul.template_fuellen(template, {})

        self.assertEqual([True, True, False], [g["ist_schachtanfang"] for g in gefuellt])


class KurveTests(unittest.TestCase):
    def setUp(self) -> None:
        self.gefuellt = [
            {"haltung": "H1", "start_s": 100, "ende_s": 110, "max_conf": 0.80,
             "urteil_pascal": "bogen", "ist_schachtanfang": False},
            {"haltung": "H1", "start_s": 200, "ende_s": 205, "max_conf": 0.40,
             "urteil_pascal": "kein_bogen", "ist_schachtanfang": False},
            {"haltung": "H1", "start_s": 0, "ende_s": 3, "max_conf": 0.90,
             "urteil_pascal": "kein_bogen", "ist_schachtanfang": True},
            {"haltung": "H2", "start_s": 50, "ende_s": 55, "max_conf": 0.20,
             "urteil_pascal": "unsicher", "ist_schachtanfang": False},
        ]

    def test_schachtanfaenge_zaehlen_nicht_als_fehlalarm(self) -> None:
        # Sie sind durch Trimmung entfernbar und wuerden die Kurve verfaelschen.
        zeile = next(z for z in kurve_modul.kurve(self.gefuellt, [0.9]) if z["conf"] == 0.50)

        self.assertEqual(0, zeile["falsch"])
        self.assertEqual(1.0, zeile["precision"])

    def test_unsichere_gruppen_zaehlen_weder_richtig_noch_falsch(self) -> None:
        zeile = next(z for z in kurve_modul.kurve(self.gefuellt, [0.9]) if z["conf"] == 0.20)

        self.assertEqual(1, zeile["unsicher"])
        self.assertEqual(1, zeile["falsch"])          # nur die 0.40-Gruppe
        self.assertEqual(2, zeile["richtig"])         # Protokollbogen + bestaetigter Bogen

    def test_hoehere_schwelle_verliert_recall_und_gewinnt_precision(self) -> None:
        zeilen = {z["conf"]: z for z in kurve_modul.kurve(self.gefuellt, [0.9, 0.30])}

        self.assertEqual("2/2", zeilen[0.10]["recall_protokoll"])
        self.assertEqual("1/2", zeilen[0.50]["recall_protokoll"])
        self.assertLess(zeilen[0.10]["precision"], zeilen[0.50]["precision"])


class ProtokollTrefferTests(unittest.TestCase):
    def test_ein_nie_gefundener_befund_zaehlt_als_konfidenz_null(self) -> None:
        bericht = {
            "ergebnisse": [
                {
                    "haltung": "H1",
                    "befunde_pruefbar": 2,
                    "gruppen": [{"ist_treffer": True, "max_conf": 0.6}],
                }
            ]
        }

        self.assertEqual([0.6, 0.0], kurve_modul.protokoll_treffer(bericht))

    def test_nicht_pruefbare_befunde_erhoehen_den_nenner_nicht(self) -> None:
        bericht = {
            "ergebnisse": [
                {
                    "haltung": "H1",
                    "befunde_pruefbar": 0,
                    "gruppen": [{"ist_treffer": True, "max_conf": 0.6}],
                }
            ]
        }

        self.assertEqual([], kurve_modul.protokoll_treffer(bericht))


class UrteilUebertragenTests(unittest.TestCase):
    def setUp(self) -> None:
        self.urteile = {
            "a": {"haltung": "H1", "start_s": 100, "ende_s": 110, "urteil": "kein_bogen"},
            "b": {"haltung": "H1", "start_s": 300, "ende_s": 310, "urteil": "bogen"},
            "c": {"haltung": "H2", "start_s": 100, "ende_s": 110, "urteil": "bogen"},
        }

    def test_protokolltreffer_braucht_kein_menschliches_urteil(self) -> None:
        neu = {"start": 999, "ende": 999, "ist_treffer": True}

        self.assertEqual(
            "protokoll_bogen", vergleich_modul.urteil_uebertragen("H1", neu, self.urteile)
        )

    def test_ueberlappendes_zeitfenster_uebernimmt_das_urteil(self) -> None:
        neu = {"start": 105, "ende": 108, "ist_treffer": False}

        self.assertEqual("kein_bogen", vergleich_modul.urteil_uebertragen("H1", neu, self.urteile))

    def test_toleranz_greift_am_rand(self) -> None:
        knapp_daneben = {"start": 120, "ende": 122, "ist_treffer": False}
        deutlich_daneben = {"start": 200, "ende": 202, "ist_treffer": False}

        self.assertEqual(
            "kein_bogen", vergleich_modul.urteil_uebertragen("H1", knapp_daneben, self.urteile)
        )
        self.assertEqual(
            "unbeurteilt", vergleich_modul.urteil_uebertragen("H1", deutlich_daneben, self.urteile)
        )

    def test_eine_neue_gruppe_ohne_urteil_gilt_nie_als_fehlalarm(self) -> None:
        neu = {"start": 700, "ende": 705, "ist_treffer": False}

        self.assertEqual("unbeurteilt", vergleich_modul.urteil_uebertragen("H1", neu, self.urteile))

    def test_urteile_anderer_haltungen_werden_nicht_verwechselt(self) -> None:
        # Gleiches Zeitfenster, andere Haltung: darf nicht uebernommen werden.
        neu = {"start": 100, "ende": 110, "ist_treffer": False}

        self.assertEqual("bogen", vergleich_modul.urteil_uebertragen("H2", neu, self.urteile))
        self.assertEqual("kein_bogen", vergleich_modul.urteil_uebertragen("H1", neu, self.urteile))

    def test_ein_bestaetigter_bogen_schlaegt_ein_fehlalarm_urteil(self) -> None:
        # Eine breite neue Gruppe kann zwei alte Gruppen beruehren.
        urteile = {
            "a": {"haltung": "H1", "start_s": 100, "ende_s": 105, "urteil": "kein_bogen"},
            "b": {"haltung": "H1", "start_s": 130, "ende_s": 135, "urteil": "bogen"},
        }
        neu = {"start": 100, "ende": 135, "ist_treffer": False}

        self.assertEqual("bogen", vergleich_modul.urteil_uebertragen("H1", neu, urteile))


class UrteileEinlesenTests(unittest.TestCase):
    def test_review_wird_auf_haltung_und_zeitfenster_geschluesselt(self) -> None:
        with tempfile.TemporaryDirectory() as ordner:
            pfad = Path(ordner) / "review.json"
            pfad.write_text(
                json.dumps(
                    {
                        "urteile": {
                            "x": {"haltung": "H1", "start_s": 10, "ende_s": 20, "urteil": "bogen"}
                        }
                    }
                ),
                encoding="utf-8",
            )

            self.assertEqual({"H1|10-20": "bogen"}, kurve_modul.urteile_einlesen(pfad))


if __name__ == "__main__":
    unittest.main()
