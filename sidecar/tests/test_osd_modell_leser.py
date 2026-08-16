"""osd_modell_leser: Gewichts-SHA-256-Pruefung VOR dem Laden (Ruling zu Aufgabe 7).

Nur der fail-closed Pfad wird hier geprueft - kein trainierter Kandidat noetig
und keiner vorhanden. Echte Inferenz bleibt bewusst ungetestet, bis ein
Kandidat existiert (Aufgabe 5 laeuft erst spaeter auf echter Hardware).

Fix-Runde 1 zu Aufgabe 7 (2026-08-16) ergaenzt zwei weitere reine Tests, beide
ohne Modell/Bild: dass _YOLO_CONF nie wieder mit GRUNDSCHWELLE zusammenfaellt
(das machte das Sicherheitstor wirkungslos), und dass der ermittelte Stil
tatsaechlich bei parse_meter ankommt statt fest ueberschrieben zu werden.
"""

import json
import sys
from pathlib import Path

import pytest

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_modell_leser as leser_modul


class _VerbotenesModul:
    """Steht in sys.modules['ultralytics'] und knallt bei jedem Zugriff.

    Wird die SHA-256-Pruefung korrekt VOR dem Laden ausgeloest, wird dieses
    Attribut nie gelesen. Wuerde baue_modell_leser trotz falschem Hash
    versuchen, das Modell zu laden, faellt der Test mit dieser
    AssertionError durch statt mit der erwarteten ValueError - kein echtes
    ultralytics noetig.
    """

    def __getattr__(self, name):
        raise AssertionError(
            "ultralytics haette bei falschem Gewichts-Hash nicht importiert "
            f"werden duerfen (Zugriff auf '{name}')")


def _kandidat(tmp_path: Path, gewicht_bytes: bytes, manifest_sha256: str) -> Path:
    kandidat = tmp_path / "kandidat"
    (kandidat / "weights").mkdir(parents=True)
    (kandidat / "weights" / "best.pt").write_bytes(gewicht_bytes)
    manifest = {
        "gewicht_datei": "weights/best.pt",
        "gewicht_sha256": manifest_sha256,
        "imgsz": 320,
    }
    (kandidat / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    return kandidat


def test_abweichender_gewichts_hash_bricht_vor_dem_laden_ab(tmp_path, monkeypatch):
    kandidat = _kandidat(tmp_path, b"nicht die echten Gewichte", "falscher_hash")
    monkeypatch.setitem(sys.modules, "ultralytics", _VerbotenesModul())

    with pytest.raises(ValueError, match="SHA-256"):
        leser_modul.baue_modell_leser(kandidat, schwelle=0.0)


def test_fehlermeldung_nennt_erwarteten_und_gefundenen_hash(tmp_path, monkeypatch):
    kandidat = _kandidat(tmp_path, b"andere-bytes", "0000000000000000")
    monkeypatch.setitem(sys.modules, "ultralytics", _VerbotenesModul())

    with pytest.raises(ValueError) as fehler:
        leser_modul.baue_modell_leser(kandidat, schwelle=0.0)

    text = str(fehler.value)
    assert "0000000000000000" in text
    # Der tatsaechliche SHA-256 von b"andere-bytes" wird ebenfalls genannt.
    import hashlib
    assert hashlib.sha256(b"andere-bytes").hexdigest() in text


def test_code_hashes_bindet_die_drei_massgeblichen_module():
    """Aufgabe 4: Gewicht + Schwelle binden nur das MODELL - ZIEL_HOEHE,
    _IOU_SCHWELLE, TOR_MINDESTZEICHEN, _YOLO_CONF und der Zuschnitt aendern
    die Lesung mit demselben Gewicht ebenso."""
    hashes = leser_modul.code_hashes()

    assert set(hashes) == {"osd_modell_leser.py", "osd_modell.py", "osd_meter.py"}
    for wert in hashes.values():
        assert len(wert) == 64


def test_yolo_conf_bleibt_unter_der_grundschwelle():
    """_YOLO_CONF und GRUNDSCHWELLE duerfen nie wieder zusammenfallen.

    Fix-Runde 1 zu Aufgabe 7: Bei _YOLO_CONF == GRUNDSCHWELLE (frueher beide
    0.25) hat predict() jedes Zeichen unter der Grundschwelle schon VOR
    zu_zeichenfolge verworfen. Jede zustandekommende Lesung hatte dadurch
    zwangslaeufig konfidenz_min >= GRUNDSCHWELLE, und "konfidenz_min >=
    schwelle" pruefte nichts mehr, was der Boxfilter nicht schon vorher
    weggenommen hatte, sobald die Kalibrierung die Grundschwelle einfror.
    """
    import osd_schwelle_kalibrieren as kal

    assert leser_modul._YOLO_CONF < kal.GRUNDSCHWELLE


def test_stil_aus_glyphenmaske_wird_an_parse_meter_durchgereicht():
    """Fix-Runde 1 zu Aufgabe 7: stil stand vorher fest auf "dunkel" - geraten,
    nicht ermittelt.

    Ohne Trenner ("007") laesst osd_meter.parse_meter im Auto-Format die
    punktlose Form NUR bei stil == "dunkel" durch (siehe dort: "if format ==
    FORMAT_AUTO and stil != 'dunkel': return None"). Das macht den
    durchgereichten Stil beobachtbar, ganz ohne Bild oder Modell - getestet
    wird die reine Logik _ergebnis_aus_erkennungen, die lese() nach der
    Inferenz aufruft.
    """
    # Klassen 0 und 7 sind laut osd_meter.ZEICHEN ("0123456789.mLZ:") die
    # Ziffern '0' und '7'; drei nicht ueberlappende Boxen links nach rechts
    # ergeben die Zeichenfolge "007".
    erkennungen = [
        (0, 0.2, 0.5, 0.1, 0.3, 0.9),
        (0, 0.5, 0.5, 0.1, 0.3, 0.9),
        (7, 0.8, 0.5, 0.1, 0.3, 0.9),
    ]

    dunkel = leser_modul._ergebnis_aus_erkennungen(erkennungen, "dunkel", schwelle=0.0)
    hell = leser_modul._ergebnis_aus_erkennungen(erkennungen, "hell", schwelle=0.0)

    assert dunkel["zeichenfolge"] == "007"
    assert dunkel["stil"] == "dunkel"
    assert dunkel["meter"] == pytest.approx(0.7)
    assert dunkel["leseweg"] == "modell"

    # Derselbe Rohbefund, aber mit Stil "hell" durchgereicht: parse_meter
    # verweigert die punktlose Form dann bewusst - waere stil hier weiterhin
    # fest auf "dunkel" gesetzt, wuerde dieser Fall faelschlich durchgehen.
    assert hell["stil"] == "hell"
    assert hell["meter"] is None
    assert hell["leseweg"] is None
