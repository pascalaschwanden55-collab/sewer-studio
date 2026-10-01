import base64
import hashlib
import importlib.util
import io
import json
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "detect_release_holdout_status.py"
SPEC = importlib.util.spec_from_file_location(
    "detect_release_holdout_status_under_test",
    SCRIPT_PATH,
)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("Statusskript konnte fuer den Test nicht geladen werden.")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


CLASS_NAMES = (
    "BCA_anschluss",
    "BAB_riss",
    "BAC_bruch",
    "BAA_verformung",
    "BAF_oberflaeche",
    "BAH_schadanschluss",
    "BAI_dichtung",
    "BAJ_verbindung",
    "BBA_wurzeln",
    "BBB_anhaftung",
    "BBC_ablagerung",
    "BBD_boden",
    "BBF_infiltration",
    "SONST_schaden",
    "BCC_bogen",
)

# Echtes kleines 1x1-PNG, nie ein Kundenbild. Der individuelle Testzusatz macht
# die Bildbytes eindeutig; der PNG-Header und seine Masse bleiben unveraendert.
PNG_BYTES = base64.b64decode(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
)


class DetectReleaseHoldoutStatusTests(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.root = Path(self._temp.name)

    def tearDown(self):
        self._temp.cleanup()

    def test_unvollstaendige_review_bleibt_review_incomplete(self):
        fixture = StatusFixture(self.root / "incomplete", candidate_count=3)
        fixture.write_review(
            {
                fixture.candidate_ids[0]: fixture.negative_decision(),
            }
        )

        status = MODULE.evaluate_holdout_status(fixture.holdout, fixture.review)

        self.assertEqual("review_incomplete", status["dataset_status"])
        self.assertEqual(3, status["total"])
        self.assertEqual(1, status["reviewed"])
        self.assertEqual(2, status["open"])
        self.assertEqual(0, status["positive"])
        self.assertEqual(1, status["negative"])
        self.assertEqual(0, status["exclude"])

    def test_cli_gibt_verstaendliches_json_aus_und_meldet_offenen_status(self):
        fixture = StatusFixture(self.root / "cli", candidate_count=2)
        fixture.write_review({})
        stdout = io.StringIO()

        with redirect_stdout(stdout):
            exit_code = MODULE.main(
                [
                    "--holdout",
                    str(fixture.holdout),
                    "--review",
                    str(fixture.review),
                ]
            )

        document = json.loads(stdout.getvalue())
        self.assertEqual(2, exit_code)
        self.assertEqual("detect_release_holdout_status", document["purpose"])
        self.assertEqual("review_incomplete", document["dataset_status"])
        self.assertEqual(2, document["total"])
        self.assertEqual(2, document["open"])

    def test_fehlende_klasse_bleibt_coverage_incomplete(self):
        fixture = StatusFixture(self.root / "missing-class", candidate_count=76)
        decisions = fixture.release_decisions(included_class_ids=range(14))
        fixture.write_review(decisions)

        status = MODULE.evaluate_holdout_status(fixture.holdout, fixture.review)

        self.assertEqual("coverage_incomplete", status["dataset_status"])
        self.assertEqual(0, status["open"])
        self.assertEqual(75, status["negative"])
        self.assertEqual(75, status["negative_physical_holdings"])
        self.assertEqual(0, status["instances_by_class"]["BCC_bogen"])
        self.assertEqual(0, status["images_by_class"]["BCC_bogen"])
        self.assertIn(
            {
                "metric": "class_instances",
                "class_id": 14,
                "class_name": "BCC_bogen",
                "actual": 0,
                "required": 20,
            },
            status["shortfalls"],
        )

    def test_vollstaendige_abdeckung_ist_bereit_und_zaehlt_alle_boxen(self):
        fixture = StatusFixture(self.root / "ready", candidate_count=76)
        fixture.write_review(fixture.release_decisions(included_class_ids=range(15)))
        before = fixture.file_hashes()

        status = MODULE.evaluate_holdout_status(fixture.holdout, fixture.review)

        self.assertEqual("ready_for_detect_evaluation", status["dataset_status"])
        self.assertEqual(76, status["total"])
        self.assertEqual(0, status["open"])
        self.assertEqual(1, status["positive"])
        self.assertEqual(75, status["negative"])
        self.assertEqual(0, status["exclude"])
        self.assertEqual(1, status["positive_physical_holdings"])
        self.assertEqual(75, status["negative_physical_holdings"])
        self.assertEqual(
            {name: 20 for name in CLASS_NAMES},
            status["instances_by_class"],
        )
        self.assertEqual(
            {name: 1 for name in CLASS_NAMES},
            status["images_by_class"],
        )
        self.assertEqual([], status["shortfalls"])
        self.assertEqual(before, fixture.file_hashes())

        stricter = MODULE.evaluate_holdout_status(
            fixture.holdout,
            fixture.review,
            min_instances_per_class=21,
        )
        self.assertEqual("coverage_incomplete", stricter["dataset_status"])
        self.assertEqual(15, len(stricter["shortfalls"]))

    def test_falsche_review_bindung_wird_abgewiesen(self):
        fixture = StatusFixture(self.root / "binding", candidate_count=2)
        fixture.write_review({})
        review = fixture.read_json(fixture.review)
        review["class_map_sha256"] = "f" * 64
        fixture.review.write_bytes(fixture.json_bytes(review))

        with self.assertRaisesRegex(ValueError, "class_map_sha256"):
            MODULE.evaluate_holdout_status(fixture.holdout, fixture.review)

    def test_bildmutation_wird_abgewiesen(self):
        fixture = StatusFixture(self.root / "image-mutation", candidate_count=2)
        fixture.write_review({})
        image_path = fixture.holdout / fixture.candidates[0]["image_path"]
        image_path.write_bytes(image_path.read_bytes() + b"manipuliert")

        with self.assertRaisesRegex(ValueError, "Bild wurde veraendert"):
            MODULE.evaluate_holdout_status(fixture.holdout, fixture.review)

    def test_release_mindestwerte_duerfen_nicht_gesenkt_werden(self):
        cases = (
            {"min_instances_per_class": 19},
            {"min_negative_images": 74},
            {"min_negative_physical_holdings": 29},
        )
        for parameters in cases:
            with self.subTest(parameters=parameters):
                with self.assertRaisesRegex(ValueError, "Release-Mindestwert"):
                    MODULE.evaluate_holdout_status(
                        self.root / "fehlt",
                        self.root / "fehlt.json",
                        **parameters,
                    )


class StatusFixture:
    def __init__(self, root: Path, *, candidate_count: int):
        self.root = root
        self.holdout = root / "holdout"
        self.review = root / "review" / "review.json"
        images_root = self.holdout / "images"
        images_root.mkdir(parents=True)
        self.review.parent.mkdir(parents=True)
        self.candidates: list[dict[str, object]] = []
        hashes: dict[str, dict[str, object]] = {}
        for index in range(candidate_count):
            image_name = f"frame-{index:03d}.png"
            relative_path = f"images/{image_name}"
            image_bytes = PNG_BYTES + index.to_bytes(4, "big")
            (images_root / image_name).write_bytes(image_bytes)
            image_sha256 = hashlib.sha256(image_bytes).hexdigest()
            left = 10_000 + index
            right = 20_000 + index
            self.candidates.append(
                {
                    "id": f"drh-{index:03d}",
                    "image_path": relative_path,
                    "frame_path": image_name,
                    "image_sha256": image_sha256,
                    "size_bytes": len(image_bytes),
                    "width": 1,
                    "height": 1,
                    "haltung_key": f"{left}-{right}",
                    "physical_holding_key": f"{left}|{right}",
                }
            )
            hashes[relative_path] = {
                "sha256": image_sha256,
                "size_bytes": len(image_bytes),
            }

        candidates_document = {
            "schema_version": "1.0",
            "purpose": "detect_release_holdout_candidates",
            "holdout_id": "h" * 64,
            "candidates": self.candidates,
        }
        candidates_path = self.holdout / "_candidates.json"
        candidates_path.write_bytes(self.json_bytes(candidates_document))
        candidates_sha256 = self.sha_file(candidates_path)
        hashes["_candidates.json"] = {
            "sha256": candidates_sha256,
            "size_bytes": candidates_path.stat().st_size,
        }
        self.classes = [
            {"id": class_id, "name": name, "label": f"Klasse {class_id}"}
            for class_id, name in enumerate(CLASS_NAMES)
        ]
        self.manifest = {
            "schema_version": "1.0",
            "purpose": "detect_release_holdout",
            "holdout_id": "h" * 64,
            "frozen": True,
            "hash_algorithm": "sha256",
            "hashes_count": len(hashes),
            "candidates_count": candidate_count,
            "candidates_sha256": candidates_sha256,
            "candidate_id": "detect_gold_candidate",
            "candidate_manifest_sha256": "1" * 64,
            "candidate_weights_sha256": "2" * 64,
            "class_map_version": 3,
            "class_map_sha256": "3" * 64,
            "vsa_manifest_hash": "4" * 64,
            "vsa_manifest_sha256": "4" * 64,
            "classes": self.classes,
            "hashes": hashes,
        }
        (self.holdout / "_manifest.json").write_bytes(self.json_bytes(self.manifest))

    @property
    def candidate_ids(self) -> list[str]:
        return [str(row["id"]) for row in self.candidates]

    def release_decisions(self, *, included_class_ids) -> dict[str, object]:
        annotations = []
        for class_id in included_class_ids:
            for number in range(20):
                annotations.append(
                    {
                        "id": f"box-{class_id:02d}-{number:02d}",
                        "class_id": class_id,
                        "class_name": CLASS_NAMES[class_id],
                        "box": {
                            "x_center": 0.5,
                            "y_center": 0.5,
                            "width": 0.2,
                            "height": 0.2,
                        },
                    }
                )
        decisions: dict[str, object] = {
            self.candidate_ids[0]: self.positive_decision(annotations)
        }
        for candidate_id in self.candidate_ids[1:]:
            decisions[candidate_id] = self.negative_decision()
        return decisions

    @staticmethod
    def positive_decision(annotations: list[dict[str, object]]) -> dict[str, object]:
        return {
            "decision": "positive",
            "comment": "Alle sichtbaren Objekte markiert.",
            "reviewed_at_utc": "2026-08-03T12:30:00Z",
            "annotations": annotations,
        }

    @staticmethod
    def negative_decision() -> dict[str, object]:
        return {
            "decision": "negative",
            "comment": "Keine der 15 Klassen sichtbar.",
            "reviewed_at_utc": "2026-08-03T12:30:00Z",
            "annotations": [],
        }

    def write_review(self, decisions: dict[str, object]) -> None:
        manifest_path = self.holdout / "_manifest.json"
        document = {
            "schema_version": "1.0",
            "purpose": "detect_release_holdout_review",
            "holdout_id": self.manifest["holdout_id"],
            "manifest_sha256": self.sha_file(manifest_path),
            "candidates_sha256": self.manifest["candidates_sha256"],
            "candidate_id": self.manifest["candidate_id"],
            "candidate_manifest_sha256": self.manifest[
                "candidate_manifest_sha256"
            ],
            "candidate_weights_sha256": self.manifest[
                "candidate_weights_sha256"
            ],
            "class_map_version": self.manifest["class_map_version"],
            "class_map_sha256": self.manifest["class_map_sha256"],
            "vsa_manifest_hash": self.manifest["vsa_manifest_hash"],
            "vsa_manifest_sha256": self.manifest["vsa_manifest_sha256"],
            "reviewer": "Besitzer",
            "updated_at_utc": "2026-08-03T12:30:00Z",
            "decisions": decisions,
        }
        self.review.write_bytes(self.json_bytes(document))

    def file_hashes(self) -> dict[str, str]:
        files = [
            path
            for path in (*self.holdout.rglob("*"), self.review)
            if path.is_file()
        ]
        return {
            str(path.relative_to(self.root)).replace("\\", "/"): self.sha_file(path)
            for path in sorted(files)
        }

    @staticmethod
    def json_bytes(document: object) -> bytes:
        return (
            json.dumps(document, ensure_ascii=False, indent=2) + "\n"
        ).encode("utf-8")

    @staticmethod
    def read_json(path: Path) -> dict[str, object]:
        return json.loads(path.read_text(encoding="utf-8"))

    @staticmethod
    def sha_file(path: Path) -> str:
        return hashlib.sha256(path.read_bytes()).hexdigest()


if __name__ == "__main__":
    unittest.main()
