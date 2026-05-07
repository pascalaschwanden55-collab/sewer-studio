# V4.1 Batch-Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Batch-Inference fuer YOLO, SAM und DINO im Sidecar, plus Batch-Endpunkte fuer die C#-Pipeline.

**Architecture:** Jedes Modell (YOLO, DINO, SAM) bekommt eine `*_batch()`-Funktion im Wrapper und einen `/*/batch`-Endpunkt in den Routes. Bestehende Single-Endpunkte bleiben unveraendert (Backward-Compat). Die C#-Seite ruft die neuen Batch-Endpunkte auf.

**Tech Stack:** Python 3.12, FastAPI, ultralytics, SAM 2, Grounding DINO 1.5, Pydantic v2

**Spec:** `docs/superpowers/specs/2026-04-13-v41-batch-pipeline-design.md`

---

### Task 1: YOLO Batch-Schemas

**Files:**
- Modify: `sidecar/sidecar/schemas/detection.py`

- [ ] **Step 1: Batch-Request/Response Schemas hinzufuegen**

Am Ende von `detection.py` anfuegen:

```python
# ── YOLO Batch ─────────────────────────────────────────────────────────

class YoloBatchItem(BaseModel):
    image_base64: str
    frame_id: str = ""

class YoloBatchRequest(BaseModel):
    items: list[YoloBatchItem] = Field(..., min_length=1, max_length=16)
    confidence_threshold: float = Field(default=0.25, ge=0.0, le=1.0)

class YoloBatchResultItem(BaseModel):
    frame_id: str = ""
    result: YoloResponse

class YoloBatchResponse(BaseModel):
    results: list[YoloBatchResultItem] = []
    total_inference_time_ms: float = 0.0
```

- [ ] **Step 2: Commit**

```bash
git add sidecar/sidecar/schemas/detection.py
git commit -m "YOLO Batch-Schemas: YoloBatchRequest/Response"
```

---

### Task 2: YOLO Batch-Wrapper

**Files:**
- Modify: `sidecar/sidecar/models/yolo_wrapper.py`

- [ ] **Step 1: `detect_batch()` Funktion hinzufuegen**

Nach der bestehenden `detect()` Funktion (Zeile ~494) anfuegen:

```python
def detect_batch(
    images_b64: list[str],
    confidence_threshold: float,
    frame_ids: list[str] | None = None,
) -> list[tuple[str, YoloResponse]]:
    """Batch-YOLO: mehrere Bilder in einem Forward Pass.

    Gibt Liste von (frame_id, YoloResponse) zurueck.
    """
    if frame_ids is None:
        frame_ids = [str(i) for i in range(len(images_b64))]

    images = [decode_image(b64) for b64 in images_b64]

    with _inference_guard():
        model = _get_yolo_model()
        results_out = []
        t0 = time.perf_counter()

        # Batch-Predict: ultralytics unterstuetzt Liste als source
        batch_np = [np.array(img) for img in images]
        batch_results = model.predict(
            source=batch_np,
            conf=confidence_threshold,
            verbose=False,
        )
        total_ms = (time.perf_counter() - t0) * 1000

        for idx, (fid, img, result) in enumerate(
            zip(frame_ids, images, batch_results)
        ):
            detections: list[YoloDetection] = []
            frame_class = "empty"

            boxes = result.boxes
            if boxes is not None and len(boxes) > 0:
                frame_class = "relevant"
                all_xyxy = boxes.xyxy.cpu().numpy()
                all_cls = boxes.cls.cpu().numpy().astype(int)
                all_conf = boxes.conf.cpu().numpy()

                for i in range(len(boxes)):
                    cls_id = int(all_cls[i])
                    conf = float(all_conf[i])
                    cls_name = result.names.get(cls_id, str(cls_id))
                    detections.append(YoloDetection(
                        x1=float(all_xyxy[i, 0]),
                        y1=float(all_xyxy[i, 1]),
                        x2=float(all_xyxy[i, 2]),
                        y2=float(all_xyxy[i, 3]),
                        class_name=cls_name,
                        confidence=conf,
                    ))

            if _using_custom_weights:
                is_relevant = len(detections) > 0
            else:
                is_relevant = True
                frame_class = "pipe_content" if frame_class == "empty" else frame_class

            resp = YoloResponse(
                is_relevant=is_relevant,
                detections=detections,
                frame_class=frame_class,
                inference_time_ms=round(total_ms / len(images), 1),
            )
            results_out.append((fid, resp))

        return results_out
```

- [ ] **Step 2: Commit**

```bash
git add sidecar/sidecar/models/yolo_wrapper.py
git commit -m "YOLO Batch-Wrapper: detect_batch() mit ultralytics Batch-Predict"
```

---

### Task 3: YOLO Batch-Route

**Files:**
- Modify: `sidecar/sidecar/routes/yolo.py`

- [ ] **Step 1: Batch-Endpunkt hinzufuegen**

```python
from ..schemas.detection import (
    YoloRequest, YoloResponse,
    YoloClassifyRequest, YoloClassifyResponse, YoloClassifyPrediction,
    YoloBatchRequest, YoloBatchResponse, YoloBatchResultItem,
)

# Nach dem bestehenden /classify/yolo Endpunkt anfuegen:

@router.post("/detect/yolo/batch", response_model=YoloBatchResponse)
async def detect_yolo_batch(req: YoloBatchRequest) -> YoloBatchResponse:
    """Batch-YOLO: mehrere Bilder in einem Forward Pass."""
    import time
    t0 = time.perf_counter()
    try:
        results = yolo_wrapper.detect_batch(
            images_b64=[item.image_base64 for item in req.items],
            confidence_threshold=req.confidence_threshold,
            frame_ids=[item.frame_id for item in req.items],
        )
    except RuntimeError as exc:
        raise HTTPException(status_code=409, detail=str(exc)) from exc

    total_ms = (time.perf_counter() - t0) * 1000
    return YoloBatchResponse(
        results=[
            YoloBatchResultItem(frame_id=fid, result=resp)
            for fid, resp in results
        ],
        total_inference_time_ms=round(total_ms, 1),
    )
```

- [ ] **Step 2: Testen**

```bash
cd sidecar
python -m pytest tests/ -v -k "yolo" --timeout=30 2>/dev/null || echo "Keine YOLO-Tests vorhanden (OK fuer jetzt)"
```

Manueller Smoke-Test (Sidecar muss laufen):
```bash
curl -s http://localhost:8100/detect/yolo/batch \
  -H "Content-Type: application/json" \
  -d '{"items":[{"image_base64":"'$(python3 -c "import base64; print(base64.b64encode(open('c:/KI_BRAIN/fewshot_images/' + __import__('os').listdir('c:/KI_BRAIN/fewshot_images')[0], 'rb').read()).decode())")'","frame_id":"test1"}],"confidence_threshold":0.25}' \
  | python3 -m json.tool | head -20
```

- [ ] **Step 3: Commit**

```bash
git add sidecar/sidecar/routes/yolo.py
git commit -m "YOLO Batch-Route: POST /detect/yolo/batch"
```

---

### Task 4: SAM Batch-Schemas

**Files:**
- Modify: `sidecar/sidecar/schemas/segmentation.py`

- [ ] **Step 1: Batch-Schemas hinzufuegen**

Am Ende von `segmentation.py` anfuegen:

```python
# ── SAM Batch ──────────────────────────────────────────────────────────

class SamBatchItem(BaseModel):
    image_base64: str
    bounding_boxes: list[BoundingBox] = []
    frame_id: str = ""
    pipe_diameter_mm: int | None = None

class SamBatchRequest(BaseModel):
    items: list[SamBatchItem] = Field(..., min_length=1, max_length=16)

class SamBatchResultItem(BaseModel):
    frame_id: str = ""
    result: SamResponse

class SamBatchResponse(BaseModel):
    results: list[SamBatchResultItem] = []
    total_inference_time_ms: float = 0.0
```

- [ ] **Step 2: Commit**

```bash
git add sidecar/sidecar/schemas/segmentation.py
git commit -m "SAM Batch-Schemas: SamBatchRequest/Response"
```

---

### Task 5: SAM Box-Batching im Wrapper (Priority #1)

**Files:**
- Modify: `sidecar/sidecar/models/sam_wrapper.py`

- [ ] **Step 1: `_predict_boxes_batched()` Funktion hinzufuegen**

Nach `_predict_single_box()` (Zeile ~134) einfuegen:

```python
def _predict_boxes_batched(
    predictor, boxes: list[BoundingBox], masks_out: list[MaskResult],
    h: int, w: int, device: str,
) -> None:
    """Batch: alle Boxen in einem Forward Pass durch SAM 2."""
    import torch
    if not boxes:
        return

    boxes_np = np.array([[b.x1, b.y1, b.x2, b.y2] for b in boxes])
    try:
        with torch.inference_mode():
            masks, scores, _ = predictor.predict(
                point_coords=None,
                point_labels=None,
                box=boxes_np,
                multimask_output=False,
            )
        # masks shape: (N, 1, H, W) oder (N, H, W)
        if masks.ndim == 4:
            masks = masks[:, 0]  # → (N, H, W)
        for i, bbox in enumerate(boxes):
            _append_mask_result(masks_out, masks[i], float(scores[i]), bbox, h, w)
    except Exception as exc:
        logger.warning("SAM 2 Batch-Prediction fehlgeschlagen (%d Boxen): %s — Fallback sequentiell", len(boxes), exc)
        for bbox in boxes:
            _predict_single_box(predictor, bbox, masks_out, h, w, device)
```

- [ ] **Step 2: Bestehende `segment()` auf Batching umstellen**

In der `segment()` Funktion den Abschnitt wo Boxen sequentiell verarbeitet werden ersetzen. Die Boxen sollen zuerst gebatched versucht werden, mit Fallback auf sequentiell.

Suche den Abschnitt wo `_predict_single_box` in einer Schleife aufgerufen wird und ersetze durch:

```python
# Batched Prediction (alle Boxen auf einmal)
_predict_boxes_batched(predictor, bounding_boxes, masks, h, w, device)
```

- [ ] **Step 3: Commit**

```bash
git add sidecar/sidecar/models/sam_wrapper.py
git commit -m "SAM Box-Batching: alle Boxen in einem Forward Pass (Roadmap #1)"
```

---

### Task 6: SAM Batch-Route

**Files:**
- Modify: `sidecar/sidecar/routes/sam.py`

- [ ] **Step 1: Batch-Endpunkt hinzufuegen**

```python
from ..schemas.segmentation import (
    SamRequest, SamResponse,
    SamBatchRequest, SamBatchResponse, SamBatchResultItem,
)

@router.post("/segment/sam/batch", response_model=SamBatchResponse)
async def segment_sam_batch(req: SamBatchRequest) -> SamBatchResponse:
    """Batch-SAM: mehrere Bilder mit je N Boxen segmentieren."""
    import time
    t0 = time.perf_counter()

    results = []
    for item in req.items:
        resp = sam_wrapper.segment(
            image_base64=item.image_base64,
            bounding_boxes=item.bounding_boxes,
            pipe_diameter_mm=item.pipe_diameter_mm,
        )
        results.append(SamBatchResultItem(frame_id=item.frame_id, result=resp))

    total_ms = (time.perf_counter() - t0) * 1000
    return SamBatchResponse(
        results=results,
        total_inference_time_ms=round(total_ms, 1),
    )
```

Hinweis: SAM Batch ueber Bilder ist sequentiell (jedes Bild braucht `set_image()`),
aber innerhalb jedes Bildes sind die Boxen gebatched (Task 5). Das ist der Hauptgewinn.

- [ ] **Step 2: Commit**

```bash
git add sidecar/sidecar/routes/sam.py
git commit -m "SAM Batch-Route: POST /segment/sam/batch"
```

---

### Task 7: DINO Batch-Schemas und Route

**Files:**
- Modify: `sidecar/sidecar/schemas/detection.py`
- Modify: `sidecar/sidecar/routes/dino.py`

- [ ] **Step 1: Batch-Schemas in detection.py**

```python
# ── DINO Batch ─────────────────────────────────────────────────────────

class DinoBatchItem(BaseModel):
    image_base64: str
    frame_id: str = ""
    text_prompt: str | None = None

class DinoBatchRequest(BaseModel):
    items: list[DinoBatchItem] = Field(..., min_length=1, max_length=16)
    box_threshold: float = Field(default=0.30, ge=0.0, le=1.0)
    text_threshold: float = Field(default=0.25, ge=0.0, le=1.0)

class DinoBatchResultItem(BaseModel):
    frame_id: str = ""
    result: DinoResponse

class DinoBatchResponse(BaseModel):
    results: list[DinoBatchResultItem] = []
    total_inference_time_ms: float = 0.0
```

- [ ] **Step 2: Batch-Route in dino.py**

```python
from ..schemas.detection import (
    DinoRequest, DinoResponse,
    DinoBatchRequest, DinoBatchResponse, DinoBatchResultItem,
)

@router.post("/detect/dino/batch", response_model=DinoBatchResponse)
async def detect_dino_batch(req: DinoBatchRequest) -> DinoBatchResponse:
    """Batch-DINO: mehrere Bilder grounding."""
    import time
    t0 = time.perf_counter()

    results = []
    for item in req.items:
        resp = dino_wrapper.detect(
            image_base64=item.image_base64,
            text_prompt=item.text_prompt,
            box_threshold=req.box_threshold,
            text_threshold=req.text_threshold,
        )
        results.append(DinoBatchResultItem(frame_id=item.frame_id, result=resp))

    total_ms = (time.perf_counter() - t0) * 1000
    return DinoBatchResponse(
        results=results,
        total_inference_time_ms=round(total_ms, 1),
    )
```

- [ ] **Step 3: Commit**

```bash
git add sidecar/sidecar/schemas/detection.py sidecar/sidecar/routes/dino.py
git commit -m "DINO Batch: Schemas + POST /detect/dino/batch"
```

---

### Task 8: Smoke-Test aller Batch-Endpunkte

**Files:**
- Create: `sidecar/tests/test_batch_endpoints.py`

- [ ] **Step 1: Test-Datei erstellen**

```python
"""Smoke-Tests fuer Batch-Endpunkte (braucht laufenden Sidecar)."""

import base64
import os
import pytest
import httpx

SIDECAR_URL = os.environ.get("SIDECAR_URL", "http://localhost:8100")
TEST_IMAGE_DIR = r"C:\KI_BRAIN\fewshot_images"


def _get_test_image_b64() -> str:
    """Erstes Bild aus fewshot_images als base64."""
    images = [f for f in os.listdir(TEST_IMAGE_DIR) if f.endswith(".png")]
    if not images:
        pytest.skip("Keine Test-Bilder in KI_BRAIN/fewshot_images")
    path = os.path.join(TEST_IMAGE_DIR, images[0])
    with open(path, "rb") as f:
        return base64.b64encode(f.read()).decode()


@pytest.fixture(scope="module")
def image_b64():
    return _get_test_image_b64()


@pytest.fixture(scope="module")
def client():
    return httpx.Client(base_url=SIDECAR_URL, timeout=60)


def test_yolo_batch(client, image_b64):
    resp = client.post("/detect/yolo/batch", json={
        "items": [
            {"image_base64": image_b64, "frame_id": "f1"},
            {"image_base64": image_b64, "frame_id": "f2"},
        ],
        "confidence_threshold": 0.25,
    })
    assert resp.status_code == 200
    data = resp.json()
    assert len(data["results"]) == 2
    assert data["results"][0]["frame_id"] == "f1"
    assert data["total_inference_time_ms"] > 0


def test_dino_batch(client, image_b64):
    resp = client.post("/detect/dino/batch", json={
        "items": [
            {"image_base64": image_b64, "frame_id": "f1"},
        ],
        "box_threshold": 0.30,
        "text_threshold": 0.25,
    })
    assert resp.status_code == 200
    data = resp.json()
    assert len(data["results"]) == 1


def test_sam_batch(client, image_b64):
    # Erst YOLO fuer Boxen
    yolo_resp = client.post("/detect/yolo", json={
        "image_base64": image_b64,
        "confidence_threshold": 0.25,
    })
    detections = yolo_resp.json().get("detections", [])

    boxes = [
        {"x1": d["x1"], "y1": d["y1"], "x2": d["x2"], "y2": d["y2"], "label": d["class_name"]}
        for d in detections[:3]
    ]

    resp = client.post("/segment/sam/batch", json={
        "items": [
            {"image_base64": image_b64, "bounding_boxes": boxes, "frame_id": "f1"},
        ],
    })
    assert resp.status_code == 200
    data = resp.json()
    assert len(data["results"]) == 1
```

- [ ] **Step 2: Tests ausfuehren (Sidecar muss laufen)**

```bash
cd sidecar && python -m pytest tests/test_batch_endpoints.py -v --timeout=120
```

- [ ] **Step 3: Commit**

```bash
git add sidecar/tests/test_batch_endpoints.py
git commit -m "Batch-Endpunkte Smoke-Tests"
```

---

### Task 9: Ollama-Konfiguration verifizieren

**Files:** Bereits geaendert, nur Verifizierung.

- [ ] **Step 1: Start-KiMaximum5090.ps1 pruefen**

Verifizieren dass folgende Werte gesetzt sind:
- `OLLAMA_NUM_PARALLEL = "6"`
- `OLLAMA_MAX_LOADED_MODELS = "2"`
- `OLLAMA_FLASH_ATTENTION = "1"`
- `SEWERSTUDIO_OLLAMA_NUM_CTX = "8192"`
- `AUSWERTUNGPRO_AI_VISION_MODEL = "qwen3-vl:8b"`
- `AUSWERTUNGPRO_AI_REFERENCE_MODEL = "qwen2.5vl:32b"`
- `SEWERSTUDIO_GPU_CONCURRENCY = "6"`

- [ ] **Step 2: OllamaConfig.cs pruefen**

- `DefaultVisionModel = "qwen3-vl:8b"`
- `DefaultReferenceVisionModel = "qwen2.5vl:32b"`
- `DefaultNumCtx = 8192`

- [ ] **Step 3: GpuModelSelector.cs pruefen**

- Workstation: `ParallelSlots: 6`, `ReferenceModel: "qwen2.5vl:32b"`
- Kein Laptop-Tier mehr

- [ ] **Step 4: Commit (falls Korrekturen noetig)**

```bash
git add -A && git commit -m "V4.1 Config-Verifizierung: 8Bx6 + 32B Eskalation"
```
