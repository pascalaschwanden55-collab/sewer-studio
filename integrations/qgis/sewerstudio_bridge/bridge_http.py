import json
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import Request, urlopen


ACCEPT_HEADER = "application/json, application/geo+json"


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
    try:
        request = Request(url, headers={"Accept": ACCEPT_HEADER})
        with urlopen(request, timeout=timeout) as response:
            if response.status != 200:
                return BridgeFetchResult(
                    warning=f"Bridge-Request fehlgeschlagen ({response.status}): {url}"
                )
            return BridgeFetchResult(value=response.read())
    except HTTPError as ex:
        if ex.code == 404:
            return BridgeFetchResult()
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
