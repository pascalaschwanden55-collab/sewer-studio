#!/usr/bin/env python3
"""Plant frische PDF-/Video-Quellen fuer den Detect-Release-Extraktor."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
from pathlib import Path
from typing import Any, Sequence


SCRIPT_ROOT = Path(__file__).resolve().parent
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))

import bcc_release_holdout as protection
import prepare_detect_release_holdout as holdout


SELECTION_SALT = "detect-release-pdf-extraction-v1"
VIDEO_SUFFIXES = {".mpg", ".mpeg", ".mp4", ".avi", ".mov", ".mp2"}
PROTOCOL_NAME = re.compile(r"^\d{8}[_-].+", re.IGNORECASE)


def _find_video(pdf: Path) -> Path | None:
    parent = protection._safe_existing_path(pdf.parent, pdf.parent, expect_file=False)
    videos: list[Path] = []
    for entry in sorted(parent.iterdir(), key=lambda item: item.name.casefold()):
        if protection._is_reparse_point(entry):
            raise ValueError(f"Verknuepfung im PDF-Quellordner ist nicht erlaubt: {entry}")
        if entry.is_file() and entry.suffix.casefold() in VIDEO_SUFFIXES:
            videos.append(protection._safe_existing_path(entry, parent, expect_file=True))
    if not videos:
        return None
    exact = [video for video in videos if video.stem.casefold() == pdf.stem.casefold()]
    if not exact:
        return None
    if len(exact) != 1:
        raise ValueError(
            f"Mehrere exakt zum PDF passende Videos gefunden: {pdf.name}"
        )
    return exact[0]


def discover_sources(
    source_roots: Sequence[Path],
    contamination: protection.ContaminationSnapshot,
) -> tuple[list[tuple[holdout.SourcePdf, Path]], dict[str, int]]:
    by_hash: dict[str, tuple[holdout.SourcePdf, Path]] = {}
    ambiguous: set[str] = set()
    counts = {
        "pdf_files": 0,
        "strict_protocol_names": 0,
        "blocked_holding": 0,
        "without_video": 0,
        "ambiguous_pdf_hash": 0,
    }
    for raw_root in source_roots:
        root = holdout._safe_root(raw_root, "PDF-Quellordner")
        for pdf in holdout._iter_pdfs(root):
            counts["pdf_files"] += 1
            if not PROTOCOL_NAME.match(pdf.stem):
                continue
            counts["strict_protocol_names"] += 1
            holding_key = holdout.resolve_pdf_holding(pdf, root)
            if holding_key is None:
                continue
            if protection._holding_aliases(holding_key) & contamination.holding_aliases:
                counts["blocked_holding"] += 1
                continue
            video = _find_video(pdf)
            if video is None:
                counts["without_video"] += 1
                continue
            digest = holdout._sha256_file(pdf)
            if digest in ambiguous:
                continue
            source = holdout.SourcePdf(
                path=pdf,
                sha256=digest,
                name=pdf.name,
                holding_key=holding_key,
                physical_holding_key=protection._physical_holding_key(holding_key),
            )
            previous = by_hash.get(digest)
            if previous is not None and previous[0].physical_holding_key != source.physical_holding_key:
                by_hash.pop(digest, None)
                ambiguous.add(digest)
                continue
            candidate = (source, video)
            if previous is None or str(pdf).casefold() < str(previous[0].path).casefold():
                by_hash[digest] = candidate
    counts["ambiguous_pdf_hash"] = len(ambiguous)
    return list(by_hash.values()), counts


def select_sources(
    sources: Sequence[tuple[holdout.SourcePdf, Path]],
    count: int,
    minimum: int,
) -> tuple[tuple[holdout.SourcePdf, Path], ...]:
    by_holding: dict[str, list[tuple[holdout.SourcePdf, Path]]] = {}
    for item in sources:
        by_holding.setdefault(item[0].physical_holding_key, []).append(item)
    selected_per_holding: list[tuple[holdout.SourcePdf, Path]] = []
    for physical, items in by_holding.items():
        selected_per_holding.append(
            min(
                items,
                key=lambda item: hashlib.sha256(
                    f"{SELECTION_SALT}|pdf|{physical}|{item[0].sha256}".encode("utf-8")
                ).hexdigest(),
            )
        )
    selected_per_holding.sort(
        key=lambda item: hashlib.sha256(
            f"{SELECTION_SALT}|holding|{item[0].physical_holding_key}".encode("utf-8")
        ).hexdigest()
    )
    chosen = tuple(selected_per_holding[:count])
    if len(chosen) < minimum:
        raise ValueError(f"Zu wenig frische PDF-/Video-Haltungen: {len(chosen)}/{minimum}.")
    return chosen


def build_request(
    knowledge_root: Path,
    selected: Sequence[tuple[holdout.SourcePdf, Path]],
    extraction_output_root: Path,
    ffmpeg_path: Path,
    ffprobe_path: Path,
) -> dict[str, Any]:
    fractions = (0.25, 0.5, 0.75)
    pdfs: list[dict[str, Any]] = []
    for source, video in selected:
        fraction = fractions[int(source.sha256[:2], 16) % len(fractions)]
        pdfs.append(
            {
                "path": str(source.path),
                "pdf_sha256": source.sha256,
                "haltung_key": source.holding_key,
                "video_path": str(video),
                "background_fraction": fraction,
            }
        )
    return {
        "knowledge_root": str(knowledge_root),
        "output_root": str(extraction_output_root),
        "ffmpeg_path": str(ffmpeg_path),
        "ffprobe_path": str(ffprobe_path),
        "pdfs": pdfs,
    }


def _prepare_new_output_paths(
    request_output: Path,
    extraction_output: Path,
) -> tuple[Path, Path]:
    request = Path(os.path.abspath(request_output))
    extraction = Path(os.path.abspath(extraction_output))
    request_parent = holdout._safe_root(request.parent, "Auftragsordner")
    extraction_parent = holdout._safe_root(extraction.parent, "Extraktions-Zielordner")
    if request.parent != request_parent or extraction.parent != extraction_parent:
        raise ValueError("Ausgabepfade muessen direkt in einem sicheren Ordner liegen.")
    if request.suffix.casefold() != ".json":
        raise ValueError("Die Auftragsdatei muss eine JSON-Datei sein.")
    if request.exists() or extraction.exists():
        raise FileExistsError(
            "Auftragsdatei oder Extraktionsziel existiert bereits; nichts wird ueberschrieben."
        )
    return request, extraction


def _parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Frische PDF-/Video-Auftraege fuer das Detect-Release-Holdout planen.")
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument(
        "--class-map",
        type=Path,
        default=holdout.REPOSITORY_ROOT / "training" / "class_maps" / "detect_class_map_v3.json",
    )
    parser.add_argument("--source-root", type=Path, action="append", required=True)
    parser.add_argument("--request-output", type=Path, required=True)
    parser.add_argument("--extraction-output-root", type=Path, required=True)
    parser.add_argument("--ffmpeg", type=Path, required=True)
    parser.add_argument("--ffprobe", type=Path, required=True)
    parser.add_argument("--source-holdings", type=int, default=150)
    parser.add_argument("--minimum-holdings", type=int, default=100)
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args(argv)
    if args.source_holdings < args.minimum_holdings or args.minimum_holdings < 1:
        parser.error("source-holdings muss mindestens so gross wie minimum-holdings sein.")
    return args


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv if argv is not None else sys.argv[1:])
    try:
        knowledge = holdout._safe_root(args.knowledge_root, "KnowledgeRoot")
        candidate = holdout.validate_candidate(args.candidate, args.class_map)
        contamination = protection.scan_contamination(knowledge, candidate.base_model_path)
        sources, counts = discover_sources(tuple(args.source_root), contamination)
        selected = select_sources(sources, args.source_holdings, args.minimum_holdings)
        ffmpeg = holdout._safe_file(args.ffmpeg, args.ffmpeg.parent, "ffmpeg")
        ffprobe = holdout._safe_file(args.ffprobe, args.ffprobe.parent, "ffprobe")
        request = build_request(
            knowledge,
            selected,
            Path(os.path.abspath(args.extraction_output_root)),
            ffmpeg,
            ffprobe,
        )
        print(json.dumps({**counts, "selected_holdings": len(selected)}, ensure_ascii=False, indent=2))
        print(f"Auftrag: {args.request_output}")
        print(f"Extraktionsziel: {args.extraction_output_root}")
        if not args.execute:
            print("Dry-Run: Es wurde nichts geschrieben.")
            return 0
        request_output, extraction_output = _prepare_new_output_paths(
            args.request_output,
            args.extraction_output_root,
        )
        extraction_output.mkdir(parents=False)
        protection._safe_existing_path(
            extraction_output,
            extraction_output.parent,
            expect_file=False,
        )
        try:
            protection._atomic_write(request_output, holdout._json_bytes(request))
        except Exception:
            extraction_output.rmdir()
            raise
        print("Extraktionsauftrag sicher geschrieben.")
        return 0
    except (OSError, ValueError, FileExistsError) as error:
        print(f"FEHLER: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
