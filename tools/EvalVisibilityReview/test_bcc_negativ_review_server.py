"""Tests fuer den Pruefplatz der automatischen Negativbilder."""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent))

from bcc_negativ_review_server import NegativReviewStore, URTEILE  # noqa: E402


def _queue_bauen(wurzel: Path, anzahl: int = 3) -> Path:
    (wurzel / "bilder").mkdir(parents=True)
    faelle = []
    for i in range(1, anzahl + 1):
        # Kein echtes JPEG noetig: Der Store prueft Bytes gegen den Hash.
        daten = f"bild-{i}".encode()
        (wurzel / "bilder" / f"{i:02d}.jpg").write_bytes(daten)
        faelle.append({
            "nummer": i, "bild": f"bilder/{i:02d}.jpg",
            "bild_sha256": hashlib.sha256(daten).hexdigest(),
            "haltung": f"h-{i}", "video": "x.mpg", "sekunde": 1.0, "split": "train",
        })
    doc = {"schema": "bcc_negativpruefung_v1", "frage": "Bogen sichtbar?",
           "lernbestand": "L", "lernbestand_manifest_sha256": "a" * 64, "faelle": faelle}
    text = json.dumps(doc, indent=1, ensure_ascii=False)
    # Bytes schreiben statt write_text: Unter Windows wandelt write_text \n in
    # \r\n um, und der Hash passt dann nicht mehr zu den Bytes auf der Platte.
    # Genau dieser Fehler steckte im ersten eingefrorenen Bestand.
    rohbytes = text.encode("utf-8")
    (wurzel / "queue.json").write_bytes(rohbytes)
    (wurzel / "queue.sha256").write_text(
        hashlib.sha256(rohbytes).hexdigest() + "\n", encoding="utf-8")
    return wurzel


def test_veraenderte_queue_wird_abgewiesen(tmp_path: Path) -> None:
    """Eine nachtraeglich veraenderte Stichprobe ist keine Stichprobe mehr."""
    q = _queue_bauen(tmp_path / "q")
    daten = json.loads((q / "queue.json").read_text(encoding="utf-8"))
    daten["faelle"] = daten["faelle"][:2]
    (q / "queue.json").write_bytes(json.dumps(daten, indent=1).encode("utf-8"))

    with pytest.raises(ValueError, match="Hash"):
        NegativReviewStore(q, tmp_path / "review.json", "Pascal")


def test_veraendertes_bild_wird_nicht_gezeigt(tmp_path: Path) -> None:
    """Beurteilt werden muss genau das Bild aus dem Lernbestand."""
    q = _queue_bauen(tmp_path / "q")
    store = NegativReviewStore(q, tmp_path / "review.json", "Pascal")
    (q / "bilder" / "02.jpg").write_bytes(b"etwas anderes")

    assert store.bild(1)  # unveraendert, wird geliefert
    with pytest.raises(ValueError, match="weicht von seinem Hash ab"):
        store.bild(2)


def test_unzulaessiges_urteil_wird_abgewiesen(tmp_path: Path) -> None:
    q = _queue_bauen(tmp_path / "q")
    store = NegativReviewStore(q, tmp_path / "review.json", "Pascal")
    with pytest.raises(ValueError, match="Unzulaessiges Urteil"):
        store.urteilen(1, "vielleicht")
    assert set(URTEILE) == {"bogen_sichtbar", "kein_bogen", "unsicher"}


def test_fortsetzen_uebernimmt_bisherige_urteile(tmp_path: Path) -> None:
    """Ein Neustart darf keine Arbeit verlieren und keine doppelt zeigen."""
    q = _queue_bauen(tmp_path / "q")
    ziel = tmp_path / "review.json"
    store = NegativReviewStore(q, ziel, "Pascal")
    store.urteilen(1, "kein_bogen")
    store.urteilen(2, "bogen_sichtbar")

    weiter = NegativReviewStore(q, ziel, "Pascal")
    assert weiter.naechster()["nummer"] == 3
    assert weiter.naechster()["erledigt"] == 2


def test_fremde_review_wird_nicht_ueberschrieben(tmp_path: Path) -> None:
    """Eine Review aus einer anderen Stichprobe darf nicht stillschweigend weiterlaufen."""
    q1 = _queue_bauen(tmp_path / "q1")
    q2 = _queue_bauen(tmp_path / "q2", anzahl=4)
    ziel = tmp_path / "review.json"
    NegativReviewStore(q1, ziel, "Pascal").urteilen(1, "kein_bogen")

    with pytest.raises(ValueError, match="anderen Queue"):
        NegativReviewStore(q2, ziel, "Pascal")


def test_fehlerquote_zaehlt_unsichere_nicht_mit(tmp_path: Path) -> None:
    q = _queue_bauen(tmp_path / "q", anzahl=4)
    ziel = tmp_path / "review.json"
    store = NegativReviewStore(q, ziel, "Pascal")
    store.urteilen(1, "bogen_sichtbar")
    store.urteilen(2, "kein_bogen")
    store.urteilen(3, "kein_bogen")
    store.urteilen(4, "unsicher")

    d = json.loads(ziel.read_text(encoding="utf-8"))
    assert d["zusammenfassung"] == {"bogen_sichtbar": 1, "kein_bogen": 2, "unsicher": 1}
    assert d["fehlerquote_der_negativen"] == pytest.approx(1 / 3, abs=1e-4)  # der Store rundet auf vier Stellen
    assert d["vollstaendig"] is True


def test_reviewer_ist_pflicht(tmp_path: Path) -> None:
    q = _queue_bauen(tmp_path / "q")
    with pytest.raises(ValueError, match="Reviewer"):
        NegativReviewStore(q, tmp_path / "review.json", "   ")


def test_server_bleibt_bei_stillen_verbindungen_erreichbar(tmp_path: Path) -> None:
    """Der Pruefplatz darf nicht nach dem ersten Bild stehenbleiben.

    Chrome und Edge oeffnen vorsorglich Verbindungen, ohne sofort eine Anfrage
    zu senden. Ein einfaediger Server nimmt so eine an, wartet auf eine Anfrage,
    die nie kommt, und blockiert alles Weitere. Am 2026-08-10 genau so
    aufgetreten: ein Bild beurteilbar, danach nichts mehr.
    """
    import socket
    import threading
    import urllib.request

    import bcc_negativ_review_server as modul

    q = _queue_bauen(tmp_path / "q", anzahl=3)
    faden = threading.Thread(
        target=modul.run_server,
        args=(q, tmp_path / "review.json", "Pascal", 0),
        kwargs={"browser_oeffnen": False},
        daemon=True,
    )
    # Der Port wird erst beim Binden bekannt; deshalb ueber einen eigenen Server.
    store = modul.NegativReviewStore(q, tmp_path / "review.json", "Pascal")

    class Server(__import__("socketserver").ThreadingTCPServer):
        daemon_threads = True

    server = Server(("127.0.0.1", 0), modul.create_handler(store))
    port = server.server_address[1]
    threading.Thread(target=server.serve_forever, daemon=True).start()
    del faden

    stumme = [socket.create_connection(("127.0.0.1", port)) for _ in range(4)]
    try:
        for _ in range(3):
            antwort = urllib.request.urlopen(f"http://127.0.0.1:{port}/naechster", timeout=5)
            fall = json.loads(antwort.read())
            if fall.get("fertig"):
                break
            store.urteilen(int(fall["nummer"]), "kein_bogen")
        assert store.naechster()["fertig"] is True
    finally:
        for s in stumme:
            s.close()
        server.shutdown()
        server.server_close()


def _queue_mit_clip(wurzel: Path) -> Path:
    """Warteschlange mit genau einem Fall, der einen Clip traegt."""
    _queue_bauen(wurzel, anzahl=1)
    (wurzel / "clips").mkdir()
    (wurzel / "clips" / "01.mp4").write_bytes(b"nicht-echt-aber-bytes")
    doc = json.loads((wurzel / "queue.json").read_text(encoding="utf-8-sig"))
    doc["faelle"][0]["clip"] = "clips/01.mp4"
    rohbytes = json.dumps(doc, indent=1, ensure_ascii=False).encode("utf-8")
    (wurzel / "queue.json").write_bytes(rohbytes)
    (wurzel / "queue.sha256").write_text(
        hashlib.sha256(rohbytes).hexdigest() + "\n", encoding="utf-8")
    return wurzel


def test_clip_wird_ausgeliefert_und_angekuendigt(tmp_path: Path) -> None:
    store = NegativReviewStore(
        _queue_mit_clip(tmp_path / "q"), tmp_path / "review.json", "Pascal")
    assert store.naechster()["clip"] is True
    assert store.clip(1) == b"nicht-echt-aber-bytes"


def test_ohne_clip_bleibt_es_beim_standbild(tmp_path: Path) -> None:
    store = NegativReviewStore(
        _queue_bauen(tmp_path / "q"), tmp_path / "review.json", "Pascal")
    assert store.naechster()["clip"] is False
    with pytest.raises(KeyError):
        store.clip(1)


def test_clip_ausserhalb_der_warteschlange_wird_abgewiesen(tmp_path: Path) -> None:
    """Der Pfad kommt aus einer Datei — er darf nicht aus dem Ordner zeigen."""
    wurzel = _queue_mit_clip(tmp_path / "q")
    (tmp_path / "fremd.mp4").write_bytes(b"fremd")
    doc = json.loads((wurzel / "queue.json").read_text(encoding="utf-8-sig"))
    doc["faelle"][0]["clip"] = "../fremd.mp4"
    rohbytes = json.dumps(doc, indent=1, ensure_ascii=False).encode("utf-8")
    (wurzel / "queue.json").write_bytes(rohbytes)
    (wurzel / "queue.sha256").write_text(
        hashlib.sha256(rohbytes).hexdigest() + "\n", encoding="utf-8")
    store = NegativReviewStore(wurzel, tmp_path / "review.json", "Pascal")
    with pytest.raises(ValueError):
        store.clip(1)
