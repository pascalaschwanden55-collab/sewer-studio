"""Tests fuer den Skill-Linter (stdlib unittest, kein pytest noetig).

Ausfuehren:
  python tools/skill-linter/test_skill_lint.py -v
"""
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import skill_lint  # noqa: E402

HERE = Path(__file__).parent
FM = "---\nname: test\ndescription: Use when testing\n---\n"


def _mk_skill(root, name, body):
    d = Path(root) / name
    d.mkdir(parents=True, exist_ok=True)
    (d / "SKILL.md").write_text(body, encoding="utf-8")
    return d


def _run(root):
    return skill_lint.main(["skill_lint.py", str(root)])


class SkillLintTests(unittest.TestCase):
    def test_sauber(self):
        with tempfile.TemporaryDirectory() as t:
            _mk_skill(t, "a", FM + "# A\nAlles gut, echte Route /detect/yolo.\n")
            self.assertEqual(_run(t), 0)

    def test_funde(self):
        with tempfile.TemporaryDirectory() as t:
            _mk_skill(t, "a", FM + "# A\nBuild unter C:/Sewer-StudioKI_3.1/ mit qwen3-vl:32b.\n")
            self.assertEqual(_run(t), 1)

    def test_kaputtes_format_hat_vorrang(self):
        with tempfile.TemporaryDirectory() as t:
            d = Path(t) / "a"
            d.mkdir()
            (d / "SKILL.md").write_text("# Kein Frontmatter\nqwen3-vl:32b\n", encoding="utf-8")
            self.assertEqual(_run(t), 2)

    def test_negation_32b(self):
        with tempfile.TemporaryDirectory() as t:
            _mk_skill(t, "a", FM + "# A\nDas Modell qwen3-vl:32b gibt es nicht in HEAD.\n")
            self.assertEqual(_run(t), 0)

    def test_negation_benchmark(self):
        with tempfile.TemporaryDirectory() as t:
            _mk_skill(t, "a", FM + "# A\nHinweis: benchmark_metrics.json existiert derzeit nicht.\n")
            self.assertEqual(_run(t), 0)

    def test_ignoriert_archiv(self):
        with tempfile.TemporaryDirectory() as t:
            _mk_skill(Path(t) / "skills-archiv", "alt", FM + "# Alt\nqwen3-vl:32b\n")
            _mk_skill(t, "gut", FM + "# Gut\nsauber\n")
            self.assertEqual(_run(t), 0)

    def test_cli_alle_drei_exitcodes(self):
        with tempfile.TemporaryDirectory() as t:
            clean = Path(t) / "clean"
            _mk_skill(clean, "a", FM + "# A\nsauber\n")
            findings = Path(t) / "find"
            _mk_skill(findings, "a", FM + "# A\nqwen3-vl:32b\n")
            broken = Path(t) / "brk"
            (broken / "a").mkdir(parents=True)
            (broken / "a" / "SKILL.md").write_text("kaputt ohne frontmatter\n", encoding="utf-8")
            script = str(HERE / "skill_lint.py")
            self.assertEqual(subprocess.run([sys.executable, script, str(clean)]).returncode, 0)
            self.assertEqual(subprocess.run([sys.executable, script, str(findings)]).returncode, 1)
            self.assertEqual(subprocess.run([sys.executable, script, str(broken)]).returncode, 2)


if __name__ == "__main__":
    unittest.main()
