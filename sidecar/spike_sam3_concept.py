"""
SPIKE (kein Produktionscode): Testet, ob SAM 3 Text-Konzept-Segmentierung Boegen
zuverlaessig als "pipe bend" erkennt - statt wie DINO als "infiltration" / wie der
YOLO-Klassifikator als "BCE Rohrende".

Hintergrund: Der YOLO-Klassifikator hat keine Bogen-Klasse und meldet Boegen als
BCE (0.68). DINO meldet den dunklen Tunnel als "infiltration" (0.46 > bend 0.41).
SAM3 SemanticPredictor soll direkt aus dem Text-Prompt "pipe bend" segmentieren.

Voraussetzung: gated Gewichte facebook/sam3 -> Datei mit 'sam3' im Namen unter
models/sam3/ (z.B. models/sam3/sam3.pt). NICHTS wird ohne vorhandene Datei geladen.

Aufruf:  python sidecar/spike_sam3_concept.py <pfad/zu/sam3.pt>
"""
import sys
import time
from pathlib import Path

# Echte Test-Frames (von mir extrahiert, Haltung 1077586-1077458)
FRAMES = [
    (r"c:/tmp/bogen_t16.png", "Bogen (erster, links verschoben)"),
    (r"c:/tmp/frame_t180.png", "Bogen (zweiter, rechts) - wird sonst als BCE verkannt"),
    (r"c:/tmp/frame_t120.png", "gerades Rohr mit Ablagerung (Kontrolle: KEIN Bogen)"),
    (r"c:/tmp/ende-8.png", "Rohrende/Schacht (Kontrolle: hier DARF kein bend kommen)"),
]
import os
# Konzeptliste via Env ueberschreibbar, um Prompt-Wording schnell zu testen.
_c = os.environ.get("SPIKE_CONCEPTS")
CONCEPTS = _c.split(" . ") if _c else ["pipe bend", "crack", "root", "deposit", "water"]


def vram_gb():
    try:
        import torch
        if torch.cuda.is_available():
            return torch.cuda.memory_allocated() / 1e9
    except Exception:
        pass
    return 0.0


def main():
    if len(sys.argv) < 2:
        print("Aufruf: python spike_sam3_concept.py <pfad/zu/sam3.pt>")
        sys.exit(2)
    weights = Path(sys.argv[1])
    if not weights.is_file():
        print(f"FEHLT: {weights} - bitte zuerst die gated SAM3-Gewichte ablegen.")
        sys.exit(2)
    if "sam3" not in weights.stem.lower():
        print(f"WARNUNG: Dateiname '{weights.name}' enthaelt kein 'sam3' - ultralytics "
              f"erkennt sie evtl. nicht als SAM3 (is_sam3 = 'sam3' in stem).")

    from ultralytics.models.sam import SAM3SemanticPredictor

    print(f"Lade SAM3 (SemanticPredictor) aus {weights} ...")
    t0 = time.perf_counter()
    predictor = SAM3SemanticPredictor(overrides=dict(
        model=str(weights), conf=0.25, save=False, verbose=False, mode="predict"))
    # Modell explizit aufsetzen (laedt build_sam3_image_model)
    predictor.setup_model(model=None, verbose=False)
    print(f"  geladen in {time.perf_counter()-t0:.1f}s, VRAM nach Load: {vram_gb():.2f} GB")

    for path, desc in FRAMES:
        if not Path(path).is_file():
            print(f"\n[skip] {path} fehlt")
            continue
        print(f"\n=== {desc}\n    {path}")
        t1 = time.perf_counter()
        try:
            predictor.set_prompts({"text": list(CONCEPTS)})
            results = predictor(source=path)
            if hasattr(results, "__iter__") and not hasattr(results, "boxes"):
                results = list(results)
                r = results[0]
            else:
                r = results
        except Exception as exc:
            import traceback
            print(f"    FEHLER: {type(exc).__name__}: {exc}")
            traceback.print_exc()
            continue
        dt = (time.perf_counter() - t1) * 1000
        names = r.names if hasattr(r, "names") else {}
        boxes = getattr(r, "boxes", None)
        n = 0 if boxes is None else len(boxes)
        print(f"    {n} Maske(n) in {dt:.0f}ms, VRAM {vram_gb():.2f} GB")
        def name_of(idx):
            if isinstance(names, dict):
                return names.get(idx, str(idx))
            if isinstance(names, (list, tuple)) and 0 <= idx < len(names):
                return names[idx]
            return str(idx)
        if n:
            for b in boxes:
                cls = int(b.cls.item()) if b.cls is not None else -1
                conf = float(b.conf.item()) if b.conf is not None else 0.0
                print(f"      {name_of(cls)}: {conf:.2f}")
        else:
            print("      (kein Konzept erkannt)")

    print("\nFAZIT-CHECK: Erscheint 'pipe bend' bei den Bogen-Frames mit hoher Konfidenz")
    print("und FEHLT es beim geraden Rohr + Rohrende? Dann zieht der SAM3-Hebel.")


if __name__ == "__main__":
    main()
