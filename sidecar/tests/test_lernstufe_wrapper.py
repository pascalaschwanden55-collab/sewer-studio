"""Der Lernstufen-Wrapper darf nur hashgebundene, freigegebene Modelle zulassen."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import pytest

from sidecar.models import lernstufe_wrapper as lw


def _freigabe_schreiben(ordner: Path, klasse: str, gewicht: Path,
                        dateiname: str | None = None, **abweichung) -> Path:
    doc = {
        "schema": "lernstufe_freigabe_v1",
        "status": "freigegeben",
        "klasse": klasse,
        "gewicht": str(gewicht),
        "gewicht_sha256": hashlib.sha256(gewicht.read_bytes()).hexdigest(),
        "regel": {"vorschlag": "staerkste Meldung je Video"},
        "abnahme": {"precision": 0.855, "recall": 0.978},
    }
    doc.update(abweichung)
    text = json.dumps(doc, indent=1, ensure_ascii=False)
    ziel = ordner / (dateiname or f"{klasse}_v1.json")
    ziel.write_bytes(text.encode("utf-8"))
    ziel.with_suffix(".sha256").write_bytes(
        (hashlib.sha256(text.encode("utf-8")).hexdigest() + "\n").encode("utf-8"))
    return ziel


@pytest.fixture()
def freigabeordner(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> Path:
    ordner = tmp_path / "freigaben"
    ordner.mkdir()
    monkeypatch.setattr(lw.settings, "lernstufe_freigaben_root", str(ordner))
    return ordner


def _gewicht(tmp_path: Path, name: str = "best.pt") -> Path:
    p = tmp_path / name
    p.write_bytes(b"kein echtes gewicht, nur bytes")
    return p


def test_gueltige_freigabe_wird_gelesen(freigabeordner: Path, tmp_path: Path) -> None:
    g = _gewicht(tmp_path)
    _freigabe_schreiben(freigabeordner, "rohranfang", g)
    stufen = lw.freigegebene_lernstufen()
    assert [s.klasse for s in stufen] == ["rohranfang"]
    assert stufen[0].precision == pytest.approx(0.855)


def test_veraenderte_freigabe_wird_verworfen(freigabeordner: Path, tmp_path: Path) -> None:
    """Die Datei ist an ihren eigenen Hash gebunden."""
    g = _gewicht(tmp_path)
    datei = _freigabe_schreiben(freigabeordner, "rohranfang", g)
    doc = json.loads(datei.read_text(encoding="utf-8-sig"))
    doc["abnahme"]["precision"] = 0.99          # geschoente Zahl
    datei.write_bytes(json.dumps(doc, indent=1, ensure_ascii=False).encode("utf-8"))
    with pytest.raises(lw.LernstufeError):
        lw.freigegebene_lernstufen()


def test_veraendertes_gewicht_wird_verworfen(freigabeordner: Path, tmp_path: Path) -> None:
    g = _gewicht(tmp_path)
    _freigabe_schreiben(freigabeordner, "rohranfang", g)
    g.write_bytes(b"ein anderes gewicht")
    with pytest.raises(lw.LernstufeError):
        lw.freigegebene_lernstufen()


def test_nicht_freigegebener_status_wird_verworfen(freigabeordner: Path, tmp_path: Path) -> None:
    _freigabe_schreiben(freigabeordner, "rohranfang", _gewicht(tmp_path), status="kandidat")
    with pytest.raises(lw.LernstufeError):
        lw.freigegebene_lernstufen()


def test_unbekannte_klasse_oeffnet_keinen_endpunkt(freigabeordner: Path, tmp_path: Path) -> None:
    """Eine fremde Freigabedatei im Ordner darf keine neue Klasse freischalten."""
    _freigabe_schreiben(freigabeordner, "erfundeneklasse", _gewicht(tmp_path))
    with pytest.raises(lw.LernstufeError):
        lw.freigegebene_lernstufen()


def test_zwei_freigaben_derselben_klasse_sperren_sie(freigabeordner: Path, tmp_path: Path) -> None:
    """Bei zwei Dateien ist unklar, welche gilt — dann lieber keine."""
    _freigabe_schreiben(freigabeordner, "rohranfang", _gewicht(tmp_path, "a.pt"))
    _freigabe_schreiben(freigabeordner, "rohranfang", _gewicht(tmp_path, "b.pt"),
                        dateiname="rohranfang_v2.json")
    with pytest.raises(lw.LernstufeError):
        lw.freigegebene_lernstufen()


def test_eine_kaputte_datei_sperrt_die_uebrigen_nicht(freigabeordner: Path, tmp_path: Path) -> None:
    _freigabe_schreiben(freigabeordner, "rohranfang", _gewicht(tmp_path, "a.pt"))
    (freigabeordner / "kaputt.json").write_bytes(b"{kein json")
    assert [s.klasse for s in lw.freigegebene_lernstufen()] == ["rohranfang"]


def test_waehlen_verlangt_den_richtigen_hash(freigabeordner: Path, tmp_path: Path) -> None:
    g = _gewicht(tmp_path)
    _freigabe_schreiben(freigabeordner, "rohranfang", g)
    echt = hashlib.sha256(g.read_bytes()).hexdigest()
    assert lw.waehlen("rohranfang", echt).klasse == "rohranfang"
    with pytest.raises(lw.LernstufeError):
        lw.waehlen("rohranfang", "0" * 64)
    with pytest.raises(lw.LernstufeError):
        lw.waehlen("rohrende", echt)


@pytest.mark.parametrize("klasse", ["Rohranfang", "rohr anfang", "", "a" * 40])
def test_ungueltige_klassennamen_werden_abgewiesen(klasse: str, freigabeordner: Path) -> None:
    with pytest.raises(lw.LernstufeError):
        lw.waehlen(klasse, "a" * 64)
