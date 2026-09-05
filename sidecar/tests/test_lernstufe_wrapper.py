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


def test_einordnen_letterboxt_das_bild_wie_die_abnahme(
        freigabeordner: Path, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    """Die Freigabe wurde mit letterbox_pil(640) VOR dem predict gemessen.

    Das Gewicht traegt nur Resize+CenterCrop: Ohne Letterbox schneidet Ultralytics
    von einem 720x576-Bild links und rechts je 80 Pixel ab und misst ein anderes
    Modell als das freigegebene (Gegenprobe 2026-09-04: bis 0,79 Abweichung je Bild,
    Spitzenmoment verschoben).
    """
    import base64
    import io

    import numpy as np
    from PIL import Image

    from sidecar.models import yolo_wrapper as yw

    gewicht = _gewicht(tmp_path)
    _freigabe_schreiben(freigabeordner, "rohranfang", gewicht)
    sha = hashlib.sha256(gewicht.read_bytes()).hexdigest()

    class FakeProbs:
        data = [0.25, 0.75]

    class FakeErgebnis:
        probs = FakeProbs()

    class FakeModell:
        names = {0: "kein_rohranfang", 1: "rohranfang"}

        def __init__(self) -> None:
            self.quellen: list[tuple[object, int]] = []

        def predict(self, source, imgsz, verbose):  # noqa: ANN001
            self.quellen.append((source, imgsz))
            return [FakeErgebnis()]

    modell = FakeModell()

    class FakeZustand:
        model = modell

    class FakeGpu:
        def discard_foreign_content(self, slot, content_id):  # noqa: ANN001
            return None

        def acquire_busy(self, slot):  # noqa: ANN001
            return "besitzer"

        def release_busy(self, slot, besitzer):  # noqa: ANN001
            return None

        def ensure_loaded(self, slot, device, loader, content_id=None):  # noqa: ANN001
            return FakeZustand()

    monkeypatch.setattr(lw, "gpu_manager", FakeGpu())
    monkeypatch.setattr(lw, "_geraet", lambda: "cpu")

    # SD-Bild 720x576 mit rotem linken Rand — genau der Streifen, den ein CenterCrop verliert.
    bild = Image.new("RGB", (720, 576), (40, 40, 40))
    for x in range(40):
        for y in range(576):
            bild.putpixel((x, y), (255, 0, 0))
    puffer = io.BytesIO()
    bild.save(puffer, format="PNG")
    b64 = base64.b64encode(puffer.getvalue()).decode("ascii")

    ergebnis = lw.einordnen(b64, "rohranfang", sha, imgsz=640)

    assert ergebnis["konfidenz"] == pytest.approx(0.75)
    ((quelle, imgsz),) = modell.quellen
    assert imgsz == 640
    assert quelle.shape[:2] == (640, 640)
    erwartet = np.ascontiguousarray(np.asarray(yw._letterbox_rgb(bild, 640))[:, :, ::-1])
    assert np.array_equal(quelle, erwartet)
    # Der rote Rand liegt im letterboxten Bild links bei Spalte 0..35, Zeile 64..575 (BGR: Rot = Kanal 2).
    assert int(quelle[300, 10, 2]) == 255 and int(quelle[300, 10, 0]) == 0
