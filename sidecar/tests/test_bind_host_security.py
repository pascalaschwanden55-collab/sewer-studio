import os
import shutil
import subprocess
from pathlib import Path

import pytest
from pydantic import ValidationError

from sidecar.config import SidecarSettings


@pytest.mark.parametrize("host", ["localhost", "127.0.0.1", "127.23.45.67", "::1"])
def test_sidecar_settings_accept_loopback_bind_hosts(host):
    assert SidecarSettings(host=host).host == host


@pytest.mark.parametrize("host", ["0.0.0.0", "192.168.1.20", "sidecar.example"])
def test_sidecar_settings_reject_non_loopback_bind_hosts(host):
    with pytest.raises(ValidationError, match="Loopback"):
        SidecarSettings(host=host)


def test_start_script_rejects_non_loopback_before_starting_uvicorn():
    powershell = shutil.which("powershell")
    if not powershell:
        pytest.skip("PowerShell ist nur auf der Windows-Zielmaschine verfuegbar.")

    script = Path(__file__).resolve().parents[1] / "start_sidecar.ps1"
    env = os.environ.copy()
    env["SEWER_SIDECAR_HOST"] = "0.0.0.0"

    result = subprocess.run(
        [powershell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script), "-DryRun"],
        capture_output=True,
        text=True,
        env=env,
        timeout=15,
        check=False,
    )

    output = result.stdout + result.stderr
    assert result.returncode == 1
    assert "nur lokale Adressen" in output
    assert "uvicorn" not in output
