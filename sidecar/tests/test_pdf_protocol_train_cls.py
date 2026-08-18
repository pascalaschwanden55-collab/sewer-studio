"""VRAM-Schutz des alten PDF-Klassifikator-Trainings."""

import importlib.util
import urllib.error
from pathlib import Path


SKRIPT = (
    Path(__file__).resolve().parents[2]
    / "tools"
    / "PdfProtocolIngest"
    / "train_cls.py"
)
SPEC = importlib.util.spec_from_file_location("pdf_protocol_train_cls", SKRIPT)
assert SPEC is not None and SPEC.loader is not None
train_cls = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(train_cls)


def test_sidecar_up_behandelt_http_fehler_als_erreichbaren_dienst(monkeypatch):
    def antwortet_mit_401(*args, **kwargs):
        raise urllib.error.HTTPError(
            train_cls.SIDECAR, 401, "Unauthorized", {}, None)

    monkeypatch.setattr(train_cls.urllib.request, "urlopen", antwortet_mit_401)

    assert train_cls.sidecar_up()
