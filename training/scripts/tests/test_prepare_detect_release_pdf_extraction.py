from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_ROOT = Path(__file__).resolve().parents[1]
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))

import prepare_detect_release_pdf_extraction as target


class DetectReleasePdfExtractionRequestTests(unittest.TestCase):
    def test_video_muss_exakt_den_pdf_stem_besitzen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            pdf = root / "20250101_111-222.pdf"
            pdf.write_bytes(b"pdf")
            (root / "anderes_video.mp4").write_bytes(b"video")

            self.assertIsNone(target._find_video(pdf))

    def test_genau_ein_stemgleiches_video_wird_uebernommen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            pdf = root / "20250101_111-222.pdf"
            pdf.write_bytes(b"pdf")
            expected = root / "20250101_111-222.mp4"
            expected.write_bytes(b"video")
            (root / "anderes_video.avi").write_bytes(b"video")

            self.assertEqual(expected, target._find_video(pdf))

    def test_mehrere_stemgleiche_video_endungen_sperren(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            pdf = root / "20250101_111-222.pdf"
            pdf.write_bytes(b"pdf")
            (root / "20250101_111-222.mp4").write_bytes(b"video")
            (root / "20250101_111-222.avi").write_bytes(b"video")

            with self.assertRaisesRegex(ValueError, "Mehrere exakt"):
                target._find_video(pdf)

    def test_neue_ausgabepfade_in_sicheren_ordnern_werden_akzeptiert(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            request, extraction = target._prepare_new_output_paths(
                root / "request.json",
                root / "extraction",
            )

            self.assertEqual(root / "request.json", request)
            self.assertEqual(root / "extraction", extraction)

    def test_vorhandenes_ziel_wird_nie_ueberschrieben(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "request.json").write_text("{}", encoding="utf-8")

            with self.assertRaises(FileExistsError):
                target._prepare_new_output_paths(
                    root / "request.json",
                    root / "extraction",
                )

    def test_auftrag_braucht_json_endung(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)

            with self.assertRaises(ValueError):
                target._prepare_new_output_paths(
                    root / "request.txt",
                    root / "extraction",
                )


if __name__ == "__main__":
    unittest.main()
