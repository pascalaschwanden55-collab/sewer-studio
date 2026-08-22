from __future__ import annotations

import ast
from pathlib import Path


SYNCED_NAMES = {
    "_MEAN",
    "_STD",
    "letterbox_pil",
    "Letterbox",
    "build_val_tf",
    "build_train_tf",
}


def _synced_ast(path: Path) -> dict[str, str]:
    tree = ast.parse(path.read_text(encoding="utf-8"))
    result: dict[str, str] = {}
    for node in tree.body:
        name = None
        if isinstance(node, (ast.FunctionDef, ast.ClassDef)):
            name = node.name
        elif isinstance(node, ast.Assign) and len(node.targets) == 1:
            target = node.targets[0]
            if isinstance(target, ast.Name):
                name = target.id
        if name in SYNCED_NAMES:
            result[name] = ast.dump(node, include_attributes=False)
    return result


def test_sidecar_compat_bleibt_zum_training_transform_synchron() -> None:
    repo = Path(__file__).resolve().parents[2]
    sidecar_copy = repo / "sidecar" / "sidecar" / "models" / "nocrop_compat.py"
    training_source = repo / "training" / "vsa_classifier" / "nocrop_patch.py"

    assert _synced_ast(sidecar_copy) == _synced_ast(training_source)
