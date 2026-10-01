from __future__ import annotations

import argparse
from pathlib import Path
from typing import Sequence

try:
    from tools.EvalVisibilityReview.bcc_release_holdout_review_server import (
        BccHardNegativeReviewStore,
        create_server,
    )
except ModuleNotFoundError:
    from bcc_release_holdout_review_server import (  # type: ignore[no-redef]
        BccHardNegativeReviewStore,
        create_server,
    )


def run_server(
    queue_root: Path,
    output_path: Path,
    reviewer: str,
    port: int = 8774,
) -> None:
    store = BccHardNegativeReviewStore(
        queue_root,
        output_path,
        reviewer,
    )
    state = store.prepare_output()
    server = create_server(store, port)
    actual_port = server.server_address[1]
    print(f"BCC Hard-Negative-Pruefung: http://127.0.0.1:{actual_port}/")
    print(f"Bilder: {state['total']}; offen: {state['open']}")
    print(f"Review-Ausgabe: {store.output_path}")
    print("Stoppen mit Strg+C")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Lokale blinde BCC-Hard-Negative-Pruefung"
    )
    parser.add_argument("--queue", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--reviewer", required=True)
    parser.add_argument("--port", type=int, default=8774)
    parser.add_argument(
        "--prepare-only",
        action="store_true",
        help="Review-Datei vorbereiten, aber keinen HTTP-Server starten.",
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    if args.prepare_only:
        store = BccHardNegativeReviewStore(
            args.queue,
            args.output,
            args.reviewer,
        )
        state = store.prepare_output()
        print(f"Review vorbereitet: {store.output_path}")
        print(f"Bilder: {state['total']}; offen: {state['open']}")
        return 0
    run_server(
        args.queue,
        args.output,
        args.reviewer,
        args.port,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
