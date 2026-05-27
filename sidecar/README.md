# Sewer-Studio Vision Sidecar

This is the Python-based vision sidecar for Sewer-Studio. It provides a multi-model pipeline for AI-assisted sewer inspection:
- **YOLO (v11)**: Pre-screening and object detection.
- **Grounding DINO**: Open-vocabulary detection.
- **SAM (Segment Anything)**: Pixel-precise segmentation.

## Setup

The sidecar requires Python 3.10 or higher.

1.  **Run the setup script**:
    ```powershell
    .\setup.ps1
    ```
    This will create a `.venv`, install all dependencies from `requirements-lock.txt`, and ensure the environment is deterministic.
    The setup uses the PyTorch CUDA 12.1 package index for the pinned `torch` and `torchvision` wheels.

## Usage

Start the sidecar with:
```powershell
.\start_sidecar.ps1
```
The sidecar will start on `http://127.0.0.1:8100`. Sewer-Studio automatically detects and connects to it if it is running.

## TensorRT Engine

To rebuild the local YOLO TensorRT engine on the target GPU machine:

```powershell
.\build_engine.ps1
```

The script backs up the old `.engine` file first and writes JSON metadata with hashes and version information. It then exports `models\yolo26m\yolo26m.pt` to ONNX and builds `models\yolo26m\yolo26m.engine` with `trtexec --fp16`.

## Telemetry

YOLO detection requests append one JSON line to:

```text
%LocalAppData%\SewerStudio\Telemetry\sidecar.jsonl
```

The path can be changed with `SEWER_SIDECAR_TELEMETRY_DIR`. Set `SEWER_SIDECAR_TELEMETRY_ENABLED=false` to disable it.

## Development

- Top-level dependencies are listed in `requirements.txt`.
- Pinned dependencies with hashes are in `requirements-lock.txt`.
- To update the lock file after changing `requirements.txt`, run:
  ```bash
  uv pip compile requirements.txt -o requirements-lock.txt --generate-hashes
  ```
- Recreate the local Python environment with:
  ```powershell
  .\setup.ps1
  ```
