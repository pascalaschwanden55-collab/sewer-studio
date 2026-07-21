"""Fokussierte Tests fuer die Guardrails (Kernlogik, ohne LLM-SDK).

Lauf:  python -m pytest test_guardrails.py   (oder)   python test_guardrails.py
"""
from __future__ import annotations

from pathlib import Path

import guardrails as g


def test_is_sealed_split_erkennt_abnahme_und_gold():
    assert g.is_sealed_split("abnahme")
    assert g.is_sealed_split("testset_gold")
    assert g.is_sealed_split("Holdout-2026")
    assert g.is_sealed_split("versiegelt_v1")


def test_is_sealed_split_laesst_devval_durch():
    assert not g.is_sealed_split("devval")
    assert not g.is_sealed_split("train")
    assert not g.is_sealed_split("v003_bootstrap")


def test_assert_eval_split_allowed_blockt_abnahme():
    raised = False
    try:
        g.assert_eval_split_allowed("abnahme")
    except g.GuardrailViolation:
        raised = True
    assert raised, "Abnahme-Split muss eine GuardrailViolation werfen"


def test_assert_eval_split_allowed_erlaubt_devval():
    g.assert_eval_split_allowed("devval")  # darf NICHT werfen


def test_path_is_within():
    root = Path("/a/b")
    assert g.path_is_within(Path("/a/b/c/report.md"), root)
    assert not g.path_is_within(Path("/a/x/report.md"), root)


def test_assert_write_allowed_blockt_ausserhalb():
    raised = False
    try:
        g.assert_write_allowed(Path("/tmp/evil.md"), Path("/a/b/reports"))
    except g.GuardrailViolation:
        raised = True
    assert raised


def _run_all():
    passed = 0
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            fn()
            passed += 1
            print(f"  ok  {name}")
    print(f"\n{passed} Tests bestanden.")


if __name__ == "__main__":
    _run_all()
