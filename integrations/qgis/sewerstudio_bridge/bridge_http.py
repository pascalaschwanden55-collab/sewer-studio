import json
import os
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import Request, urlopen


ACCEPT_HEADER = "application/json, application/geo+json"

# Anmeldung an der Bruecke (SewerStudio-Gesamtaudit 2026-08-14, P1-3).
# SewerStudio legt den Token beim Start in seinem AppData-Ordner ab; das Plugin
# laeuft als derselbe Benutzer und darf ihn lesen. Ohne Token antwortet die
# Bruecke mit 401 - "nur lokal" ist keine Grenze, wenn mehrere Programme lokal laufen.
TOKEN_HEADER = "X-QGIS-Bridge-Token"
TOKEN_ENV_VAR = "SEWERSTUDIO_QGIS_BRIDGE_TOKEN"
APPDATA_ENV_VAR = "SEWERSTUDIO_APPDATA_DIR"
TOKEN_FILE_NAME = ".qgis_bridge_token"
PRODUCT_NAME = "SewerStudio"


def token_file_path():
    """Gleiche Ableitung wie AppDataPathResolver.Resolve in SewerStudio."""

    override = os.environ.get(APPDATA_ENV_VAR)
    if override and override.strip():
        return Path(override.strip()) / TOKEN_FILE_NAME

    local_appdata = os.environ.get("LOCALAPPDATA")
    if not local_appdata:
        return None
    return Path(local_appdata) / PRODUCT_NAME / TOKEN_FILE_NAME


def read_bridge_token():
    """Liest den Token: Env-Var vor Datei. Fehlt beides, wird None geliefert."""

    env_token = os.environ.get(TOKEN_ENV_VAR)
    if env_token and env_token.strip():
        return env_token.strip()

    pfad = token_file_path()
    if pfad is None:
        return None

    try:
        inhalt = pfad.read_text(encoding="utf-8").strip()
    except OSError:
        return None

    return inhalt or None


class BridgeFetchResult:
    """Ergebnis ohne Ausnahme: Nutzdaten oder eine protokollierbare Warnung."""

    def __init__(self, value=None, warning=None):
        self.value = value
        self.warning = warning


def fetch_bridge_bytes(base_url, endpoint, timeout=1.5):
    """Liest einen lokalen Bridge-Endpunkt. 404 ist erwartbar und bleibt still."""

    base = (base_url or "").strip().rstrip("/")
    if not _is_loopback_http_url(base):
        return BridgeFetchResult(
            warning="Bridge-URL muss eine lokale HTTP-Adresse wie http://127.0.0.1:8765 sein."
        )
    if not endpoint.startswith("/") or endpoint.startswith("//"):
        return BridgeFetchResult(warning=f"Ungueltiger Bridge-Endpunkt: {endpoint}")

    url = f"{base}{endpoint}"
    headers = {"Accept": ACCEPT_HEADER}
    token = read_bridge_token()
    if token:
        headers[TOKEN_HEADER] = token

    try:
        request = Request(url, headers=headers)
        with urlopen(request, timeout=timeout) as response:
            if response.status != 200:
                return BridgeFetchResult(
                    warning=f"Bridge-Request fehlgeschlagen ({response.status}): {url}"
                )
            return BridgeFetchResult(value=response.read())
    except HTTPError as ex:
        if ex.code == 404:
            return BridgeFetchResult()
        if ex.code == 401:
            # Klartext statt roher Fehlernummer: der Benutzer soll wissen, was fehlt.
            return BridgeFetchResult(
                warning=(
                    "Bridge-Anmeldung fehlgeschlagen (401). SewerStudio muss laufen; "
                    f"der Token wird aus {TOKEN_FILE_NAME} im SewerStudio-AppData-Ordner "
                    f"oder aus {TOKEN_ENV_VAR} gelesen."
                )
            )
        return BridgeFetchResult(
            warning=f"Bridge-Request fehlgeschlagen ({ex.code}): {url}"
        )
    except (URLError, TimeoutError, OSError, ValueError) as ex:
        return BridgeFetchResult(warning=f"Bridge nicht erreichbar: {url} ({ex})")


def fetch_bridge_json(base_url, endpoint, timeout=1.5):
    result = fetch_bridge_bytes(base_url, endpoint, timeout)
    if result.value is None or result.warning is not None:
        return result

    try:
        return BridgeFetchResult(value=json.loads(result.value.decode("utf-8")))
    except (UnicodeDecodeError, json.JSONDecodeError):
        return BridgeFetchResult(warning=f"Ungueltiges JSON von {endpoint}")


def _is_loopback_http_url(url):
    try:
        parsed = urlsplit(url)
        port = parsed.port
        return (
            parsed.scheme.lower() == "http"
            and parsed.hostname in ("127.0.0.1", "localhost", "::1")
            and (port is None or 1 <= port <= 65535)
            and parsed.username is None
            and parsed.password is None
            and parsed.query == ""
            and parsed.fragment == ""
            and parsed.path in ("", "/")
        )
    except (TypeError, ValueError):
        return False
