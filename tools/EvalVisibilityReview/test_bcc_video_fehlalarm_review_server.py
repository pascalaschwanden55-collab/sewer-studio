from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from tools.EvalVisibilityReview.bcc_video_fehlalarm_review_server import (
    URTEILE,
    FehlalarmReviewStore,
)


class FehlalarmReviewStoreTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary = tempfile.TemporaryDirectory()
        self.root = Path(self._temporary.name)
        self.queue_root = self.root / "queue"
        self.output = self.root / "reviews" / "review.json"
        self._warteschlange_schreiben()

    def tearDown(self) -> None:
        self._temporary.cleanup()

    def _warteschlange_schreiben(self, faelle: list[dict] | None = None) -> None:
        (self.queue_root / "clips").mkdir(parents=True, exist_ok=True)
        faelle = faelle or [
            {
                "nummer": 1,
                "fall_id": "aaaa1111",
                "haltung": "36053-36052",
                "start_s": 160,
                "ende_s": 162,
                "clip": "fall_001_aaaa1111.mp4",
            },
            {
                "nummer": 2,
                "fall_id": "bbbb2222",
                "haltung": "10261-10262",
                "start_s": 12,
                "ende_s": 16,
                "clip": "fall_002_bbbb2222.mp4",
            },
        ]
        for fall in faelle:
            (self.queue_root / "clips" / fall["clip"]).write_bytes(b"clip")
        (self.queue_root / "queue.json").write_text(
            json.dumps(
                {
                    "schema_version": 1,
                    "quelle_bericht_sha256": "a" * 64,
                    "faelle": faelle,
                },
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )

    def _store(self) -> FehlalarmReviewStore:
        return FehlalarmReviewStore(self.queue_root, self.output, "Pascal")

    def test_warteschlange_zeigt_den_ersten_offenen_fall(self) -> None:
        stand = self._store().stand()
        self.assertEqual(2, stand["gesamt"])
        self.assertEqual(2, stand["offen"])
        self.assertEqual("aaaa1111", stand["naechster"]["fall_id"])

    def test_die_pruefliste_verraet_weder_konfidenz_noch_vorabeinstufung(self) -> None:
        # Eine sichtbare Vorsortierung wuerde das Urteil lenken — genau dieser
        # Fehler ist bei der Benchmark-Erweiterung v1 schon einmal passiert.
        fall = self._store().stand()["naechster"]
        verboten = {"conf", "max_conf", "konfidenz", "einstufung", "bewertung", "ist_treffer"}
        self.assertEqual(set(), verboten & set(fall))

    def test_urteil_wird_sofort_und_atomar_gespeichert(self) -> None:
        store = self._store()
        stand = store.entscheiden("aaaa1111", "bogen")

        self.assertEqual(1, stand["offen"])
        self.assertEqual(1, stand["zaehlung"]["bogen"])
        gespeichert = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual("bogen", gespeichert["urteile"]["aaaa1111"]["urteil"])
        self.assertEqual("36053-36052", gespeichert["urteile"]["aaaa1111"]["haltung"])
        self.assertFalse(self.output.with_suffix(".json.tmp").exists())

    def test_ein_neustart_setzt_die_pruefung_fort(self) -> None:
        self._store().entscheiden("aaaa1111", "kein_bogen")

        stand = self._store().stand()
        self.assertEqual(1, stand["offen"])
        self.assertEqual("bbbb2222", stand["naechster"]["fall_id"])
        self.assertEqual(1, stand["zaehlung"]["kein_bogen"])

    def test_letztes_urteil_laesst_sich_zuruecknehmen(self) -> None:
        store = self._store()
        store.entscheiden("aaaa1111", "bogen")

        stand = store.zuruecknehmen()

        self.assertEqual(2, stand["offen"])
        self.assertEqual(0, stand["zaehlung"]["bogen"])

    def test_unbekanntes_urteil_und_unbekannter_fall_werden_abgewiesen(self) -> None:
        store = self._store()
        with self.assertRaises(ValueError):
            store.entscheiden("aaaa1111", "vielleicht")
        with self.assertRaises(ValueError):
            store.entscheiden("gibtsnicht", "bogen")
        self.assertFalse(self.output.exists())

    def test_review_einer_fremden_warteschlange_wird_nicht_fortgesetzt(self) -> None:
        self._store().entscheiden("aaaa1111", "bogen")
        self._warteschlange_schreiben(
            [
                {
                    "nummer": 1,
                    "fall_id": "cccc3333",
                    "haltung": "X",
                    "start_s": 1,
                    "ende_s": 2,
                    "clip": "fall_001_cccc3333.mp4",
                }
            ]
        )

        with self.assertRaises(SystemExit):
            self._store()

    def test_clip_pfad_bleibt_im_clip_ordner(self) -> None:
        store = self._store()
        self.assertIsNotNone(store.clip_pfad("aaaa1111"))
        self.assertIsNone(store.clip_pfad("gibtsnicht"))

        store.faelle[0]["clip"] = "../../ausserhalb.mp4"
        self.assertIsNone(store.clip_pfad("aaaa1111"))

    def test_erlaubte_urteile_sind_genau_drei(self) -> None:
        self.assertEqual(("bogen", "kein_bogen", "unsicher"), URTEILE)

    def test_neue_queue_bindet_die_clip_bytes(self) -> None:
        queue = json.loads((self.queue_root / "queue.json").read_text(encoding="utf-8"))
        queue["schema_version"] = 2
        for fall in queue["faelle"]:
            import hashlib
            clip = self.queue_root / "clips" / fall["clip"]
            fall["clip_sha256"] = hashlib.sha256(clip.read_bytes()).hexdigest()
        (self.queue_root / "queue.json").write_text(json.dumps(queue), encoding="utf-8")
        self.assertEqual(2, self._store().stand()["gesamt"])

        erster_clip = self.queue_root / "clips" / queue["faelle"][0]["clip"]
        erster_clip.write_bytes(b"veraendert")
        with self.assertRaises(SystemExit):
            self._store()


if __name__ == "__main__":
    unittest.main()
