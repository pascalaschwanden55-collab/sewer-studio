from __future__ import annotations

from pathlib import Path

import pytest

from osd_hd_validierung_vorbereiten import WORKSPACE_MARKER, ziel_vorbereiten


def test_ziel_vorbereiten_loescht_nur_markierten_arbeitsordner(tmp_path: Path) -> None:
    fremd = tmp_path / "fremd"
    fremd.mkdir()
    (fremd / "kundendatei.txt").write_text("nicht loeschen", encoding="utf-8")

    with pytest.raises(SystemExit, match="keinen SewerStudio-Arbeitsmarker"):
        ziel_vorbereiten(fremd, force=True)

    assert (fremd / "kundendatei.txt").read_text(encoding="utf-8") == "nicht loeschen"

    arbeitsziel = tmp_path / "arbeitsziel"
    ziel_vorbereiten(arbeitsziel, force=False)
    (arbeitsziel / "alt.txt").write_text("alt", encoding="utf-8")

    ziel_vorbereiten(arbeitsziel, force=True)

    assert not (arbeitsziel / "alt.txt").exists()
    assert (arbeitsziel / WORKSPACE_MARKER).is_file()
    assert (arbeitsziel / "frames").is_dir()
