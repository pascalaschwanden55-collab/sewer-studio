#!/usr/bin/env python3
"""Reine, deterministische Metriken fuer den Detect-Gold-Holdout."""

from __future__ import annotations

import math
from collections import defaultdict
from dataclasses import dataclass
from typing import Any, Mapping, Sequence


@dataclass(frozen=True)
class Box:
    x_center: float
    y_center: float
    width: float
    height: float

    def __post_init__(self) -> None:
        values = (self.x_center, self.y_center, self.width, self.height)
        if not all(math.isfinite(value) for value in values):
            raise ValueError("Box enthaelt keine endlichen Werte.")
        if self.width <= 0.0 or self.height <= 0.0:
            raise ValueError("Boxbreite und -hoehe muessen positiv sein.")
        if (
            self.x_center - self.width / 2.0 < -1e-9
            or self.y_center - self.height / 2.0 < -1e-9
            or self.x_center + self.width / 2.0 > 1.0 + 1e-9
            or self.y_center + self.height / 2.0 > 1.0 + 1e-9
        ):
            raise ValueError("Box liegt ausserhalb des normalisierten Bildes.")

    @property
    def xyxy(self) -> tuple[float, float, float, float]:
        return (
            self.x_center - self.width / 2.0,
            self.y_center - self.height / 2.0,
            self.x_center + self.width / 2.0,
            self.y_center + self.height / 2.0,
        )


@dataclass(frozen=True)
class GroundTruth:
    image_id: str
    sample_id: str
    class_id: int
    class_name: str
    box: Box


@dataclass(frozen=True)
class Prediction:
    image_id: str
    prediction_id: str
    class_id: int
    class_name: str
    confidence: float
    box: Box


def box_iou(left: Box, right: Box) -> float:
    lx1, ly1, lx2, ly2 = left.xyxy
    rx1, ry1, rx2, ry2 = right.xyxy
    intersection = max(0.0, min(lx2, rx2) - max(lx1, rx1)) * max(
        0.0,
        min(ly2, ry2) - max(ly1, ry1),
    )
    left_area = (lx2 - lx1) * (ly2 - ly1)
    right_area = (rx2 - rx1) * (ry2 - ry1)
    union = left_area + right_area - intersection
    return 0.0 if union <= 0.0 else intersection / union


def _ratio(numerator: int, denominator: int) -> float:
    return 0.0 if denominator == 0 else numerator / denominator


def _metrics(tp: int, fp: int, fn: int) -> dict[str, float | int]:
    precision = _ratio(tp, tp + fp)
    recall = _ratio(tp, tp + fn)
    return {
        "tp": tp,
        "fp": fp,
        "fn": fn,
        "precision": precision,
        "recall": recall,
        "f1": (
            0.0
            if precision + recall == 0.0
            else 2.0 * precision * recall / (precision + recall)
        ),
    }


@dataclass
class _FlowEdge:
    to: int
    reverse: int
    capacity: int
    cost: int


def _add_flow_edge(
    graph: list[list[_FlowEdge]],
    source: int,
    target: int,
    capacity: int,
    cost: int,
) -> _FlowEdge:
    forward = _FlowEdge(target, len(graph[target]), capacity, cost)
    backward = _FlowEdge(source, len(graph[source]), 0, -cost)
    graph[source].append(forward)
    graph[target].append(backward)
    return forward


def _maximum_pairs(
    truths: Sequence[GroundTruth],
    predictions: Sequence[Prediction],
    iou_threshold: float,
    *,
    require_same_class: bool,
) -> list[tuple[int, int, float]]:
    """Findet zuerst maximal viele Paare, danach maximalen Gesamt-IoU.

    Ein einfaches Greedy nach dem groessten Einzel-IoU kann bei mehreren
    Objekten einen gueltigen Treffer verlieren. Der kleine Min-Cost-Max-Flow
    liefert dagegen die maximale Paarzahl und entscheidet danach anhand IoU
    sowie stabil sortierter IDs reproduzierbar.
    """

    candidates: list[tuple[int, int, float]] = []
    for truth_index, truth in enumerate(truths):
        for prediction_index, prediction in enumerate(predictions):
            if require_same_class and truth.class_id != prediction.class_id:
                continue
            iou = box_iou(truth.box, prediction.box)
            if iou >= iou_threshold:
                candidates.append((truth_index, prediction_index, iou))
    if not candidates:
        return []

    truth_count = len(truths)
    prediction_count = len(predictions)
    source = 0
    truth_offset = 1
    prediction_offset = truth_offset + truth_count
    sink = prediction_offset + prediction_count
    graph: list[list[_FlowEdge]] = [[] for _ in range(sink + 1)]
    for truth_index in range(truth_count):
        _add_flow_edge(graph, source, truth_offset + truth_index, 1, 0)
    for prediction_index in range(prediction_count):
        _add_flow_edge(
            graph,
            prediction_offset + prediction_index,
            sink,
            1,
            0,
        )

    ordered = sorted(
        candidates,
        key=lambda item: (
            truths[item[0]].sample_id,
            predictions[item[1]].prediction_id,
            item[0],
            item[1],
        ),
    )
    maximum_pairs = min(truth_count, prediction_count)
    iou_multiplier = (len(ordered) + 1) * (maximum_pairs + 1) + 1
    edge_refs: list[tuple[int, int, float, _FlowEdge]] = []
    for stable_rank, (truth_index, prediction_index, iou) in enumerate(ordered):
        iou_units = int(round(iou * 1_000_000_000_000))
        edge = _add_flow_edge(
            graph,
            truth_offset + truth_index,
            prediction_offset + prediction_index,
            1,
            -iou_units * iou_multiplier + stable_rank,
        )
        edge_refs.append((truth_index, prediction_index, iou, edge))

    node_count = len(graph)
    while True:
        distances: list[int | None] = [None] * node_count
        previous: list[tuple[int, int] | None] = [None] * node_count
        distances[source] = 0
        for _ in range(node_count - 1):
            changed = False
            for node, edges in enumerate(graph):
                distance = distances[node]
                if distance is None:
                    continue
                for edge_index, edge in enumerate(edges):
                    if edge.capacity <= 0:
                        continue
                    next_distance = distance + edge.cost
                    old_distance = distances[edge.to]
                    predecessor = (node, edge_index)
                    if old_distance is None or next_distance < old_distance or (
                        next_distance == old_distance
                        and (previous[edge.to] is None or predecessor < previous[edge.to])
                    ):
                        distances[edge.to] = next_distance
                        previous[edge.to] = predecessor
                        changed = True
            if not changed:
                break
        if previous[sink] is None:
            break
        node = sink
        while node != source:
            predecessor = previous[node]
            if predecessor is None:
                raise RuntimeError("Interner Matching-Pfad ist unvollstaendig.")
            previous_node, edge_index = predecessor
            edge = graph[previous_node][edge_index]
            edge.capacity -= 1
            graph[node][edge.reverse].capacity += 1
            node = previous_node

    result = [
        (truth_index, prediction_index, iou)
        for truth_index, prediction_index, iou, edge in edge_refs
        if edge.capacity == 0
    ]
    return sorted(
        result,
        key=lambda item: (
            truths[item[0]].sample_id,
            predictions[item[1]].prediction_id,
        ),
    )


def _validate_inputs(
    truths: Sequence[GroundTruth],
    predictions: Sequence[Prediction],
    class_names: Mapping[int, str],
) -> None:
    if len({truth.sample_id for truth in truths}) != len(truths):
        raise ValueError("Ground-Truth enthaelt doppelte Sample-IDs.")
    prediction_keys = [
        (prediction.image_id, prediction.prediction_id)
        for prediction in predictions
    ]
    if len(set(prediction_keys)) != len(prediction_keys):
        raise ValueError("Vorhersagebeleg enthaelt doppelte Prediction-IDs.")
    for item in [*truths, *predictions]:
        if not item.image_id or not item.class_name:
            raise ValueError("Scoring-Eintrag besitzt eine leere ID oder Klasse.")
        if class_names.get(item.class_id) != item.class_name:
            raise ValueError("Klassen-ID und Klassenname widersprechen sich.")
    for prediction in predictions:
        if not math.isfinite(prediction.confidence) or not 0.0 <= prediction.confidence <= 1.0:
            raise ValueError("Vorhersage besitzt eine ungueltige Konfidenz.")


def score_predictions(
    truths: Sequence[GroundTruth],
    predictions: Sequence[Prediction],
    class_names: Mapping[int, str],
    *,
    iou_threshold: float = 0.5,
) -> dict[str, Any]:
    """Bewertet Klassen- und Geometrietreffer ohne versteckte Schwellenwahl."""

    if not 0.0 < iou_threshold <= 1.0:
        raise ValueError("IoU-Schwelle muss in (0, 1] liegen.")
    if not truths:
        raise ValueError("Der positive Holdout enthaelt keine Ground-Truth.")
    ordered_classes = dict(sorted(class_names.items()))
    if not ordered_classes or list(ordered_classes) != list(range(len(ordered_classes))):
        raise ValueError("Klassen-IDs muessen lueckenlos bei 0 beginnen.")
    _validate_inputs(truths, predictions, ordered_classes)

    truths_by_image: dict[str, list[GroundTruth]] = defaultdict(list)
    predictions_by_image: dict[str, list[Prediction]] = defaultdict(list)
    for truth in truths:
        truths_by_image[truth.image_id].append(truth)
    for prediction in predictions:
        predictions_by_image[prediction.image_id].append(prediction)

    all_image_ids = sorted(set(truths_by_image) | set(predictions_by_image))
    exact_matches: list[dict[str, Any]] = []
    matched_truth_ids: set[str] = set()
    matched_prediction_keys: set[tuple[str, str]] = set()
    geometry_matches: list[dict[str, Any]] = []
    confusion: dict[tuple[str, str], int] = defaultdict(int)
    geometry_truth_ids: set[str] = set()
    geometry_prediction_keys: set[tuple[str, str]] = set()

    for image_id in all_image_ids:
        image_truths = sorted(
            truths_by_image.get(image_id, []),
            key=lambda item: item.sample_id,
        )
        image_predictions = sorted(
            predictions_by_image.get(image_id, []),
            key=lambda item: item.prediction_id,
        )
        for truth_index, prediction_index, iou in _maximum_pairs(
            image_truths,
            image_predictions,
            iou_threshold,
            require_same_class=True,
        ):
            truth = image_truths[truth_index]
            prediction = image_predictions[prediction_index]
            matched_truth_ids.add(truth.sample_id)
            matched_prediction_keys.add((image_id, prediction.prediction_id))
            exact_matches.append(
                {
                    "image_id": image_id,
                    "sample_id": truth.sample_id,
                    "prediction_id": prediction.prediction_id,
                    "class_id": truth.class_id,
                    "class_name": truth.class_name,
                    "iou": iou,
                    "confidence": prediction.confidence,
                }
            )

        for truth_index, prediction_index, iou in _maximum_pairs(
            image_truths,
            image_predictions,
            iou_threshold,
            require_same_class=False,
        ):
            truth = image_truths[truth_index]
            prediction = image_predictions[prediction_index]
            geometry_truth_ids.add(truth.sample_id)
            geometry_prediction_keys.add((image_id, prediction.prediction_id))
            confusion[(truth.class_name, prediction.class_name)] += 1
            geometry_matches.append(
                {
                    "image_id": image_id,
                    "sample_id": truth.sample_id,
                    "prediction_id": prediction.prediction_id,
                    "expected_class": truth.class_name,
                    "predicted_class": prediction.class_name,
                    "iou": iou,
                    "confidence": prediction.confidence,
                }
            )

    per_class: list[dict[str, Any]] = []
    totals = {"tp": 0, "fp": 0, "fn": 0}
    for class_id, class_name in ordered_classes.items():
        class_truths = [truth for truth in truths if truth.class_id == class_id]
        class_predictions = [
            prediction for prediction in predictions if prediction.class_id == class_id
        ]
        tp = sum(truth.sample_id in matched_truth_ids for truth in class_truths)
        fn = len(class_truths) - tp
        fp = sum(
            (prediction.image_id, prediction.prediction_id)
            not in matched_prediction_keys
            for prediction in class_predictions
        )
        values = _metrics(tp, fp, fn)
        totals["tp"] += tp
        totals["fp"] += fp
        totals["fn"] += fn
        per_class.append(
            {
                "class_id": class_id,
                "class_name": class_name,
                "support": len(class_truths),
                "measured": bool(class_truths),
                **values,
            }
        )

    measured = [item for item in per_class if item["measured"]]
    micro = _metrics(totals["tp"], totals["fp"], totals["fn"])
    macro = {
        "classes": len(measured),
        "precision": sum(float(item["precision"]) for item in measured) / len(measured),
        "recall": sum(float(item["recall"]) for item in measured) / len(measured),
        "f1": sum(float(item["f1"]) for item in measured) / len(measured),
    }
    return {
        "iou_threshold": iou_threshold,
        "images": len(truths_by_image),
        "ground_truth_instances": len(truths),
        "predictions": len(predictions),
        "micro": micro,
        "macro": macro,
        "per_class": per_class,
        "exact_matches": sorted(
            exact_matches,
            key=lambda item: (item["image_id"], item["sample_id"]),
        ),
        "geometry": {
            "matched": len(geometry_matches),
            "unmatched_ground_truth": len(truths) - len(geometry_truth_ids),
            "unmatched_predictions": len(predictions) - len(geometry_prediction_keys),
            "confusion": [
                {
                    "expected_class": expected,
                    "predicted_class": predicted,
                    "count": count,
                }
                for (expected, predicted), count in sorted(confusion.items())
            ],
            "matches": sorted(
                geometry_matches,
                key=lambda item: (item["image_id"], item["sample_id"]),
            ),
        },
    }
