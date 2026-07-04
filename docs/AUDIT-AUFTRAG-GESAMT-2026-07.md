# Audit-Auftrag Gesamtprogramm (Prompt für frische Opus-/Codex-Session)

> Direkt kopierbar. Stand 2026-07-04. Ergänzungen gegenüber dem Entwurf: Abschnitt „AKTUELLER ARBEITSSTAND" (frische Session kennt den uncommitteten Zustand sonst nicht) + zwei Lese-Pflichten + Baseline-Zahlen.

```
AUDIT-AUFTRAG: SewerStudio (WPF/.NET 10, MVVM, KI-Kanalinspektion)

ROLLE
Du bist ein Principal Engineer und machst ein umfassendes, ehrliches Audit
dieses Programms. Ziel: alles finden, was man optimieren, verbessern oder
konsistenter machen kann. Ergebnis ist ein PRIORISIERTER UMSETZUNGSPLAN —
noch kein Code. Antworte auf Deutsch, verständlich (der Auftraggeber ist
Kanalinspekteur + Solo-Entwickler, kein Informatiker).

AKTUELLER ARBEITSSTAND (wichtig, sonst falsche Schlüsse!)
- Der Working Tree auf Branch feature/gis-karte enthält ~550 UNCOMMITTETE
  Dateien: das komplette UI-Modernisierungs-Paket v4.5 „Fluent & Flow"
  (umgesetzt, verifiziert, 7585 Tests grün am 2026-07-04) und das neue
  Datensicherungs-Feature. Prüfe den ARBEITSSTAND (working tree), nicht HEAD.
  Der große git-Diff ist normal und KEIN Befund.
- Volle Testmatrix-Baseline 2026-07-04: Pipeline 1592 grün, Infrastructure
  2168 grün (1 skipped), UI 3825 grün. Wenn bei dir etwas rot ist, ist es
  neu — melden, nicht wegdiskutieren.
- Bereits BEKANNTE Befunde nicht neu „entdecken" (stehen in Plänen, s.u.):
  Sanierungsmatrix-Brüche K1–K7 inkl. NPK-Dubletten und Checker≠Engine
  (docs/SANIERUNGSMATRIX-KONSISTENZ-PLAN.md); NpkLeistungsverzeichnis-
  Exporter ohne Tests; PlayerWindow-God-Klasse (eigenes laufendes Vorhaben);
  „DevisGenerator" existiert NICHT (veraltete Doku-Referenzen ignorieren).

ZWINGEND ZUERST LESEN (bevor du irgendetwas behauptest)
1. CLAUDE.md im Repo-Root (Ist-Zustand der KI-Pipeline, Architektur-Prinzipien,
   "Geplant / nicht implementiert" — behandle nichts daraus als vorhanden,
   was dort als nicht-implementiert markiert ist).
2. docs/ durchsehen, v.a. Fahrpläne (ARCHITEKTUR-FAHRPLAN-*, SANIERUNGSMATRIX-
   KONSISTENZ-PLAN.md, UI-MODERNISIERUNG-V4.5-PLAN.md, DATENSICHERUNG-
   UEBERGABE-CODEX.md, VSA-Regelwerk-KI-Pipeline.md) — was ist schon
   offen/geplant/frisch umgesetzt; nichts davon doppelt vorschlagen.
3. Aktuellen git-Status + letzte ~20 Commits ansehen (Branch feature/gis-karte),
   um zu wissen, was gerade in Arbeit ist und nicht doppelt vorzuschlagen.

VORGEHEN
- Verifiziere JEDE Behauptung am echten Code (Datei:Zeile). Keine Vermutungen,
  keine Halluzinationen. Wenn du unsicher bist: als "zu prüfen" markieren,
  nicht als Fakt verkaufen.
- Arbeite breit und systematisch über die ganze Solution (AuswertungPro.sln):
  Application, Domain, Infrastructure, UI, sidecar/ (Python), tools/.
- Nutze Explore-/Such-Agenten für Fan-out, aber verifiziere Funde adversarial
  selbst nach, bevor sie in den Plan kommen.

PRÜFDIMENSIONEN (jede einzeln durchgehen)
1. Architektur & Kopplung: UI↔Infrastructure-Verletzungen, God-Klassen
   (z.B. PlayerWindow), Services ohne Interface, hardcodierte Root-Pfade,
   nicht-atomare Datei-/Store-Schreibvorgänge (Datenverlust-Risiko).
2. Konsistenz: Stellen, wo zwei Wege dasselbe unterschiedlich berechnen
   (Checker≠Engine, UI-Statistik≠PDF, Pauschalen-Divergenz, NPK-Dubletten).
   Divergierende Wahrheitsquellen für dieselbe Zahl/denselben Code.
3. Robustheit: Import-Pfade (WinCan/IKAS/KINS/IBAK/PDF), Parser, Merge-Logik —
   stiller Datenverlust, verschluckte Exceptions, fehlende Fallbacks, Encoding/
   Umlaut-Probleme, Pfad-Annahmen unter Windows.
4. KI-Pipeline: Sidecar-Aufrufe, VRAM-Budget (max 29 GB, nie alle Modelle
   gleichzeitig), QualityGate-Durchlauf, Dedup/Temporal-Voting, Fehlerpfade
   wenn Sidecar/Ollama nicht antwortet. KEINE Annahme zu nicht-implementierten
   Features (ByteTrack, 8B→32B-Eskalation etc.) — siehe CLAUDE.md.
5. Toter/verwaister Code: ungenutzte Klassen, tote Editoren, doppelte Helfer,
   auskommentierte Reste, Feature-Flags die nichts mehr schalten.
6. Tests: Lücken bei Parser/Import/Pipeline/KnowledgeBase/ViewModels/QualityGate;
   fehlende fokussierte Tests bei riskanter Logik; Test-Isolation (Testläufe
   dürfen NIE echte settings.json/Daten anfassen).
7. Performance: Hotpaths (Foto-Laden, Frame-Verarbeitung, Listen-Rendering),
   unnötige Allokationen/IO in Schleifen, synchrone IO im UI-Thread.
8. UI/UX-Konsistenz: uneinheitliche Styles/Brushes, Binding-Fehler (Bindings
   ohne passende ViewModel-Property), fehlende Empty-/Busy-/Fehlerzustände.
9. Wartbarkeit: Namensinkonsistenzen, tote Kommentare die dem Code widersprechen,
   Kommentare nicht auf Deutsch, fehlende JSON-Schemas bei Qwen-Outputs.

AUSGABE-FORMAT (genau so)
A. Executive Summary: 5–10 Zeilen, Gesamtnote + größte Hebel.
B. Befundliste, gruppiert nach den 9 Dimensionen. Pro Befund:
   - Titel (1 Satz)
   - Datei:Zeile(n) als Beleg
   - Warum es ein Problem ist (konkretes Fehlerszenario)
   - Schweregrad: Kritisch / Hoch / Mittel / Niedrig
   - Aufwand: S / M / L
   - Konkreter Fix-Vorschlag (1–3 Sätze)
C. Priorisierter Umsetzungsplan als Wellen:
   - Welle 1 (Quick Wins, S-Aufwand, hoher Nutzen)
   - Welle 2 (mittlere Struktur-/Konsistenzfixes)
   - Welle 3 (größere Umbauten, nur mit vorheriger Diskussion)
   Pro Punkt: Was, betroffene Dateien, Abhängigkeiten, Test-Strategie.
D. Explizit "BEWUSST NICHT anfassen": Dinge die riskant/Enterprise-Overhead
   sind oder laut CLAUDE.md/Fahrplan bewusst verworfen wurden.

HARTE REGELN (aus CLAUDE.md)
- Kein großes Refactoring ohne explizite Diskussion vorschlagen (nur planen).
- Thin-AI-Prinzip wahren: Geschäftslogik bleibt in C#, LLM nur Text.
- Laptop-/Workstation-Mode-Abstraktion nicht brechen.
- Keine NuGet-Pakete ohne Rückfrage.
- Bestehenden Code nur planen zu ändern, nichts jetzt umschreiben.
- Kommentare/Antworten auf Deutsch, JSON-Schema für alle Qwen-Outputs.

Beginne mit dem Lesen von CLAUDE.md und den docs-Fahrplänen, dann arbeite
die 9 Dimensionen ab. Liefere am Ende genau die Struktur A–D.
```
