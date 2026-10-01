from __future__ import annotations

from pathlib import Path

import schicht1


def test_fehlendes_skript_wird_ehrlich_gemeldet(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.setattr(schicht1, "SCRIPTS_DIR", tmp_path)

    result = schicht1._run_script("fehlt.py", [])

    assert result["ok"] is False
    assert "Skript" in result["message"]
    assert "existiert noch nicht" in result["message"]


def test_schicht1_startet_nur_das_fest_gewaehlte_skript(monkeypatch, tmp_path: Path) -> None:
    script = tmp_path / "probe.py"
    script.write_text("print('schicht1-ok')\n", encoding="utf-8")
    monkeypatch.setattr(schicht1, "SCRIPTS_DIR", tmp_path)

    result = schicht1._run_script("probe.py", ["--nur-liste"])

    assert result["ok"] is True
    assert "schicht1-ok" in result["message"]
