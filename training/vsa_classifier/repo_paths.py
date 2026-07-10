"""Gemeinsame, vom Repository-Ordner unabhaengige Pfade der Trainingsskripte."""

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
EVAL_REVIEW_ROOT = REPO_ROOT / "EvalVisibilityReview_20260525"
CLEAN_EVAL_ROOT = str(EVAL_REVIEW_ROOT / "eval_visible_clean_eval_set")
HIDDEN_EVAL_ROOT = str(EVAL_REVIEW_ROOT / "eval_unclean_or_hidden_eval_set")
BENCHMARK_REPORT_ROOT = str(REPO_ROOT / "docs" / "benchmarks")
