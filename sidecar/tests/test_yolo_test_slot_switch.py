"""Slot YOLO_TEST: zwei Wrapper, ein GPU-Platz (Audit 2026-08-14, Befund S-H1).

`bcc_test_wrapper` und `lernstufe_wrapper` teilen sich denselben Slot. Vorher merkte
sich jedes Modul in einer eigenen Modulvariablen, welches Gewicht es zuletzt geladen
hatte — voneinander wussten die beiden nichts. Nach einem Wechsel sah jedes Modul
seinen eigenen Hash unveraendert, entlud nicht und inferierte auf dem FREMDEN Modell:
bcc bekam still ein leeres Ergebnis (`boxes is None`), lernstufe einen 500er.

Die Identitaet des geladenen Modells gehoert deshalb an den Slot selbst
(`SlotState.content_id`), nicht in die Wrapper. Zusaetzlich serialisiert ein
gemeinsames Predict-Lock die Folge Laden->Inferenz beider Wrapper gegeneinander.
"""

import threading

import pytest

from sidecar.gpu_manager import GpuModelManager, ModelSlot, ModelUnloadedError


class _FakeModel:
    """Steht fuer ein geladenes Gewicht; `kennung` sagt, welches es ist."""

    def __init__(self, kennung: str) -> None:
        self.kennung = kennung


def _lader(kennung: str, protokoll: list[str]):
    def laden():
        protokoll.append(kennung)
        return _FakeModel(kennung), None

    return laden


def test_gleiche_inhaltskennung_laedt_nicht_neu():
    manager = GpuModelManager()
    protokoll: list[str] = []

    erst = manager.ensure_loaded(
        ModelSlot.YOLO_TEST, "cpu", _lader("bcc", protokoll), content_id="sha-bcc")
    zweit = manager.ensure_loaded(
        ModelSlot.YOLO_TEST, "cpu", _lader("bcc", protokoll), content_id="sha-bcc")

    assert protokoll == ["bcc"], "Dasselbe Gewicht darf nicht doppelt geladen werden."
    assert erst.model is zweit.model


def test_andere_inhaltskennung_entlaedt_und_laedt_neu():
    manager = GpuModelManager()
    protokoll: list[str] = []

    manager.ensure_loaded(
        ModelSlot.YOLO_TEST, "cpu", _lader("bcc", protokoll), content_id="sha-bcc")
    zustand = manager.ensure_loaded(
        ModelSlot.YOLO_TEST, "cpu", _lader("lernstufe", protokoll), content_id="sha-lern")

    assert protokoll == ["bcc", "lernstufe"]
    assert zustand.model.kennung == "lernstufe"
    assert zustand.content_id == "sha-lern"


def test_wechselfolge_liefert_nie_das_fremde_modell():
    """Der eigentliche Schadensfall: bcc -> lernstufe -> bcc."""
    manager = GpuModelManager()
    protokoll: list[str] = []

    erst = manager.ensure_loaded(
        ModelSlot.YOLO_TEST, "cpu", _lader("bcc", protokoll), content_id="sha-bcc")
    assert erst.model.kennung == "bcc"

    zwischen = manager.ensure_loaded(
        ModelSlot.YOLO_TEST, "cpu", _lader("lernstufe", protokoll), content_id="sha-lern")
    assert zwischen.model.kennung == "lernstufe"

    zurueck = manager.ensure_loaded(
        ModelSlot.YOLO_TEST, "cpu", _lader("bcc", protokoll), content_id="sha-bcc")

    # Vor der Reparatur kam hier das Lernstufen-Modell zurueck — still und ohne Fehler.
    assert zurueck.model.kennung == "bcc"
    assert protokoll == ["bcc", "lernstufe", "bcc"]


def test_ohne_inhaltskennung_bleibt_das_alte_verhalten():
    """Slots ohne Kennung (YOLO, DINO, SAM) duerfen sich nicht aendern."""
    manager = GpuModelManager()
    protokoll: list[str] = []

    manager.ensure_loaded(ModelSlot.DINO, "cpu", _lader("dino", protokoll))
    manager.ensure_loaded(ModelSlot.DINO, "cpu", _lader("dino", protokoll))

    assert protokoll == ["dino"]


def test_wechsel_bei_laufender_inferenz_wird_abgewiesen():
    """Eine fremde Lease darf nie unter der laufenden Inferenz weggeladen werden."""
    manager = GpuModelManager()
    protokoll: list[str] = []
    manager.ensure_loaded(
        ModelSlot.YOLO_TEST, "cpu", _lader("bcc", protokoll), content_id="sha-bcc")

    besitzer = manager.acquire_busy(ModelSlot.YOLO_TEST)
    try:
        with pytest.raises(ModelUnloadedError):
            manager.ensure_loaded(
                ModelSlot.YOLO_TEST, "cpu", _lader("lernstufe", protokoll),
                content_id="sha-lern")
    finally:
        manager.release_busy(ModelSlot.YOLO_TEST, besitzer)

    assert protokoll == ["bcc"], "Das fremde Gewicht darf dabei nicht geladen werden."


def test_beide_wrapper_teilen_ein_predict_lock():
    """Getrennte Locks liessen die Folge Laden->Inferenz beider Wrapper verschraenken."""
    from sidecar.models import bcc_test_wrapper, lernstufe_wrapper

    assert bcc_test_wrapper._predict_lock is lernstufe_wrapper._predict_lock
    assert isinstance(bcc_test_wrapper._predict_lock, type(threading.RLock()))
