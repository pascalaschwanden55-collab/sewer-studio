"""Bild-Einordner aus freigegebenen Lernstufen — Rohranfang, Rohrende.

Der Client kann keinen Modellpfad vorgeben. Er nennt nur die Klasse und den
erwarteten Gewicht-Hash; beides muss zu einer Freigabedatei passen.

Eine Freigabe entsteht nur ueber `training/scripts/lernstufe_freigabe.py` und
bindet Gewicht, Lernbestand, Messregel und das Ergebnis einer vorher
festgeschriebenen Abnahme aneinander. Ohne sie laeuft hier nichts — ein
ungemessenes Modell darf nicht mitreden. Der Anlass ist real: Das aktive
Detect-Altmodell lief lange produktiv, bis 2026-07-25 auffiel, dass seine Boxen
kollabiert waren.

Diese Modelle liefern KEINE Boxen. Sie sagen nur, wie sicher das ganze Bild die
Klasse zeigt. Wo im Bild, weiss das Protokoll nicht und damit auch das Modell
nicht.

Das produktive YOLO-Modell im Slot ``YOLO`` wird nicht ersetzt; gerechnet wird
im getrennten Slot ``YOLO_TEST``.
"""

from __future__ import annotations

import hashlib
import json
import logging
import os
import re
import tempfile
from dataclasses import dataclass
from pathlib import Path

from ..config import settings
from ..gpu_manager import ModelSlot, ModelUnloadedError, gpu_manager
from . import yolo_wrapper, yolo_test_slot

logger = logging.getLogger(__name__)

# Nur diese Klassen sind ueberhaupt zugelassen. Eine neue Klasse kommt hier
# bewusst von Hand dazu, damit eine fremde Freigabedatei im Ordner nicht
# automatisch einen neuen Endpunkt aufmacht.
ERLAUBTE_KLASSEN = ("rohranfang", "rohrende")

_KLASSE_PATTERN = re.compile(r"^[a-z][a-z_]{0,31}$")
# Gemeinsam mit bcc_test_wrapper — beide belegen den Slot YOLO_TEST (Audit S-H1).
_predict_lock = yolo_test_slot.PREDICT_LOCK


class LernstufeError(RuntimeError):
    """Erwarteter, benutzerfreundlich meldbarer Fehler."""


@dataclass(frozen=True)
class Lernstufe:
    klasse: str
    gewicht: Path
    gewicht_sha256: str
    precision: float
    recall: float
    regel: str
    freigabe_sha256: str


def _ist_link(pfad: Path) -> bool:
    try:
        return pfad.is_symlink() or bool(
            os.stat(pfad, follow_symlinks=False).st_file_attributes & 0x400)
    except (OSError, AttributeError):
        return pfad.is_symlink()


def _sha256(pfad: Path) -> str:
    h = hashlib.sha256()
    with pfad.open("rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def _ist_sha256(wert: object) -> bool:
    return isinstance(wert, str) and re.fullmatch(r"[0-9a-fA-F]{64}", wert) is not None


def _freigabe_lesen(datei: Path) -> Lernstufe | None:
    """Liest eine Freigabe streng. Alles Unklare gibt None statt einer Ausnahme.

    So sperrt eine einzelne fremde oder halbfertige Datei im Ordner nicht die
    uebrigen Klassen.
    """
    try:
        if not datei.is_file() or _ist_link(datei):
            return None
        roh = datei.read_bytes()
        sha_datei = datei.with_suffix(".sha256")
        if not sha_datei.is_file():
            return None
        if hashlib.sha256(roh).hexdigest() != sha_datei.read_text(encoding="utf-8").strip():
            logger.warning("Freigabe %s passt nicht zu ihrem Hash", datei.name)
            return None
        d = json.loads(roh.decode("utf-8-sig"))
    except (OSError, ValueError):
        return None

    if d.get("schema") != "lernstufe_freigabe_v1" or d.get("status") != "freigegeben":
        return None
    klasse = d.get("klasse")
    if not isinstance(klasse, str) or _KLASSE_PATTERN.fullmatch(klasse) is None:
        return None
    if klasse not in ERLAUBTE_KLASSEN:
        return None
    if not _ist_sha256(d.get("gewicht_sha256")):
        return None

    gewicht = Path(str(d.get("gewicht", "")))
    try:
        if not gewicht.is_file() or _ist_link(gewicht):
            return None
        if _sha256(gewicht).lower() != str(d["gewicht_sha256"]).lower():
            logger.warning("Gewicht von %s weicht von seiner Freigabe ab", klasse)
            return None
    except OSError:
        return None

    abnahme = d.get("abnahme") or {}
    try:
        precision = float(abnahme["precision"])
        recall = float(abnahme["recall"])
    except (KeyError, TypeError, ValueError):
        return None

    return Lernstufe(
        klasse=klasse,
        gewicht=gewicht,
        gewicht_sha256=str(d["gewicht_sha256"]).lower(),
        precision=precision,
        recall=recall,
        regel=str((d.get("regel") or {}).get("vorschlag") or ""),
        freigabe_sha256=hashlib.sha256(roh).hexdigest(),
    )


def freigegebene_lernstufen() -> list[Lernstufe]:
    """Alle gueltigen Freigaben, nach Klassennamen sortiert."""
    wurzel = Path(settings.lernstufe_freigaben_root)
    try:
        aufgeloest = wurzel.resolve(strict=True)
    except OSError as exc:
        raise LernstufeError("Der Freigabe-Ordner ist nicht verfuegbar.") from exc
    if not aufgeloest.is_dir() or _ist_link(aufgeloest):
        raise LernstufeError("Der Freigabe-Ordner ist nicht sicher lesbar.")
    try:
        dateien = sorted(aufgeloest.glob("*.json"))
    except OSError as exc:
        raise LernstufeError("Der Freigabe-Ordner ist nicht lesbar.") from exc

    stufen = [s for s in (_freigabe_lesen(d) for d in dateien) if s is not None]
    if not stufen:
        raise LernstufeError("Keine gueltige Lernstufen-Freigabe gefunden.")
    # Je Klasse hoechstens eine: bei zwei Dateien derselben Klasse ist unklar,
    # welche gilt — dann lieber keine.
    zaehler: dict[str, int] = {}
    for s in stufen:
        zaehler[s.klasse] = zaehler.get(s.klasse, 0) + 1
    eindeutig = [s for s in stufen if zaehler[s.klasse] == 1]
    for klasse, n in zaehler.items():
        if n > 1:
            logger.warning("Klasse %s hat %d Freigaben — gesperrt", klasse, n)
    if not eindeutig:
        raise LernstufeError("Keine eindeutige Lernstufen-Freigabe gefunden.")
    return sorted(eindeutig, key=lambda s: s.klasse)


def waehlen(klasse: str, erwarteter_sha256: str) -> Lernstufe:
    """Waehlt genau die Klasse mit genau diesem Gewicht-Hash."""
    if _KLASSE_PATTERN.fullmatch(klasse or "") is None or not _ist_sha256(erwarteter_sha256):
        raise LernstufeError("Klasse oder SHA-256 sind ungueltig.")
    gesucht = erwarteter_sha256.lower()
    for s in freigegebene_lernstufen():
        if s.klasse == klasse and s.gewicht_sha256 == gesucht:
            return s
    raise LernstufeError(f"Keine freigegebene Lernstufe {klasse!r} mit diesem Hash.")


def _geraet() -> str:
    device = settings.effective_yolo_device
    if device.startswith("cuda") and not yolo_wrapper._cuda_available():
        return "cpu"
    return device


def _laden(stufe: Lernstufe, device: str):
    from ultralytics import YOLO

    # Wie beim BCC-Kandidaten: genau ein gelesener Byte-Strom geht in eine
    # private Momentaufnahme. YOLO oeffnet nie den veraenderbaren Originalpfad.
    try:
        with tempfile.TemporaryDirectory(prefix="sewerstudio_lernstufe_") as tmp:
            kopie = Path(tmp) / "gewicht.pt"
            digest = hashlib.sha256()
            with stufe.gewicht.open("rb") as quelle, kopie.open("xb") as ziel:
                for block in iter(lambda: quelle.read(1 << 20), b""):
                    digest.update(block)
                    ziel.write(block)
            if digest.hexdigest().lower() != stufe.gewicht_sha256:
                raise LernstufeError("Der Hash des Gewichts hat sich vor dem Laden geaendert.")
            modell = YOLO(str(kopie))
            namen = {int(k): str(v).strip() for k, v in dict(modell.names).items()}
            if stufe.klasse not in namen.values():
                raise LernstufeError(
                    f"Das Gewicht kennt die Klasse {stufe.klasse!r} nicht: {sorted(namen.values())}")
            if _sha256(kopie).lower() != stufe.gewicht_sha256:
                raise LernstufeError("Die private Modellkopie hat sich waehrend des Ladens geaendert.")
            modell.to(device)
            return modell, None
    except LernstufeError:
        raise
    except OSError as exc:
        raise LernstufeError("Die gepruefte Modellkopie konnte nicht erstellt werden.") from exc


def einordnen(image_base64: str, klasse: str, erwarteter_sha256: str, imgsz: int = 640) -> dict:
    """Sagt, wie sicher das ganze Bild die Klasse zeigt. Keine Box."""
    stufe = waehlen(klasse, erwarteter_sha256)
    bild = yolo_wrapper.decode_image(image_base64)
    device = _geraet()

    # Das Lock teilen sich beide Nutzer des Slots YOLO_TEST (Audit S-H1).
    with _predict_lock:
        # Fremden Inhalt vor der Lease raeumen — eine eigene Lease wuerde das
        # Entladen sonst selbst sperren. Welches Gewicht drinliegt, weiss allein
        # der Slot; eine Modulvariable hier saehe den Wechsel des BCC-Wrappers nicht.
        gpu_manager.discard_foreign_content(
            ModelSlot.YOLO_TEST, stufe.gewicht_sha256)

        besitzer = gpu_manager.acquire_busy(ModelSlot.YOLO_TEST)
        try:
            # Laden UNTER der Lease (Muster aus bcc_test_wrapper, Paket 2): sonst
            # sieht der Waechter den Ladevorgang nicht und ein paralleler
            # OOM-Evict koennte das Modell dazwischen entfernen.
            zustand = gpu_manager.ensure_loaded(
                ModelSlot.YOLO_TEST, device, lambda: _laden(stufe, device),
                content_id=stufe.gewicht_sha256)
            modell = zustand.model
            if modell is None:
                # Unload-Race: kontrollierter 503 statt AttributeError/500.
                raise ModelUnloadedError(ModelSlot.YOLO_TEST.value)
            eingabe = yolo_wrapper._pil_rgb_to_ultralytics_bgr(bild)
            ergebnis = modell.predict(source=eingabe, imgsz=imgsz, verbose=False)[0]
            namen = {int(k): str(v).strip() for k, v in dict(modell.names).items()}
            index = next(i for i, n in namen.items() if n == stufe.klasse)
            wert = float(ergebnis.probs.data[index])
        finally:
            gpu_manager.release_busy(ModelSlot.YOLO_TEST, besitzer)

    return {
        "klasse": stufe.klasse,
        "konfidenz": wert,
        "gewicht_sha256": stufe.gewicht_sha256,
        "freigabe_sha256": stufe.freigabe_sha256,
        "precision": stufe.precision,
        "recall": stufe.recall,
        "device": device,
    }
