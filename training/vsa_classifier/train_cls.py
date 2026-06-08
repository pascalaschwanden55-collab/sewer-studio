"""
Trainiert einen YOLO-cls VSA-Klassifikator auf dem eval-freien Datensatz
(C:\\KI_BRAIN\\yolo_vsa_cls_dataset, 11 Klassen). Reproduzierbar (seed=42,
deterministic). RTX 5090 / Blackwell: Ultralytics-AMP nutzt bf16 automatisch.

KEIN Eval-Set im Training (Kontamination = 0 verifiziert beim Datensatzbau).
Gemessen wird separat via eval_cls.py gegen das eingefrorene 57er-Clean-Set.

Beispiel:
  python training/vsa_classifier/train_cls.py --epochs 60
  python training/vsa_classifier/train_cls.py --epochs 3 --name vsa_cls_smoke   # Smoke
"""
import argparse
from ultralytics import YOLO


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", default=r"C:\KI_BRAIN\yolo_vsa_cls_dataset")
    ap.add_argument("--model", default="yolo11s-cls.pt")
    ap.add_argument("--epochs", type=int, default=60)
    ap.add_argument("--imgsz", type=int, default=224)
    ap.add_argument("--batch", type=int, default=64)
    ap.add_argument("--patience", type=int, default=15)
    ap.add_argument("--project", default=r"C:\KI_BRAIN\yolo_cls_runs")
    ap.add_argument("--name", default="vsa_cls_v1")
    ap.add_argument("--device", default="0")
    ap.add_argument("--no-crop", action="store_true", help="Letterbox statt Crop (kein Random/CenterCrop)")
    ap.add_argument("--balance", action="store_true", help="WeightedRandomSampler nur im Train (LEER-schuetzend)")
    ap.add_argument("--leer-target", type=float, default=0.28, help="LEER-Anteil im Train bei --balance")
    args = ap.parse_args()

    if args.no_crop:
        from nocrop_patch import patch_dataset
        patch_dataset()
        print("NO-CROP aktiv: Letterbox statt Crop (Training + Val)")

    if args.balance:
        from balance_patch import patch_trainer
        patch_trainer(leer_target=args.leer_target)
        print(f"BALANCE aktiv: WeightedRandomSampler (nur Train, LEER={args.leer_target:.0%})")

    model = YOLO(args.model)
    model.train(
        data=args.data,
        epochs=args.epochs,
        imgsz=args.imgsz,
        batch=args.batch,
        patience=args.patience,   # Early Stopping
        device=args.device,
        seed=42,
        deterministic=True,       # Reproduzierbar (wie Datensatz-Split seed=42)
        project=args.project,
        name=args.name,
        exist_ok=True,
        verbose=True,
    )
    print("FERTIG. Bestes Modell:", rf"{args.project}\{args.name}\weights\best.pt")


if __name__ == "__main__":
    main()
