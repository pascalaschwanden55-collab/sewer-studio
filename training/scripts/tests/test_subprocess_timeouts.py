from __future__ import annotations

import ast
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
SEARCH_ROOTS = (
    REPO_ROOT / "training" / "scripts",
    REPO_ROOT / "training" / "vsa_classifier",
)
BLOCKING_SUBPROCESS_CALLS = {"run", "call", "check_call", "check_output"}


def test_blockierende_subprocess_aufrufe_haben_ein_timeout() -> None:
    ohne_timeout: list[str] = []

    for root in SEARCH_ROOTS:
        for source_path in sorted(root.rglob("*.py")):
            tree = ast.parse(source_path.read_text(encoding="utf-8-sig"), filename=str(source_path))
            for node in ast.walk(tree):
                if not isinstance(node, ast.Call) or not isinstance(node.func, ast.Attribute):
                    continue
                owner = node.func.value
                if (
                    not isinstance(owner, ast.Name)
                    or owner.id != "subprocess"
                    or node.func.attr not in BLOCKING_SUBPROCESS_CALLS
                ):
                    continue
                if not any(keyword.arg == "timeout" for keyword in node.keywords):
                    relative = source_path.relative_to(REPO_ROOT)
                    ohne_timeout.append(f"{relative}:{node.lineno}")

    assert ohne_timeout == [], "Subprocess-Aufrufe ohne Timeout: " + ", ".join(ohne_timeout)
