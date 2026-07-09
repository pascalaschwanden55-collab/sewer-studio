from types import SimpleNamespace


def test_gpu_utilization_uses_ttl_cache(monkeypatch):
    from sidecar.models import yolo_wrapper

    yolo_wrapper._reset_gpu_utilization_cache_for_tests()
    calls = []

    def fake_run(*args, **kwargs):
        calls.append((args, kwargs))
        return SimpleNamespace(returncode=0, stdout="42\n")

    monkeypatch.setattr(yolo_wrapper, "_resolve_device", lambda: "cuda:0")
    monkeypatch.setattr(yolo_wrapper.subprocess, "run", fake_run)

    try:
        assert yolo_wrapper._gpu_utilization_percent() == 42.0
        assert yolo_wrapper._gpu_utilization_percent() == 42.0
        assert len(calls) == 1
    finally:
        yolo_wrapper._reset_gpu_utilization_cache_for_tests()
