from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "gold_stock_audit.py"
SPEC = importlib.util.spec_from_file_location("gold_stock_audit", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

IMAGE_SIZE = (8, 4)
PIXELS = IMAGE_SIZE[0] * IMAGE_SIZE[1]
# 10 Hintergrund, 5 Maske, 17 Hintergrund = 32 Pixel.
VALID_RLE = "0,10,5,17"


class GoldStockAuditTests(unittest.TestCase):
    def _make_root(self, root: Path) -> tuple[Path, Path, Path, Path]:
        frames = root / "frames"
        frames.mkdir(parents=True)
        eval_images = root / "eval_set" / "images"
        eval_images.mkdir(parents=True)
        negatives = root / "negatives"
        negatives.mkdir(parents=True)
        registry = root / "export_registry_v1.json"
        registry.write_text(
            json.dumps({"approved_by": "Besitzer"}), encoding="utf-8"
        )
        return frames, eval_images, negatives, registry

    def _image(self, frames: Path, name: str, color: int = 7) -> Path:
        path = frames / name
        Image.new("RGB", IMAGE_SIZE, (color, color, color)).save(path)
        return path

    def _sample(
        self,
        sample_id: str,
        frame: Path,
        case_id: str = "case-1",
        code: str = "BCCBY",
        description: str = "Bogen bei 3 Uhr",
    ) -> dict:
        return {
            "SampleId": sample_id,
            "CaseId": case_id,
            "Code": code,
            "Beschreibung": description,
            "FramePath": str(frame),
            "Status": 1,
            "SourceType": "ManualCoding",
            "HumanConfirmed": True,
            "Corrected": False,
            "ConfirmedByUser": "Besitzer",
            "ConfirmedAtUtc": "2026-07-25T12:00:00Z",
            "MatchLevel": "ReviewApproved",
            "HasBbox": True,
            "BboxXCenter": 0.5,
            "BboxYCenter": 0.5,
            "BboxWidth": 0.5,
            "BboxHeight": 0.5,
            "SamMaskRle": VALID_RLE,
            "SamMaskImageWidth": IMAGE_SIZE[0],
            "SamMaskImageHeight": IMAGE_SIZE[1],
        }

    def _audit(
        self,
        root: Path,
        samples: list[dict],
        eval_images: Path,
        negatives: Path,
        registry: Path,
        approved_by: str = "Besitzer",
    ) -> dict:
        samples_path = root / "training_samples.json"
        samples_path.write_text(json.dumps(samples), encoding="utf-8")
        return MODULE.build_audit(
            samples_path,
            registry,
            eval_images,
            negatives,
            approved_by,
            "registry",
            datetime(2026, 7, 25, 12, 0, tzinfo=timezone.utc),
        )

    def test_jede_pruefstufe_verwirft_korrekt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            good_frame = self._image(frames, "good.png")
            eval_frame = self._image(frames, "eval.png", color=42)
            # Gleiche Bytes ins Eval-Set legen: Hash-Kollision muss verwerfen.
            (eval_images / "eval.png").write_bytes(eval_frame.read_bytes())
            broken = frames / "broken.jpg"
            broken.write_bytes(b"das-ist-kein-bild")

            samples = [
                self._sample("ok", good_frame),
                self._sample("entwurf", good_frame) | {"Status": 4},
                self._sample("zurueckgewiesen", good_frame) | {"Status": 2},
                self._sample("teacher", good_frame) | {"SourceType": "TeacherModel"},
                self._sample("nicht-bestaetigt", good_frame) | {"HumanConfirmed": False},
                self._sample("fremder-user", good_frame) | {"ConfirmedByUser": "Fremd"},
                self._sample("entscheidung-fehlt", good_frame) | {"Corrected": None},
                self._sample("zeitpunkt-fehlt", good_frame) | {"ConfirmedAtUtc": None},
                self._sample("matchlevel-falsch", good_frame) | {
                    "MatchLevel": "AutoMatched",
                },
                self._sample("bild-fehlt", frames / "fehlt.png"),
                self._sample("bild-kaputt", broken),
                self._sample("box-null", good_frame) | {"BboxWidth": 0.0},
                self._sample("box-zu-gross", good_frame) | {"BboxHeight": 1.2},
                self._sample("box-zentrum-draussen", good_frame) | {"BboxXCenter": 1.4},
                self._sample("box-ragt-raus", good_frame) | {
                    "BboxXCenter": 0.9,
                    "BboxWidth": 0.4,
                },
                self._sample("rle-fehlt", good_frame) | {"SamMaskRle": None},
                self._sample("rle-startwert", good_frame) | {"SamMaskRle": "2,10,5,17"},
                self._sample("rle-token", good_frame) | {"SamMaskRle": "0,10,x,17"},
                self._sample("rle-summe", good_frame) | {"SamMaskRle": "0,10,5,10"},
                self._sample("rle-leer", good_frame) | {"SamMaskRle": f"0,{PIXELS}"},
                self._sample("rle-maske-dims", good_frame) | {
                    # Format gueltig (16 Pixel), aber 4x4 passt nicht zum 8x4-Bild.
                    "SamMaskRle": "0,6,4,6",
                    "SamMaskImageWidth": 4,
                    "SamMaskImageHeight": 4,
                },
                self._sample("maske-ausserhalb-box", good_frame) | {
                    "SamMaskRle": "1,5,27",
                    "BboxXCenter": 0.75,
                    "BboxYCenter": 0.75,
                    "BboxWidth": 0.25,
                    "BboxHeight": 0.25,
                },
                self._sample("masken-huelle-taeuscht", good_frame) | {
                    # Pixel nur ganz links oben + rechts unten: Die Huelle schneidet
                    # die mittige Box, aber kein echter Maskenpixel liegt darin.
                    "SamMaskRle": "1,1,30,1",
                    "BboxXCenter": 0.5,
                    "BboxYCenter": 0.5,
                    "BboxWidth": 0.25,
                    "BboxHeight": 0.25,
                },
                self._sample("rle-ungerade-gueltig", good_frame) | {
                    # Der echte Encoder darf mit einem Vordergrund-Run enden.
                    "SamMaskRle": "0,27,5",
                    "BboxXCenter": 0.75,
                    "BboxYCenter": 0.75,
                    "BboxWidth": 0.5,
                    "BboxHeight": 0.5,
                },
                self._sample("code-unbekannt", good_frame, code="XXXAA"),
                self._sample("eval-treffer", eval_frame),
            ]
            audit = self._audit(root, samples, eval_images, negatives, registry)

            einlesen = audit["einlesen"]
            self.assertEqual(26, einlesen["datei_gesamt"])
            self.assertEqual(1, einlesen["uebersprungen_entwurf"])
            self.assertEqual(1, einlesen["uebersprungen_status_sonstige"])
            self.assertEqual(1, einlesen["uebersprungen_quelle_sonstige"])
            self.assertEqual(23, einlesen["eingelesen"])

            stufen = audit["pruefstufen"]
            self.assertEqual(23, stufen["eingelesen"])
            self.assertEqual(18, stufen["persoenlich"])
            self.assertEqual(16, stufen["bild_ok"])
            self.assertEqual(12, stufen["box_ok"])
            self.assertEqual(4, stufen["maske_ok"])
            self.assertEqual(3, stufen["code_ok"])
            self.assertEqual(2, stufen["eval_sauber"])
            self.assertEqual(2, stufen["final_verwendbar"])

            gruende = {v["sample_id"]: v["stufe"] for v in audit["verwerfungen"]}
            self.assertEqual(
                {
                    "nicht-bestaetigt": "persoenlich",
                    "fremder-user": "persoenlich",
                    "entscheidung-fehlt": "persoenlich",
                    "zeitpunkt-fehlt": "persoenlich",
                    "matchlevel-falsch": "persoenlich",
                    "bild-fehlt": "bild_ok",
                    "bild-kaputt": "bild_ok",
                    "box-null": "box_ok",
                    "box-zu-gross": "box_ok",
                    "box-zentrum-draussen": "box_ok",
                    "box-ragt-raus": "box_ok",
                    "rle-fehlt": "maske_ok",
                    "rle-startwert": "maske_ok",
                    "rle-token": "maske_ok",
                    "rle-summe": "maske_ok",
                    "rle-leer": "maske_ok",
                    "rle-maske-dims": "maske_ok",
                    "maske-ausserhalb-box": "maske_ok",
                    "masken-huelle-taeuscht": "maske_ok",
                    "code-unbekannt": "code_ok",
                    "eval-treffer": "eval_sauber",
                },
                gruende,
            )
            self.assertEqual(1, audit["eval_treffer_ausgeschlossen"])
            unbekannte = audit["unbekannte_codes"]
            self.assertEqual(1, len(unbekannte))
            self.assertEqual("XXX", unbekannte[0]["code"])
            self.assertEqual(["code-unbekannt"], unbekannte[0]["sample_ids"])
            final_ids = {s["sample_id"] for s in audit["samples"]}
            self.assertEqual({"ok", "rle-ungerade-gueltig"}, final_ids)

    def test_platzhalter_ist_verwendbar_aber_kb_text_offen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            samples = [
                self._sample("ohne", frame, description="Riss laengs bei 12 Uhr"),
                self._sample(
                    "platzhalter-ae",
                    frame,
                    description="Riss laengs bei 12 Uhr — Ausmass ergaenzen",
                ),
                self._sample(
                    "platzhalter-umlaut",
                    frame,
                    description="Riss quer — Ausmass ergänzen",
                ),
            ]
            audit = self._audit(root, samples, eval_images, negatives, registry)

            self.assertEqual(3, audit["pruefstufen"]["final_verwendbar"])
            self.assertEqual(2, audit["kb_text_offen"])
            flags = {s["sample_id"]: s["kb_text_offen"] for s in audit["samples"]}
            self.assertEqual(
                {"ohne": False, "platzhalter-ae": True, "platzhalter-umlaut": True},
                flags,
            )

    def test_duplikate_werden_als_gruppe_ausgewiesen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            kopie = frames / "kopie.png"
            kopie.write_bytes(frame.read_bytes())
            anderes = self._image(frames, "b.png", color=99)
            samples = [
                self._sample("s1", frame),
                self._sample("s2", kopie),
                self._sample("s3", anderes),
            ]
            audit = self._audit(root, samples, eval_images, negatives, registry)

            self.assertEqual(3, audit["pruefstufen"]["final_verwendbar"])
            self.assertEqual(1, audit["duplikat_gruppen_anzahl"])
            gruppe = audit["duplikat_gruppen"][0]
            self.assertEqual(2, gruppe["anzahl"])
            self.assertEqual(["s1", "s2"], gruppe["sample_ids"])
            self.assertEqual(
                hashlib.sha256(frame.read_bytes()).hexdigest(), gruppe["sha256"]
            )

    def test_split_ist_deterministisch_und_gruppentreu(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            samples = []
            for index in range(6):
                frame = self._image(frames, f"f{index}.png", color=index + 1)
                samples.append(
                    self._sample(f"s{index}", frame, case_id=f"foto_20260720_{index}")
                )
            copy = frames / "f0-copy.png"
            copy.write_bytes((frames / "f0.png").read_bytes())
            samples.append(
                self._sample("s0-copy", copy, case_id="foto_20260721_99")
            )
            halt_frame = self._image(frames, "h.png", color=200)
            halt_frame_2 = self._image(frames, "h2.png", color=201)
            samples.append(self._sample("h1", halt_frame, case_id="21683-21749"))
            samples.append(self._sample("h2", halt_frame_2, case_id="21683-21749"))

            audit_a = self._audit(root, samples, eval_images, negatives, registry)
            audit_b = self._audit(root, samples, eval_images, negatives, registry)

            rollen_a = {s["sample_id"]: s["rolle"] for s in audit_a["samples"]}
            rollen_b = {s["sample_id"]: s["rolle"] for s in audit_b["samples"]}
            self.assertEqual(rollen_a, rollen_b)

            # Pseudo-IDs werden nicht nach Aufnahmetag zusammengeworfen. Nur identische
            # Bilder teilen ihren SHA-Schluessel; eine echte Haltung bleibt komplett zusammen.
            gruppen = {g["gruppe"]: g["rolle"] for g in audit_a["split"]["gruppen"]}
            self.assertEqual(7, len(gruppen))
            f0_sha = hashlib.sha256((frames / "f0.png").read_bytes()).hexdigest()
            erwartet_foto = MODULE.split_role(f"bild:{f0_sha}")
            erwartet_haltung = MODULE.split_role("haltung:21683-21749")
            self.assertEqual(erwartet_foto, gruppen[f"bild:{f0_sha}"])
            self.assertEqual(
                erwartet_haltung, gruppen["haltung:21683-21749"]
            )
            self.assertEqual(erwartet_foto, rollen_a["s0"])
            self.assertEqual(erwartet_foto, rollen_a["s0-copy"])
            self.assertEqual(erwartet_haltung, rollen_a["h1"])
            self.assertEqual(erwartet_haltung, rollen_a["h2"])
            for index in range(6):
                image_sha = hashlib.sha256(
                    (frames / f"f{index}.png").read_bytes()
                ).hexdigest()
                self.assertEqual(
                    MODULE.split_role(f"bild:{image_sha}"),
                    rollen_a[f"s{index}"],
                )

            bilder = audit_a["split"]["bilder"]
            self.assertEqual(9, bilder["train"] + bilder["val"] + bilder["test"])
            self.assertTrue(audit_a["split"]["test_eingefroren_nur_markiert"])
            self.assertFalse(audit_a["split"]["release_faehig"])
            self.assertEqual(7, audit_a["split"]["fehlende_haltungsidentitaet"])

    def test_split_normalisiert_haltung_und_verbindet_bildduplikate(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            shared = self._image(frames, "shared.png", color=10)
            shared_copy = frames / "shared-copy.png"
            shared_copy.write_bytes(shared.read_bytes())
            a_other = self._image(frames, "a-other.png", color=11)
            b_other = self._image(frames, "b-other.png", color=12)
            samples = [
                self._sample("a1", shared, case_id="06.24379-06.24377"),
                self._sample("a2", a_other, case_id="24379-24377/2026"),
                self._sample("b1", shared_copy, case_id="111-222"),
                self._sample("b2", b_other, case_id="111-222"),
            ]

            audit = self._audit(root, samples, eval_images, negatives, registry)

            # Das Bildduplikat verbindet beide Haltungen transitiv. Kein Mitglied
            # darf dadurch in einer anderen Split-Rolle landen.
            self.assertEqual(1, len(audit["split"]["gruppen"]))
            self.assertEqual(1, len({sample["rolle"] for sample in audit["samples"]}))
            keys = {sample["sample_id"]: sample["haltung_key"] for sample in audit["samples"]}
            self.assertEqual("24379-24377", keys["a1"])
            self.assertEqual("24379-24377", keys["a2"])
            self.assertTrue(audit["split"]["release_faehig"])

    def test_leere_und_gold_inbox_ids_sind_keine_haltungen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            first = self._image(frames, "first.png", color=20)
            second = self._image(frames, "second.png", color=21)
            samples = [
                self._sample("leer", first, case_id=""),
                self._sample("inbox", second, case_id="gold_inbox_abc123"),
            ]

            audit = self._audit(root, samples, eval_images, negatives, registry)

            self.assertFalse(audit["split"]["release_faehig"])
            self.assertEqual(2, audit["split"]["fehlende_haltungsidentitaet"])
            self.assertTrue(all(sample["haltung_key"] is None for sample in audit["samples"]))

    def test_eval_haltung_wird_auch_bei_anderen_bildbytes_gesperrt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            (eval_images.parent / "_candidates.json").write_text(
                json.dumps([{"haltung_key": "06.24379-06.24377"}]),
                encoding="utf-8",
            )
            frame = self._image(frames, "anderer-frame.png", color=31)
            sample = self._sample(
                "eval-haltung",
                frame,
                case_id="24379-24377/2026_Saniert",
            )

            audit = self._audit(root, [sample], eval_images, negatives, registry)

            self.assertEqual(0, audit["pruefstufen"]["final_verwendbar"])
            self.assertEqual(1, audit["eval_treffer_ausgeschlossen"])
            self.assertEqual("eval_sauber", audit["verwerfungen"][0]["stufe"])
            self.assertIn("24379-24377", audit["verwerfungen"][0]["grund"])

    def test_split_rolle_folgt_dem_hash(self) -> None:
        def erwartete_rolle(key: str) -> str:
            digest = hashlib.sha256(f"split-v1|{key}".encode("utf-8")).digest()
            value = int.from_bytes(digest[:8], "big") / float(1 << 64)
            if value < 0.70:
                return "train"
            if value < 0.85:
                return "val"
            return "test"

        for key in ("foto_20260720", "foto_20260724", "21683-21749", "x", "y"):
            self.assertEqual(erwartete_rolle(key), MODULE.split_role(key))

    def test_pilotenschwelle_bei_30(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            samples = []
            for index in range(29):
                frame = self._image(frames, f"f{index}.png", color=index + 1)
                samples.append(self._sample(f"s{index:02d}", frame, case_id=f"case-{index}"))

            audit29 = self._audit(root, samples, eval_images, negatives, registry)
            self.assertEqual([], audit29["piloten"])

            frame = self._image(frames, "f29.png", color=250)
            samples.append(self._sample("s29", frame, case_id="case-29"))
            audit30 = self._audit(root, samples, eval_images, negatives, registry)
            self.assertEqual(1, len(audit30["piloten"]))
            pilot = audit30["piloten"][0]
            self.assertEqual("BCC", pilot["code"])
            self.assertEqual(30, pilot["gesamt"])
            self.assertEqual(30, pilot["train"] + pilot["val"] + pilot["test"])

    def test_pilot_ohne_unabhaengige_val_test_gruppe_ist_nicht_auswertbar(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            samples = []
            for index in range(30):
                frame = self._image(frames, f"same-holding-{index}.png", color=index + 1)
                samples.append(
                    self._sample(
                        f"same-{index:02d}",
                        frame,
                        case_id="287425-81162",
                    )
                )

            audit = self._audit(root, samples, eval_images, negatives, registry)

            self.assertEqual([], audit["piloten"])
            self.assertEqual(1, len(audit["piloten_nicht_auswertbar"]))
            blocked = audit["piloten_nicht_auswertbar"][0]
            self.assertEqual("BCC", blocked["code"])
            self.assertEqual(30, blocked["gesamt"])
            self.assertEqual(1, sum(value > 0 for value in (
                blocked["train"], blocked["val"], blocked["test"]
            )))

    def test_bericht_wird_geschrieben_und_enthaelt_kernfelder(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            (negatives / "neg1.png").write_bytes(frame.read_bytes())
            samples = [self._sample("ok", frame)]
            audit = self._audit(root, samples, eval_images, negatives, registry)

            reports = root / "reports"
            pfad = MODULE.write_report(
                audit, reports, datetime(2026, 7, 25, 12, 0, tzinfo=timezone.utc)
            )
            self.assertTrue(pfad.is_file())
            self.assertTrue(pfad.name.startswith("gold_stock_audit_"))
            dokument = json.loads(pfad.read_text(encoding="utf-8"))
            self.assertEqual("schreibfreie_pruefung", dokument["modus"])
            self.assertIn("samples_sha256", dokument["eingaben"])
            self.assertIn("registry_sha256", dokument["eingaben"])
            self.assertIn("zeitstempel_utc", dokument)
            self.assertEqual(1, dokument["negativ_pool"]["anzahl"])
            self.assertEqual(
                hashlib.sha256(frame.read_bytes()).hexdigest(),
                dokument["negativ_pool"]["dateien"][0]["sha256"],
            )

    def test_resolve_approved_by_cli_hat_vorrang(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            registry = Path(temporary) / "export_registry_v1.json"
            registry.write_text(
                json.dumps({"approved_by": "Besitzer"}), encoding="utf-8"
            )
            self.assertEqual(
                ("Jemand", "cli"), MODULE.resolve_approved_by("Jemand", registry)
            )
            self.assertEqual(
                ("Besitzer", "registry"), MODULE.resolve_approved_by(None, registry)
            )
            with self.assertRaises(ValueError):
                MODULE.resolve_approved_by(None, Path(temporary) / "fehlt.json")


if __name__ == "__main__":
    unittest.main()
