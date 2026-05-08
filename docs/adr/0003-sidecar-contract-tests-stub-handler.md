# ADR-0003: Sidecar-Contract-Tests via StubHandler ohne externe Deps

- **Status**: accepted
- **Datum**: 2026-05-07
- **Verantwortlich**: Solo-Entwicklung

## Kontext

Der Python-Sidecar (FastAPI auf Port 8100) ist ein wichtiges Aussen-System.
Er bietet Endpoints fuer YOLO, SAM, DINO, Florence-2 etc. Bei Aenderungen
am Sidecar-Vertrag (URL-Pfade, Auth-Header, Status-Codes) brechen
schweigend C#-Aufrufer.

Vor dem Audit gab es:
- `SidecarResilienceTests` (4 Tests) — Polly-Circuit-Breaker
- `VisionPipelineClientTests` (7 Tests) — JSON-Roundtrip + isolated Client

Es **fehlte**: Tests fuer den **HTTP-Vertrag** — Auth-Header,
Status-Codes (401/404/500), Endpoint-Pfade, Cancellation.

## Entscheidung

Drei-Cluster-Setup:

### Cluster A+B: Stub-basierte Contract-Tests
- Keine externe NuGet-Dependency (kein WireMock.Net).
- Nutzt das bereits etablierte `StubHandler : HttpMessageHandler`-Pattern
  aus `SidecarResilienceTests` — erweitert um Request-Capture.
- 12 Tests in einer Datei: `SidecarContractTests.cs`.

### Cluster C: Live-Tests gegen echten Sidecar (opt-in)
- Eigene Datei `SidecarLiveContractTests.cs` mit `[Trait("Category", "LiveSidecar")]`.
- Default-Filter `.runsettings` schliesst sie aus jedem normalen
  `dotnet test`-Lauf aus.
- Eigene `.runsettings.live` fuer manuelle Ausfuehrung.
- xUnit 2.7 hat kein natives Runtime-Skip — early-return mit
  `ITestOutputHelper`-Log statt neuer Test-Dependency.

## Alternativen erwogen

1. **WireMock.Net**: NuGet-Paket, sauberer Stub-Server in eigenem Prozess.
   Verworfen weil:
   - Stilbruch zum bestehenden Repo-Pattern (StubHandler).
   - Zusaetzliche Dependency + Lizenz-Frage.
   - Lernkurve fuer ein einmaliges Pattern-Setup.

2. **Reine Live-Tests**: alle Tests gegen echten Sidecar.
   Verworfen weil:
   - Funktioniert nicht in CI (kein Sidecar in GitHub Actions).
   - Sidecar-Start dauert ~5 s pro Test-Run.
   - Tests waeren flaky bei Sidecar-Crashes.

3. **Xunit.SkippableFact**: NuGet fuer Runtime-Skip.
   Verworfen weil:
   - Externe Dep fuer einen einzigen Use-Case.
   - Early-Return mit Output-Log liefert die gleiche Klarheit.

## Konsequenzen

**Positiv:**
- 15 neue Tests, 0 neue NuGet-Dependencies.
- Default-Lauf: `Sidecar` nicht erforderlich, alle 12 Stub-Tests in 44 ms.
- Live-Lauf: opt-in, gracefully early-return wenn Sidecar nicht da.
- Code-Pattern ist konsistent zum bestehenden Repo.

**Negativ:**
- Stub-Tests pruefen nur den Client-seitigen Vertrag — der echte
  Sidecar koennte trotzdem anders antworten als gestubt.
- Mitigation: Live-Tests sollten **mindestens vor jedem Release**
  ausgefuehrt werden (manuell gegen echten Sidecar).

## Test-Cluster-Inhalt

| Cluster | Datei | Tests |
|---|---|---:|
| A | `SidecarContractTests` (Health + Auth-Header) | 6 |
| B | `SidecarContractTests` (POST-Endpoints + Status-Codes + Cancellation) | 6 |
| C | `SidecarLiveContractTests` (Live, opt-in) | 3 |

## Referenzen

- Tests: `tests/AuswertungPro.Next.Pipeline.Tests/SidecarContractTests.cs`
- Live-Tests: `tests/AuswertungPro.Next.Pipeline.Tests/SidecarLiveContractTests.cs`
- Commits: `badac39` (Cluster A), `2cb226d` (Cluster B), `da156bb` (Cluster C)
- Aufruf der Live-Tests:
  ```powershell
  dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj `
    -s tests/AuswertungPro.Next.Pipeline.Tests/.runsettings.live `
    --filter "Category=LiveSidecar"
  ```
