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
    The setup uses the PyTorch CUDA 12.8 (`cu128`) nightly package index. This is
    required for the RTX 5090 (`sm_120`); do not change it back to `cu121`.

## Usage

Start the sidecar with:
```powershell
.\start_sidecar.ps1
```
The sidecar will start on `http://127.0.0.1:8100`. Sewer-Studio automatically detects and connects to it if it is running.

## Security boundary

Every request requires the secret `X-Sidecar-Token`. This token is the actual access control.
If no server token is initialized, the sidecar rejects every request with HTTP 503; an empty
token never disables authentication.
The additional trusted-host check only protects local browser sessions against DNS rebinding;
a `Host` header is client-controlled and must never be treated as authentication. Even when
`SEWER_SIDECAR_TRUSTED_HOSTS=*` is configured for diagnostics, requests without the correct
token remain blocked.

The supplied runtime also binds fail-closed to the local computer. `SEWER_SIDECAR_HOST`
accepts only `localhost`, IPv4 loopback addresses (`127.x.x.x`), or IPv6 loopback (`::1`).
Values such as `0.0.0.0`, LAN addresses, and public host names stop startup with an error.

## TensorRT Engine

To rebuild the local YOLO TensorRT engine on the target GPU machine:

```powershell
.\build_engine.ps1
```

The script backs up the old `.engine` file first and writes JSON metadata with hashes and version information. It then exports `models\yolo26m\yolo26m.pt` to ONNX and builds `models\yolo26m\yolo26m.engine` with `trtexec --fp16`. If `trtexec` is not installed, it falls back to the TensorRT Python builder.

When CUDA is available and `models\yolo26m\yolo26m.engine` exists,
`start_sidecar.ps1` selects `yolo26m.engine` automatically. Use a dry run to
verify the selected model without starting the server:

```powershell
.\start_sidecar.ps1 -DryRun
```

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
