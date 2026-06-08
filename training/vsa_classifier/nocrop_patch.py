"""
No-Crop (Letterbox) fuer Ultralytics-Klassifikation.

Ultralytics croppt standardmaessig: Training RandomResizedCrop(scale 0.08-1.0),
Val/Inferenz Resize(kurze Seite)+CenterCrop -> seitliche Bildbereiche (Rohrwand 3:00/9:00:
Anschluss/Dichtung/Versatz) werden WEGGESCHNITTEN.

Dieses Modul ersetzt den Crop durch LETTERBOX: proportional auf size skalieren, Raender
schwarz padden. Behaelt das ganze Bild UND die runde Rohrform (kein Squash/Verzerren).
Sonst identisch (ToTensor, Normalize(0,1), train: HFlip+ColorJitter wie Ultralytics-Default).
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


def patch_dataset():
    """Monkeypatch: ClassificationDataset nutzt Letterbox statt Crop (Training + Val)."""
    import ultralytics.data.dataset as ds

    def _aug(size=224, **kw):
        return build_train_tf(size)

    def _tf(size=224, **kw):
        return build_val_tf(size)

    ds.classify_augmentations = _aug
    ds.classify_transforms = _tf
    return True
