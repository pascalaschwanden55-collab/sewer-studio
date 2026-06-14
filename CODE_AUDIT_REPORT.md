# Code Audit Report — Sewer-Studio KI

This report summarizes the findings of a comprehensive code audit performed on the Sewer-Studio codebase. The audit covered compilation checks, test runs, syntax validation of scripts, thread safety, path safety, and domain rules compliance.

---

## Executive Summary

| Component | Status | Findings / Notes |
| :--- | :--- | :--- |
| **C# WPF Solution (`AuswertungPro.sln`)** | **PASSED** | Builds successfully with **0 warnings** and **0 errors** (.NET 10.0). |
| **C# Unit Tests** | **PASSED** | **1,305 tests** passed successfully across UI, Infrastructure, and Pipeline. |
| **Python Sidecar (`sidecar/`)** | **PASSED** | FastAPI sidecar runs correctly and dependency checks are clean. |
| **Python Unit Tests** | **PASSED** | **59 tests** passed successfully (covering DINO, SAM, YOLO, telemetry, and security). |
| **PowerShell Scripts** | **FIXED** | Discovered and fixed **critical syntax errors** in 6 `.ps1` files caused by UTF-8 encoding issues on Windows PowerShell. |

---

## Critical Bug Discovered & Resolved

### PowerShell encoding-induced syntax errors

> [!WARNING]
> Prior to the audit, running key PowerShell scripts (`HaltungenTool.ps1`, `HaltungsAuswertung.ps1`, `HaltungsAuswertungPro_v2.ps1` and their legacy versions) would fail immediately with syntax/parser errors on Windows machines.

#### Root Cause
The scripts were saved in **UTF-8 without BOM**.
- On Windows, Windows PowerShell (v5.1) defaults to the local system code page (typically **Windows-1252 / CP1252**) to parse files unless a Byte Order Mark (BOM) is present.
- The scripts contain German umlauts (`ä`, `ö`, `ü`) and graphical arrow/cancel symbols (`↑`, `↓`, `→`, `✕`) for UI rendering.
- In UTF-8, these multi-byte characters contain byte values that map to **typographic quotes** in CP-1252:
  - `\x93` (part of `↓`) maps to `“` (left curly double quote).
  - `\x84` (part of `Ä`) maps to `„` (low-double quote).
- Windows PowerShell interpreted these bytes as string quotation boundaries, closing or opening string literals prematurely and causing parse-time failures like:
  ```
  ParserError: Operator "=" fehlt nach einem Schlüssel im Hashliteral.
  ParserError: Unerwartetes Token "Nach" in Ausdruck oder Anweisung.
  ```

#### Resolution
All 6 affected files were successfully converted in-place to **UTF-8 with BOM**:
1. [HaltungenTool.ps1](file:///c:/Sewer-Studio_KI_4.4/HaltungenTool.ps1)
2. [HaltungsAuswertung.ps1](file:///c:/Sewer-Studio_KI_4.4/HaltungsAuswertung.ps1)
3. [HaltungsAuswertungPro_v2.ps1](file:///c:/Sewer-Studio_KI_4.4/HaltungsAuswertungPro_v2.ps1)
4. [_legacy/HaltungenTool.ps1](file:///c:/Sewer-Studio_KI_4.4/_legacy/HaltungenTool.ps1)
5. [_legacy/HaltungsAuswertung.ps1](file:///c:/Sewer-Studio_KI_4.4/_legacy/HaltungsAuswertung.ps1)
6. [_legacy/HaltungsAuswertungPro_v2.ps1](file:///c:/Sewer-Studio_KI_4.4/_legacy/HaltungsAuswertungPro_v2.ps1)

After conversion, all files pass the PowerShell AST parser checks and can be executed cleanly.

---

## Architecture & Code Quality Review

### 1. Thread Safety & Model Concurrency in Sidecar
In the Python FastAPI sidecar, multiple threads run requests concurrently. Since PyTorch and Ultralytics models are not natively thread-safe when performing inference, they are prone to race conditions and internal memory corruption if called simultaneously.
- **YOLO, DINO, and SAM wrappers** are correctly protected via dedicated threading locks:
  - `_yolo_predict_lock` in [yolo_wrapper.py](file:///c:/Sewer-Studio_KI_4.4/sidecar/sidecar/models/yolo_wrapper.py)
  - `_dino_predict_lock` in [dino_wrapper.py](file:///c:/Sewer-Studio_KI_4.4/sidecar/sidecar/models/dino_wrapper.py)
  - `_sam_predict_lock` in [sam_wrapper.py](file:///c:/Sewer-Studio_KI_4.4/sidecar/sidecar/models/sam_wrapper.py)
- **Double-check locking** is utilized in `GpuModelManager.ensure_loaded` to guarantee that model load routines are thread-safe and never load duplicate models.

### 2. Path Traversal & Security Sandbox
The model training export endpoint allows writing files to disk.
- **Security Check**: [training.py](file:///c:/Sewer-Studio_KI_4.4/sidecar/sidecar/routes/training.py) correctly implements `_resolve_output_dir` which validates that any output directory stays strictly within the sandbox root:
  ```python
  if resolved != root and root not in resolved.parents:
      raise HTTPException(status_code=400, detail="output_dir must stay inside the training export root")
  ```
- **Authentication**: Loopback security via `enforce_loopback_security` middleware checks the `host` header against `trusted_hosts` and validates the `X-Sidecar-Token` from a shared local file using constant-time comparison (`hmac.compare_digest`) to prevent timing attacks.

### 3. WPF Airspace Workaround
WPF does not easily allow drawing overlay elements on top of Win32-hosted controls like the VLC player (the "Airspace" problem).
- **Verification**: [PlayerWindow.xaml](file:///c:/Sewer-Studio_KI_4.4/src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml) implements a workaround by utilizing a WPF `Popup` element (`CodingOverlayPopup`) that targets the `VideoView` to render the annotation canvas on top of the native VLC player window without clipping.

### 4. PDF Parsing & Video Matching (Compliance with `AGENTS.md`)
The `HoldingFolderDistributor` domain logic was verified against the rules in [AGENTS.md](file:///c:/Sewer-Studio_KI_4.4/AGENTS.md):
- **Unmatched Video Copying**: Verified that when a video is ambiguous, candidates are copied using `File.Copy` instead of moved (conforming to the rule *"COPY, nie MOVE"*).
- **Missing / Ambiguous Markers**: Verified that `_VIDEO_MISSING.txt` and `_VIDEO_AMBIGUOUS.txt` are created dynamically in the target folders containing detailed diagnostic information.
- **Haltung Name Extraction**: Naming is normalized and verified against catalog rules. Trimming of node prefixes (e.g., `07.7695-07.7078` to `7695-7078`) is supported correctly.

---

## Recommendations

1. **Commit UTF-8 BOM Changes**: Ensure that any future edits to PowerShell files are saved as "UTF-8 with BOM" in Visual Studio / VS Code to maintain compatibility with Windows PowerShell 5.1 environments.
2. **Quality Gate Baseline**: While the pipeline code is functional and unit tests pass, the `QualityGateService` uses static threshold weights. Continue tracking manual verification feedback in `ICodingFeedbackRecorder` to tune these weights dynamically.
