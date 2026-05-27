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
