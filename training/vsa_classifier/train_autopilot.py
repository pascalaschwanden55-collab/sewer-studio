"""
Trainings-Autopilot fuer den VSA-Klassifikator.

Schleife: Kontaminations-Check -> trainieren -> gegen Baseline messen
(57er-clean = Haupt-Gate + 63er-hidden = Robustheit) -> Verdikt
(UEBERNAHME-KANDIDAT / GLEICHWERTIG / NICHT UEBERNEHMEN) -> Report (MD) -> Kandidat markieren.

LEITPLANKEN (hart):
- Keine Label-Erfindung: Codes kommen aus Dateinamen, unklare werden ausgeschlossen
  (der C#-Builder baut den Datensatz so; hier wird nur darauf trainiert).
- KEINE Produktiv-Schaltung: active.json wird NIE angefasst — nur Kandidaten-Markierung.
- Kontaminations-Check (Eval-Bilder duerfen nicht im Datensatz sein) vor dem Training.
- Der Mensch gibt die Produktions-Freigabe.

Beispiel:
  python training/vsa_classifier/train_autopilot.py --name vsa_cls_v6b_leer38 --leer-target 0.38
"""
import argparse
import glob
import hashlib
import json
import os
import subprocess
import sys
import time

from repo_paths import BENCHMARK_REPORT_ROOT, CLEAN_EVAL_ROOT, HIDDEN_EVAL_ROOT

HERE = os.path.dirname(os.path.abspath(__file__))
PY = sys.executable

DEF_EVAL_MANIFEST = r"C:\KI_BRAIN\eval_set\_manifest.json"
DEF_DATA = r"C:\KI_BRAIN\yolo_vsa_cls_dataset_bal"
DEF_EVAL = CLEAN_EVAL_ROOT
DEF_HIDDEN = HIDDEN_EVAL_ROOT
DEF_BASELINE = r"C:\KI_BRAIN\yolo_cls_runs\vsa_cls_v5_nocrop\weights\best.pt"
DEF_PROJECT = r"C:\KI_BRAIN\yolo_cls_runs"
CAND_DIR = r"C:\KI_BRAIN\model_candidates"
REPORT_DIR = BENCHMARK_REPORT_ROOT
KEY_CLASSES = ["BAI", "BAB", "BBA", "BDD", "BAJ"]


def run(cmd):
    print(">>", " ".join(str(c) for c in cmd), flush=True)
    if subprocess.run(cmd).returncode != 0:
        raise SystemExit(f"Schritt fehlgeschlagen: {cmd}")


def contamination_name_check(eval_root, data_root):
    """Leitplanke: kein Eval-Bild (per Dateiname) im Trainingsdatensatz."""
    eval_names = {os.path.basename(p) for p in glob.glob(os.path.join(eval_root, "images", "*.png"))}
    return sum(1 for p in glob.glob(os.path.join(data_root, "**", "*.png"), recursive=True)
               if os.path.basename(p) in eval_names)


def _sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().lower()


def contamination_hash_check(manifest_path, data_root):
    """Leitplanke 2: kein Eval-Bild per SHA-256 im Datensatz.

    Faengt UMBENANNTE Eval-Kopien, die der Namens-Check nicht sieht (der
    Dataset-Builder fand 35 solcher Kopien im alten yolo_seg_dataset).
    Quelle der Wahrheit: das eingefrorene Voll-Manifest (120er, deckt die
    57er-clean- und 63er-hidden-Teilmengen mit ab).
    """
    if not os.path.isfile(manifest_path):
        raise SystemExit(f"ABBRUCH: Eval-Manifest fehlt ({manifest_path}) — Hash-Check unmoeglich.")
    with open(manifest_path, encoding="utf-8") as f:
        m = json.load(f)
    eval_hashes = set()
    for rel, info in (m.get("hashes") or {}).items():
        if rel.startswith("images/"):
            sha = info.get("sha256") if isinstance(info, dict) else str(info)
            if sha:
                eval_hashes.add(str(sha).lower())
    if not eval_hashes:
        raise SystemExit("ABBRUCH: Eval-Manifest ohne Bild-Hashes — Hash-Check unmoeglich.")

    hits = 0
    for p in glob.glob(os.path.join(data_root, "**", "*.png"), recursive=True):
        if _sha256_file(p) in eval_hashes:
            hits += 1
            print(f"  KONTAMINIERT (Hash, ggf. umbenannt): {p}", flush=True)
    return hits


def verify_eval_subset_manifest(eval_root):
    """Stellt sicher, dass das verwendete Eval-Subset exakt zu seinem _manifest.json passt."""
    manifest_path = os.path.join(eval_root, "_manifest.json")
    if not os.path.isfile(manifest_path):
        raise SystemExit(f"ABBRUCH: Eval-Subset-Manifest fehlt: {manifest_path}")

    with open(manifest_path, encoding="utf-8") as f:
        manifest = json.load(f)

    hashes = manifest.get("hashes") or {}
    if not isinstance(hashes, dict) or not hashes:
        raise SystemExit(f"ABBRUCH: Eval-Subset-Manifest ohne Hashes: {manifest_path}")

    errors = []
    for rel, info in hashes.items():
        expected = info.get("sha256") if isinstance(info, dict) else str(info)
        if not expected:
            errors.append(f"{rel}: kein sha256 im Manifest")
            continue
        path = os.path.join(eval_root, rel.replace("/", os.sep))
        if not os.path.isfile(path):
            errors.append(f"{rel}: Datei fehlt")
            continue
        actual = _sha256_file(path)
        if actual != str(expected).lower():
            errors.append(f"{rel}: SHA mismatch {actual} != {expected}")

    if errors:
        print("Eval-Subset-Manifest verletzt:", flush=True)
        for e in errors[:20]:
            print(f"  {e}", flush=True)
        raise SystemExit(f"ABBRUCH: Eval-Subset veraendert ({len(errors)} Fehler): {eval_root}")

    print(f"Eval-Subset-Manifest OK: {eval_root} ({len(hashes)} Hashes)", flush=True)


def eval_model(weights, eval_root, imgsz, tag):
    out = os.path.join(REPORT_DIR, f"_autopilot_{tag}.json")
    run([PY, os.path.join(HERE, "eval_cls.py"), "--weights", weights,
         "--eval-root", eval_root, "--imgsz", str(imgsz), "--no-crop", "--json-out", out])
    with open(out, encoding="utf-8") as f:
        return json.load(f)


def judge(cand, base):
    """Counts-basiertes Verdikt: schlechter, wenn Gesamt/Befund/LEER ODER eine Schluesselklasse sinkt."""
    lines, worse, better_any = [], False, False
    for k, name in [("exact_correct", "Gesamt"), ("findings_correct", "Befund"), ("leer_correct", "LEER")]:
        d = cand[k] - base[k]
        lines.append(f"{name}: {cand[k]} vs {base[k]} ({d:+d})")
        worse = worse or d < 0
        better_any = better_any or d > 0
    cls_lines = []
    for c in KEY_CLASSES:
        cc = cand["per_class"].get(c, [0, 0])[0]
        bc = base["per_class"].get(c, [0, 0])[0]
        cls_lines.append(f"{c}: {cc} vs {bc} ({cc - bc:+d})")
        worse = worse or cc < bc
    label = "NICHT UEBERNEHMEN" if worse else ("UEBERNAHME-KANDIDAT" if better_any else "GLEICHWERTIG")
    return label, lines, cls_lines


def fmt(m):
    return (f"Gesamt {m['exact_correct']}/{m['frames']}={m['exact_acc']:.1%} | "
            f"Befund {m['findings_correct']}/{m['findings_total']}={m['findings_acc']:.1%} | "
            f"LEER {m['leer_correct']}/{m['leer_total']}={m['leer_acc']:.1%}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--name", required=True)
    ap.add_argument("--data", default=DEF_DATA)
    ap.add_argument("--baseline", default=DEF_BASELINE)
    ap.add_argument("--eval", default=DEF_EVAL)
    ap.add_argument("--hidden", default=DEF_HIDDEN)
    ap.add_argument("--project", default=DEF_PROJECT)
    ap.add_argument("--imgsz", type=int, default=1024)
    ap.add_argument("--batch", type=int, default=16)
    ap.add_argument("--epochs", type=int, default=60)
    ap.add_argument("--balance", action="store_true", default=True)
    ap.add_argument("--no-balance", dest="balance", action="store_false")
    ap.add_argument("--leer-target", type=float, default=0.38)
    ap.add_argument("--eval-manifest", default=DEF_EVAL_MANIFEST,
                    help="Eingefrorenes Voll-Manifest fuer den SHA-256-Kontaminations-Check")
    args = ap.parse_args()

    os.makedirs(REPORT_DIR, exist_ok=True)
    os.makedirs(CAND_DIR, exist_ok=True)
    stamp = time.strftime("%Y%m%d_%H%M%S")
    print(f"=== AUTOPILOT {args.name} (LEER-Ziel {args.leer_target:.0%}) ===", flush=True)

    # 1) Leitplanke: Kontamination (Name UND SHA-256 — Hash faengt umbenannte Kopien)
    c = contamination_name_check(args.eval, args.data)
    print(f"Kontamination (Eval-Name im Datensatz): {c}  (MUSS 0)", flush=True)
    if c != 0:
        raise SystemExit("ABBRUCH: Kontamination im Datensatz!")
    ch = contamination_hash_check(args.eval_manifest, args.data)
    print(f"Kontamination (Eval-SHA256 im Datensatz): {ch}  (MUSS 0)", flush=True)
    if ch != 0:
        raise SystemExit("ABBRUCH: Umbenannte Eval-Kopien im Datensatz (SHA-256-Treffer)!")
    verify_eval_subset_manifest(args.eval)
    verify_eval_subset_manifest(args.hidden)

    # 2) Trainieren (No-Crop/Letterbox + optional Balance)
    tcmd = [PY, os.path.join(HERE, "train_cls.py"), "--data", args.data, "--imgsz", str(args.imgsz),
            "--batch", str(args.batch), "--name", args.name, "--epochs", str(args.epochs), "--no-crop"]
    if args.balance:
        tcmd += ["--balance", "--leer-target", str(args.leer_target)]
    run(tcmd)
    weights = os.path.join(args.project, args.name, "weights", "best.pt")
    if not os.path.isfile(weights):
        raise SystemExit(f"ABBRUCH: kein best.pt unter {weights}")

    # 3) Messen: Kandidat + Baseline, je 57er-clean + 63er-hidden
    cand_clean = eval_model(weights, args.eval, args.imgsz, f"{args.name}_clean")
    cand_hidden = eval_model(weights, args.hidden, args.imgsz, f"{args.name}_hidden")
    base_clean = eval_model(args.baseline, args.eval, args.imgsz, "baseline_clean")
    base_hidden = eval_model(args.baseline, args.hidden, args.imgsz, "baseline_hidden")

    # 4) Verdikt — ENTSCHEIDUNG NUR auf 57er-clean. 63er-hidden ist reiner Kontrollblick und
    #    fliesst NICHT in die Entscheidung ein (sonst wird das Hidden-Set ueber viele Laeufe zum
    #    heimlichen Auswahlset und verliert seinen Wert).
    label, lines, cls_lines = judge(cand_clean, base_clean)

    # 5) Report
    md = os.path.join(REPORT_DIR, f"autopilot_{args.name}_{stamp}.md")
    with open(md, "w", encoding="utf-8") as f:
        f.write(f"# Autopilot-Report: {args.name}\n\n")
        f.write(f"Stand {stamp} | Datensatz `{args.data}` | LEER-Ziel {args.leer_target:.0%}\n\n")
        f.write(f"## VERDIKT (Entscheidung NUR auf 57er-clean): **{label}**\n\n")
        f.write("> 63er-hidden ist reiner Kontrollblick, fliesst NICHT in die Entscheidung ein.\n\n")
        f.write("| Eval | Kandidat | Baseline (v5) |\n|---|---|---|\n")
        f.write(f"| 57er-clean | {fmt(cand_clean)} | {fmt(base_clean)} |\n")
        f.write(f"| 63er-hidden | {fmt(cand_hidden)} | {fmt(base_hidden)} |\n\n")
        f.write("### 57er Headline (Kandidat vs Baseline, Counts)\n")
        for l in lines:
            f.write(f"- {l}\n")
        f.write("\n### 57er Schluessel-Klassen (BAI/BAB/BBA/BDD/BAJ)\n")
        for l in cls_lines:
            f.write(f"- {l}\n")
        f.write("\n### Kontrollblick 63er-hidden (NICHT entscheidungsrelevant)\n")
        f.write(f"- Kandidat: {fmt(cand_hidden)}\n- Baseline: {fmt(base_hidden)}\n")
        f.write("- (Nur finaler Blick. Das Hidden-Set ist KEIN Optimierungsziel — sonst verliert es seinen Wert.)\n")
        f.write("\n---\n**LEITPLANKE:** Nur ein KANDIDAT. NICHT produktiv. `active.json` unberuehrt. "
                "Produktions-Freigabe nur durch den Menschen (model-promotion-warden).\n")
    print("REPORT:", md, flush=True)

    # 6) Kandidat markieren (nur wenn nicht schlechter)
    payload = {"name": args.name, "weights": weights, "created": stamp, "leer_target": args.leer_target,
               "verdict": label, "decided_on": "57er-clean",
               "clean": cand_clean, "hidden_control": cand_hidden,
               "baseline_clean": base_clean, "baseline_hidden_control": base_hidden,
               "PRODUKTION_NUR_NACH_FREIGABE": True}
    if label != "NICHT UEBERNEHMEN":
        cf = os.path.join(CAND_DIR, f"{args.name}.json")
        with open(cf, "w", encoding="utf-8") as f:
            json.dump(payload, f, indent=2, ensure_ascii=False)
        print("KANDIDAT markiert:", cf, flush=True)
    else:
        print("KEIN Kandidat (Test nicht bestanden) — v5 bleibt Bestand.", flush=True)

    print(f"\n========== AUTOPILOT-VERDIKT (57er): {label} ==========", flush=True)
    print(f"  Kandidat 57er:  {fmt(cand_clean)}", flush=True)
    print(f"  Baseline 57er:  {fmt(base_clean)}", flush=True)
    print(f"  Kandidat hidden (nur Kontrolle): {fmt(cand_hidden)}", flush=True)
    print(f"  Baseline hidden (nur Kontrolle): {fmt(base_hidden)}", flush=True)


if __name__ == "__main__":
    main()
