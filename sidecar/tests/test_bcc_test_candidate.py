import hashlib
import json
import sys
from pathlib import Path
from types import SimpleNamespace

import pytest
from fastapi.testclient import TestClient


def _write_candidate(
    root,
    candidate_id: str,
    *,
    map50: float,
    epochs: int = 40,
    status: str = "not_deployed",
    pilot: str = "BCC_bogen",
    expected_sha: str | None = None,
):
    candidate = root / candidate_id
    candidate.mkdir()
    weights = candidate / "best.pt"
    weights.write_bytes(f"weights-{candidate_id}".encode())
    actual_sha = hashlib.sha256(weights.read_bytes()).hexdigest()
    manifest = {
        "schema_version": "1.0",
        "candidate_status": status,
        "pilot": pilot,
        "created_utc": "2026-07-24T12:00:00+00:00",
        "dataset": {"images": 48},
        "training": {
            "epochs_completed": epochs,
            "results": {"metrics/mAP50(B)": map50},
        },
        "weights": {"candidate_sha256": expected_sha or actual_sha},
    }
    (candidate / "candidate_manifest.json").write_text(
        json.dumps(manifest),
        encoding="utf-8",
    )
    return actual_sha


def test_select_candidate_waehlt_bestes_gueltiges_not_deployed_modell(
    tmp_path,
    monkeypatch,
):
    from sidecar.config import settings
    from sidecar.models import bcc_test_wrapper

    _write_candidate(tmp_path, "bcc_weak", map50=0.25, epochs=5)
    best_sha = _write_candidate(tmp_path, "bcc_full40", map50=0.76, epochs=40)
    _write_candidate(tmp_path, "bcc_active", map50=0.99, status="deployed")
    _write_candidate(tmp_path, "bcc_bad_hash", map50=0.98, expected_sha="0" * 64)
    _write_candidate(tmp_path, "other_pilot", map50=0.97, pilot="BAB_riss")
    monkeypatch.setattr(
        settings,
        "training_model_candidates_root",
        str(tmp_path),
        raising=False,
    )

    selected = bcc_test_wrapper.select_candidate()

    assert selected.candidate_id == "bcc_full40"
    assert selected.weights_sha256 == best_sha
    assert selected.epochs_completed == 40


def test_select_candidate_waehlt_angeforderte_id_und_sha_statt_hoeherer_map(
    tmp_path,
    monkeypatch,
):
    from sidecar.config import settings
    from sidecar.models import bcc_test_wrapper

    selected_sha = _write_candidate(
        tmp_path,
        "bcc_bogen_aaaaaaaaaaaa_neu",
        map50=0.70,
    )
    _write_candidate(
        tmp_path,
        "bcc_bogen_bbbbbbbbbbbb_alt",
        map50=0.95,
    )
    monkeypatch.setattr(
        settings,
        "training_model_candidates_root",
        str(tmp_path),
        raising=False,
    )

    selected = bcc_test_wrapper.select_candidate(
        "bcc_bogen_aaaaaaaaaaaa_neu",
        selected_sha,
    )

    assert selected.candidate_id == "bcc_bogen_aaaaaaaaaaaa_neu"
    assert selected.weights_sha256 == selected_sha
    with pytest.raises(bcc_test_wrapper.BccTestCandidateError):
        bcc_test_wrapper.select_candidate(
            "bcc_bogen_aaaaaaaaaaaa_neu",
            "f" * 64,
        )


def test_list_candidates_liefert_nur_ids_die_spaeter_waehlbar_sind(
    tmp_path,
    monkeypatch,
):
    from sidecar.config import settings
    from sidecar.models import bcc_test_wrapper

    valid_sha = _write_candidate(tmp_path, "bcc_bogen_gueltig", map50=0.70)
    _write_candidate(tmp_path, "bcc bogen leerzeichen", map50=0.99)
    _write_candidate(tmp_path, "bcc.bogen.punkt", map50=0.98)
    _write_candidate(tmp_path, "bcc_bogen_Ã¤", map50=0.97)
    monkeypatch.setattr(
        settings,
        "training_model_candidates_root",
        str(tmp_path),
        raising=False,
    )

    candidates = bcc_test_wrapper.list_candidates()

    assert [(item.candidate_id, item.weights_sha256) for item in candidates] == [
        ("bcc_bogen_gueltig", valid_sha)
    ]


def test_load_candidate_verlangt_die_komplette_freigegebene_15er_klassenkarte(
    tmp_path,
    monkeypatch,
):
    from sidecar.models import bcc_test_wrapper

    names = {
        0: "FALSCH",
        1: "BAB_riss",
        2: "BAC_bruch",
        3: "BAA_verformung",
        4: "BAF_oberflaeche",
        5: "BAH_schadanschluss",
        6: "BAI_dichtung",
        7: "BAJ_verbindung",
        8: "BBA_wurzeln",
        9: "BBB_anhaftung",
        10: "BBC_ablagerung",
        11: "BBD_boden",
        12: "BBF_infiltration",
        13: "SONST_schaden",
        14: "BCC_bogen",
    }
    fake_model = SimpleNamespace(names=names, to=lambda _device: None)
    monkeypatch.setitem(
        sys.modules,
        "ultralytics",
        SimpleNamespace(YOLO=lambda _path: fake_model),
    )
    weights = tmp_path / "best.pt"
    weights.write_bytes(b"weights")
    candidate = bcc_test_wrapper.BccCandidate(
        candidate_id="bcc_bogen_test",
        weights_path=weights,
        weights_sha256=hashlib.sha256(weights.read_bytes()).hexdigest(),
        map50=0.7,
        epochs_completed=40,
        created_utc="2026-07-28T14:43:21Z",
    )

    with pytest.raises(
        bcc_test_wrapper.BccTestCandidateError,
        match="15er-Klassenkarte",
    ):
        bcc_test_wrapper._load_candidate(candidate, "cpu")


def test_load_candidate_prueft_den_hash_unmittelbar_vor_dem_laden(
    tmp_path,
    monkeypatch,
):
    from sidecar.models import bcc_test_wrapper

    weights = tmp_path / "best.pt"
    weights.write_bytes(b"urspruenglich")
    expected_sha = hashlib.sha256(weights.read_bytes()).hexdigest()
    weights.write_bytes(b"nach-katalog-geaendert")
    yolo_calls = []
    monkeypatch.setitem(
        sys.modules,
        "ultralytics",
        SimpleNamespace(YOLO=lambda path: yolo_calls.append(path)),
    )
    candidate = bcc_test_wrapper.BccCandidate(
        candidate_id="bcc_bogen_test",
        weights_path=weights,
        weights_sha256=expected_sha,
        map50=0.7,
        epochs_completed=40,
        created_utc="2026-07-28T14:43:21Z",
    )

    with pytest.raises(bcc_test_wrapper.BccTestCandidateError, match="Hash"):
        bcc_test_wrapper._load_candidate(candidate, "cpu")

    assert yolo_calls == []


def test_load_candidate_verwendet_verifizierte_momentaufnahme_statt_originalpfad(
    tmp_path,
    monkeypatch,
):
    from sidecar.models import bcc_test_wrapper

    weights = tmp_path / "best.pt"
    weights.write_bytes(b"urspruenglich")
    expected_sha = hashlib.sha256(weights.read_bytes()).hexdigest()
    fake_model = SimpleNamespace(
        names=dict(bcc_test_wrapper._EXPECTED_CLASS_NAMES),
        to=lambda _device: None,
    )
    loaded_paths = []

    def mutate_original_while_loading(snapshot_path):
        loaded_paths.append(Path(snapshot_path))
        assert Path(snapshot_path) != weights
        assert Path(snapshot_path).read_bytes() == b"urspruenglich"
        weights.write_bytes(b"waehrend-des-ladens-geaendert")
        return fake_model

    monkeypatch.setitem(
        sys.modules,
        "ultralytics",
        SimpleNamespace(YOLO=mutate_original_while_loading),
    )
    candidate = bcc_test_wrapper.BccCandidate(
        candidate_id="bcc_bogen_test",
        weights_path=weights,
        weights_sha256=expected_sha,
        map50=0.7,
        epochs_completed=40,
        created_utc="2026-07-28T14:43:21Z",
    )

    loaded_model, _ = bcc_test_wrapper._load_candidate(candidate, "cpu")

    assert loaded_model is fake_model
    assert weights.read_bytes() == b"waehrend-des-ladens-geaendert"
    assert len(loaded_paths) == 1
    assert not loaded_paths[0].exists()


def test_load_candidate_erkennt_aenderung_der_privaten_momentaufnahme(
    tmp_path,
    monkeypatch,
):
    from sidecar.models import bcc_test_wrapper

    weights = tmp_path / "best.pt"
    weights.write_bytes(b"urspruenglich")
    expected_sha = hashlib.sha256(weights.read_bytes()).hexdigest()
    fake_model = SimpleNamespace(
        names=dict(bcc_test_wrapper._EXPECTED_CLASS_NAMES),
        to=lambda _device: None,
    )

    def mutate_snapshot(snapshot_path):
        Path(snapshot_path).write_bytes(b"private-kopie-geaendert")
        return fake_model

    monkeypatch.setitem(
        sys.modules,
        "ultralytics",
        SimpleNamespace(YOLO=mutate_snapshot),
    )
    candidate = bcc_test_wrapper.BccCandidate(
        candidate_id="bcc_bogen_test",
        weights_path=weights,
        weights_sha256=expected_sha,
        map50=0.7,
        epochs_completed=40,
        created_utc="2026-07-28T14:43:21Z",
    )

    with pytest.raises(
        bcc_test_wrapper.BccTestCandidateError,
        match="private BCC-Modellkopie",
    ):
        bcc_test_wrapper._load_candidate(candidate, "cpu")


def test_detect_markiert_unbrauchbares_foto_als_nicht_geprueft(monkeypatch):
    from sidecar.models import bcc_test_wrapper

    candidate = bcc_test_wrapper.BccCandidate(
        candidate_id="bcc_bogen_test",
        weights_path=Path("best.pt"),
        weights_sha256="a" * 64,
        map50=0.7,
        epochs_completed=40,
        created_utc="2026-07-28T14:43:21Z",
    )
    monkeypatch.setattr(bcc_test_wrapper.yolo_wrapper, "decode_image", lambda _value: object())
    monkeypatch.setattr(
        bcc_test_wrapper.yolo_wrapper,
        "_is_frame_usable",
        lambda _image: (False, "zu dunkel"),
    )
    monkeypatch.setattr(
        bcc_test_wrapper,
        "select_candidate",
        lambda *_args: candidate,
    )
    monkeypatch.setattr(bcc_test_wrapper, "_resolve_device", lambda: "cpu")

    response = bcc_test_wrapper.detect("abc", 0.25)

    assert response.available is True
    assert response.frame_usable is False
    assert response.quality_reason == "zu dunkel"
    assert response.detections == []


def test_bcc_ausgabe_filtert_ungepruefte_klassen_0_bis_13():
    from sidecar.models import bcc_test_wrapper

    class FakeTensor:
        def __init__(self, value):
            self.value = value

        def __getitem__(self, _index):
            return self

        def cpu(self):
            return self

        def numpy(self):
            return self.value

        def item(self):
            return self.value

    boxes = [
        SimpleNamespace(
            xyxy=FakeTensor([1.0, 2.0, 3.0, 4.0]),
            cls=FakeTensor(0),
            conf=FakeTensor(0.99),
        ),
        SimpleNamespace(
            xyxy=FakeTensor([10.0, 20.0, 30.0, 40.0]),
            cls=FakeTensor(14),
            conf=FakeTensor(0.88),
        ),
    ]
    results = [SimpleNamespace(boxes=boxes)]

    detections = bcc_test_wrapper._extract_bcc_detections(results)

    assert len(detections) == 1
    detection = detections[0]
    assert detection.class_name == "BCC_bogen"
    assert detection.x1 == 10.0


@pytest.mark.parametrize(
    "candidate_id",
    [
        "../bcc_bogen_aaaaaaaaaaaa",
        r"..\bcc_bogen_aaaaaaaaaaaa",
        r"C:\bcc_bogen_aaaaaaaaaaaa",
        "bcc_bogen/aaaaaaaaaaaa",
        "",
    ],
)
def test_select_candidate_weist_unsichere_id_ohne_auto_fallback_ab(
    tmp_path,
    monkeypatch,
    candidate_id,
):
    from sidecar.config import settings
    from sidecar.models import bcc_test_wrapper

    fallback_sha = _write_candidate(
        tmp_path,
        "bcc_bogen_bbbbbbbbbbbb_alt",
        map50=0.95,
    )
    monkeypatch.setattr(
        settings,
        "training_model_candidates_root",
        str(tmp_path),
        raising=False,
    )

    with pytest.raises(bcc_test_wrapper.BccTestCandidateError):
        bcc_test_wrapper.select_candidate(candidate_id, fallback_sha)


def test_select_candidate_weist_unbekannte_id_ohne_auto_fallback_ab(
    tmp_path,
    monkeypatch,
):
    from sidecar.config import settings
    from sidecar.models import bcc_test_wrapper

    fallback_sha = _write_candidate(
        tmp_path,
        "bcc_bogen_bbbbbbbbbbbb_alt",
        map50=0.95,
    )
    monkeypatch.setattr(
        settings,
        "training_model_candidates_root",
        str(tmp_path),
        raising=False,
    )

    with pytest.raises(bcc_test_wrapper.BccTestCandidateError):
        bcc_test_wrapper.select_candidate(
            "bcc_bogen_cccccccccccc_unbekannt",
            fallback_sha,
        )


def test_select_candidate_ohne_gueltiges_modell_sperrt_fail_closed(
    tmp_path,
    monkeypatch,
):
    import pytest

    from sidecar.config import settings
    from sidecar.models import bcc_test_wrapper

    _write_candidate(tmp_path, "bcc_bad_hash", map50=0.8, expected_sha="f" * 64)
    monkeypatch.setattr(
        settings,
        "training_model_candidates_root",
        str(tmp_path),
        raising=False,
    )

    with pytest.raises(
        bcc_test_wrapper.BccTestCandidateError,
        match="Kein gültiges",
    ):
        bcc_test_wrapper.select_candidate()


def test_bcc_test_route_ist_getrennt_vom_produktiven_yolo(monkeypatch):
    from sidecar.main import app
    from sidecar.models import bcc_test_wrapper
    from sidecar.schemas.detection import BccTestYoloResponse, YoloDetection

    called = {}

    def fake_detect(
        image_base64: str,
        confidence_threshold: float,
        candidate_id: str | None = None,
        candidate_sha256: str | None = None,
        meter_format: str | None = None,
    ):
        called["image"] = image_base64
        called["threshold"] = confidence_threshold
        called["candidate_id"] = candidate_id
        called["candidate_sha256"] = candidate_sha256
        called["meter_format"] = meter_format
        return BccTestYoloResponse(
            available=True,
            is_relevant=True,
            detections=[
                YoloDetection(
                    x1=10,
                    y1=20,
                    x2=100,
                    y2=200,
                    class_name="BCC_bogen",
                    confidence=0.88,
                )
            ],
            frame_class="relevant",
            inference_time_ms=12.5,
            candidate_id="bcc_full40",
            candidate_sha256="a" * 64,
            model_name="bcc_full40",
            device="cuda:0",
            meter_value=14.1,
        )

    monkeypatch.setattr(bcc_test_wrapper, "detect", fake_detect)

    response = TestClient(app).post(
        "/detect/yolo/bcc-test",
        json={
            "image_base64": "abc",
            "confidence_threshold": 0.3,
            "candidate_id": "bcc_bogen_aaaaaaaaaaaa_neu",
            "candidate_sha256": "a" * 64,
            "meter_format": "vierziffern",
        },
    )

    assert response.status_code == 200
    payload = response.json()
    assert payload["candidate_id"] == "bcc_full40"
    assert payload["detections"][0]["class_name"] == "BCC_bogen"
    assert payload["meter_value"] == 14.1
    assert called == {
        "image": "abc",
        "threshold": 0.3,
        "candidate_id": "bcc_bogen_aaaaaaaaaaaa_neu",
        "candidate_sha256": "a" * 64,
        "meter_format": "vierziffern",
    }


def test_bcc_test_route_weist_modellpfad_ausdruecklich_ab(monkeypatch):
    from sidecar.main import app
    from sidecar.models import bcc_test_wrapper

    monkeypatch.setattr(
        bcc_test_wrapper,
        "detect",
        lambda **kwargs: pytest.fail("Bei ungueltigem Request darf keine Inferenz laufen."),
    )

    response = TestClient(app).post(
        "/detect/yolo/bcc-test",
        json={
            "image_base64": "abc",
            "confidence_threshold": 0.25,
            "model_path": r"C:\fremd\best.pt",
        },
    )

    assert response.status_code == 422


@pytest.mark.parametrize(
    "pin",
    [
        {"candidate_id": "bcc_bogen_aaaaaaaaaaaa_neu"},
        {"candidate_sha256": "a" * 64},
    ],
)
def test_bcc_test_route_verlangt_id_und_sha_gemeinsam(monkeypatch, pin):
    from sidecar.main import app
    from sidecar.models import bcc_test_wrapper

    monkeypatch.setattr(
        bcc_test_wrapper,
        "detect",
        lambda **kwargs: pytest.fail("Bei unvollstaendigem Pin darf keine Inferenz laufen."),
    )

    response = TestClient(app).post(
        "/detect/yolo/bcc-test",
        json={
            "image_base64": "abc",
            "confidence_threshold": 0.25,
            **pin,
        },
    )

    assert response.status_code == 422


def test_bcc_test_candidates_route_liefert_nur_validierte_metadaten(monkeypatch):
    from sidecar.main import app
    from sidecar.models import bcc_test_wrapper

    monkeypatch.setattr(
        bcc_test_wrapper,
        "list_candidates",
        lambda: [
            bcc_test_wrapper.BccCandidate(
                candidate_id="bcc_bogen_aaaaaaaaaaaa_neu",
                weights_path=Path("unused"),
                weights_sha256="a" * 64,
                map50=0.74,
                epochs_completed=40,
                created_utc="2026-07-28T14:43:21Z",
            )
        ],
    )

    response = TestClient(app).get("/detect/yolo/bcc-test/candidates")

    assert response.status_code == 200
    payload = response.json()
    assert payload["available"] is True
    assert payload["candidates"] == [
        {
            "candidate_id": "bcc_bogen_aaaaaaaaaaaa_neu",
            "candidate_sha256": "a" * 64,
            "map50": 0.74,
            "epochs_completed": 40,
            "created_utc": "2026-07-28T14:43:21Z",
        }
    ]
    assert "path" not in json.dumps(payload).lower()


def test_bcc_test_route_meldet_fehlenden_kandidaten_ohne_500(monkeypatch):
    from sidecar.main import app
    from sidecar.models import bcc_test_wrapper

    def unavailable(*_args, **_kwargs):
        raise bcc_test_wrapper.BccTestCandidateError(
            "Kein gültiges, nicht aktives BCC-Testmodell gefunden."
        )

    monkeypatch.setattr(bcc_test_wrapper, "detect", unavailable)

    response = TestClient(app).post(
        "/detect/yolo/bcc-test",
        json={"image_base64": "abc", "confidence_threshold": 0.25},
    )

    assert response.status_code == 200
    assert response.json()["available"] is False
    assert "nicht aktives" in response.json()["error"]
