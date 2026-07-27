# AGENTS.md — Einstieg für Codex und andere Agenten

Immer einfach und ehrlich antworten. Denken und Antworten immer auf Deutsch.

## Verbindliche Projektbeschreibung

Vor Änderungen zuerst [`CLAUDE.md`](CLAUDE.md) vollständig lesen. Dort stehen der
aktuelle Aufbau, die KI-Pipeline, wichtige Klassen und fachliche Regeln. Diese Datei
ist bewusst nur der kurze Einstieg und dupliziert die Architektur nicht.

SewerStudio ist heute eine Windows-WPF-Anwendung auf .NET 10. Zum System gehören
unter anderem:

- Projekt-, Haltungs- und Schachtdaten mit VSA-KEK/EN-13508-2-Codierung
- PDF-, XTF-, WinCan-, IBAK- und Medienimporte
- Videoauswertung mit LibVLC
- lokaler Python-Sidecar für YOLO, Grounding DINO und SAM 2.1
- Ollama/Qwen, SQLite-Wissensdatenbank und Trainingsabläufe
- lokale QGIS-Brücke und Sicherungs-/Wiederherstellungsfunktionen

## Arbeitsregeln

- Wartbarkeit, Robustheit, Sicherheit und messbare Leistung sind wichtiger als
  schnelle Grossumbauten.
- Keine God-Classes erweitern. Neue Fachlogik in kleine Services/Controller legen;
  UI-Code bleibt dünn.
- Neue Workflow-/Orchestrierungsklassen (Request/Actions/Result-Muster) gehören nach
  `src/AuswertungPro.Next.Application/UseCases/`, nicht nach `UI/Ai/` — der Bestand
  dort ist per `UiAiFreezeArchitectureTests` eingefroren.
- Öffentliche Fassaden und gespeicherte Datenformate bei Umbauten erhalten.
- Kundenoriginale nie verändern. Dateioperationen absichern und Fehler pro Datei
  protokollieren, damit ein Defekt nicht den ganzen Import abbricht.
- Keine neuen NuGet-Pakete und kein grosses Refactoring ohne Rücksprache.
- Riskante Änderungen zuerst mit einem fokussierten Verhaltenstest schützen.
- Laufendes SewerStudio nicht automatisch beenden. Vor einem Build darf nur ein
  hängen gebliebener `testhost`-Prozess beendet werden.
- Nach Änderungen an Services, Schnittstellen, Datenmodellen, Import/Export oder
  KI-Pipeline den Skill `sewer-architektur` mit dem echten Code abgleichen,
  aktualisieren und validieren.

## Build und Tests

Schneller Alltags-Build:

```powershell
dotnet build AuswertungPro.Dev.slnf -c Release --no-restore
```

Vor jedem Commit mit Codeänderungen den vollständigen Release-Weg prüfen:

```powershell
dotnet build AuswertungPro.sln -c Release --no-restore
dotnet test tests\AuswertungPro.Next.Infrastructure.Tests\AuswertungPro.Next.Infrastructure.Tests.csproj -c Release --no-build --no-restore
dotnet test tests\AuswertungPro.Next.Pipeline.Tests\AuswertungPro.Next.Pipeline.Tests.csproj -c Release --no-build --no-restore
dotnet test tests\AuswertungPro.Next.UI.Tests\AuswertungPro.Next.UI.Tests.csproj -c Release --no-build --no-restore
dotnet test tests\ProjectModernizer.Tests\ProjectModernizer.Tests.csproj -c Release --no-build --no-restore
```

Bei Sidecar- oder QGIS-Arbeit zusätzlich:

```powershell
cd sidecar
.\.venv\Scripts\python.exe -m pytest -m "not gpu" -q
cd ..
python -m unittest discover integrations\qgis\tests -v
```

Neue Funktionen werden mit kurzer, verständlicher Beschreibung, passendem Test und
aktualisierter Dokumentation geliefert.
