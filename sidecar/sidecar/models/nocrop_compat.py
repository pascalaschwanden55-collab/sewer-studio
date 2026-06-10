"""Identische Kopie von training/vsa_classifier/nocrop_patch.py (Letterbox-Transforms).

Warum es diese Kopie gibt: Die no-crop-Klassifikator-Checkpoints (z.B. vsa_cls_v5_nocrop)
picklen ihre Trainings-Transforms inklusive der Klasse 'nocrop_patch.Letterbox'.
torch.load braucht dafuer ein importierbares Modul namens 'nocrop_patch' — der Sidecar
registriert diese Kopie unter genau dem Namen (siehe yolo_wrapper._ensure_nocrop_module),
damit Checkpoints ausserhalb des Trainings-Verzeichnisses laden.

WICHTIG: Inhaltlich synchron zu training/vsa_classifier/nocrop_patch.py halten.
"""
from PIL import Image
import torchvision.transforms as T

_MEAN = (0.0, 0.0, 0.0)
_STD = (1.0, 1.0, 1.0)


def letterbox_pil(img: "Image.Image", size: int) -> "Image.Image":
    """Skaliert proportional in size x size und padded schwarz (kein Crop, keine Verzerrung)."""
    if img.mode != "RGB":
        img = img.convert("RGB")
    w, h = img.size
    if w == size and h == size:
        return img
    scale = min(size / w, size / h)
    nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
    img = img.resize((nw, nh), Image.BILINEAR)
    canvas = Image.new("RGB", (size, size), (0, 0, 0))
    canvas.paste(img, ((size - nw) // 2, (size - nh) // 2))
    return canvas


class Letterbox:
    """torchvision-kompatible Letterbox-Transform (PIL -> PIL). 'size' fuer Ultralytics-Predict-Check."""

    def __init__(self, size: int):
        self.size = int(size)

    def __call__(self, img):
        return letterbox_pil(img, self.size)


def build_val_tf(size: int):
    return T.Compose([Letterbox(size), T.ToTensor(), T.Normalize(_MEAN, _STD)])


def build_train_tf(size: int):
    return T.Compose([
        Letterbox(size),
        T.RandomHorizontalFlip(0.5),
        T.ColorJitter(0.4, 0.4, 0.4, 0.015),
        T.ToTensor(),
        T.Normalize(_MEAN, _STD),
    ])
