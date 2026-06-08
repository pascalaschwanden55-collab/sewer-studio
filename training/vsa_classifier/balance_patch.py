"""
WeightedRandomSampler fuer Ultralytics-Klassifikation — NUR der Train-Loader.
Val/Eval bleiben unveraendert (sonst waere die Val-Metrik verfaelscht).

Gewichtungs-Schema (LEER-schuetzend, gegen den vom User benannten Risiko-Fall):
- LEER behaelt seinen Ist-Anteil (Default 28%, ~= Realitaet/Eval).
- Die uebrigen (Befund-)Klassen werden untereinander GLEICH verteilt auf (1-leer_target).
  -> kleine Klassen (BBA/BAI/BAB) werden hochgezogen, ohne LEER zu druecken.
weight(sample) = ziel_prob[klasse] / count[klasse].
"""
import os
from collections import Counter

import torch
from torch.utils.data import WeightedRandomSampler


def compute_sample_weights(samples, names, leer_target=0.28):
    """samples: Liste [file, class_idx, ...]; names: {idx: klassenname}."""
    labels = [s[1] for s in samples]
    counts = Counter(labels)
    classes = sorted(counts.keys())
    leer_idx = next((i for i in classes if str(names.get(i, "")).upper() == "LEER"), None)
    n_find = sum(1 for c in classes if c != leer_idx)
    target = {}
    if leer_idx is None or n_find == 0:
        for c in classes:
            target[c] = 1.0 / len(classes)
    else:
        for c in classes:
            target[c] = leer_target if c == leer_idx else (1.0 - leer_target) / n_find
    weights = [target[lbl] / counts[lbl] for lbl in labels]
    return weights, counts, target, leer_idx


def patch_trainer(leer_target=0.28):
    """Monkeypatch ClassificationTrainer.get_dataloader: Train -> WeightedRandomSampler."""
    from ultralytics.models.yolo.classify.train import ClassificationTrainer
    from ultralytics.data.build import InfiniteDataLoader, seed_worker
    from ultralytics.utils import RANK, LOGGER

    _orig = ClassificationTrainer.get_dataloader

    def patched(self, dataset_path, batch_size=16, rank=0, mode="train"):
        loader = _orig(self, dataset_path, batch_size, rank, mode)
        if mode != "train":
            return loader  # Val unveraendert
        ds = loader.dataset
        names = {i: c for i, c in enumerate(ds.base.classes)}
        weights, counts, target, leer_idx = compute_sample_weights(ds.samples, names, leer_target)
        gen = torch.Generator()
        gen.manual_seed(6148914691236517205 + RANK)
        sampler = WeightedRandomSampler(weights, num_samples=len(ds), replacement=True, generator=gen)
        nd = torch.cuda.device_count()
        nw = min((os.cpu_count() or 1) // max(nd, 1), self.args.workers)
        batch = min(batch_size, len(ds))
        new_loader = InfiniteDataLoader(
            dataset=ds,
            batch_size=batch,
            shuffle=False,
            num_workers=nw,
            sampler=sampler,
            prefetch_factor=4 if nw > 0 else None,
            pin_memory=nd > 0,
            collate_fn=getattr(ds, "collate_fn", None),
            worker_init_fn=seed_worker,
            generator=gen,
            drop_last=False,
        )
        dist = ", ".join(f"{names[c]}:{counts[c]}->{target[c] * 100:.1f}%" for c in sorted(counts))
        LOGGER.info(f"V6 WeightedRandomSampler aktiv (nur Train, LEER={leer_target:.0%}). {dist}")
        return new_loader

    ClassificationTrainer.get_dataloader = patched
    return True
