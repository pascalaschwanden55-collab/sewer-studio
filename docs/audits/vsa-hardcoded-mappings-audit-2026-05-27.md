# VSA-KEK Hardcoded Mapping Audit 2026-05-27

Quelle der Pruefung: `src/AuswertungPro.Next.UI/Data/vsa_kek_2020_catalog_manifest.json`.

Wichtiger Befund: Das aktive Manifest fuehrt `BBA` als Wurzeln/Bewuchs-Gruppe und `BBB` als Anhaftende-Stoffe/Inkrustation-Gruppe. Tests oder Fixes in die Gegenrichtung waeren eine Regression gegen die aktuelle Quelle der Wahrheit.

| Stelle | Mapping | Manifest-Abgleich | Aktion |
| --- | --- | --- | --- |
| `src/AuswertungPro.Next.Infrastructure/Ai/VsaCodeResolver.cs` | `wurzel/root/bewuchs -> BBA`, `inkrustation/kalk/anhaftung -> BBB` | korrekt | keine |
| `src/AuswertungPro.Next.Infrastructure/Ai/EnhancedVisionAnalysisService.cs` | Prompt wird aus Katalog gerendert, Fallback nennt `BBA = Wurzeln`, `BBB = Anhaftende Stoffe` | korrekt | keine |
| `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs` | Eingabemarker: `VERFORMUNG -> BAA`, `OBERFLAECHENSCHADEN -> BAF`, `WURZELN -> BBB`, `INKRUSTATION -> BBA` | falsch | korrigiert auf `BAF`, `BAJ`, `BBA`, `BBB` |
| `src/AuswertungPro.Next.Application/Reports/ProtocolPdfExporter.cs` | Symbolmapping: `BAJ -> roots`, `BBA -> incrustation`, `BBB -> obstacle` | falsch | korrigiert auf `BAJ -> default`, `BBA -> roots`, `BBB -> incrustation` |
| `src/AuswertungPro.Next.Infrastructure/Ai/Sanierung/AiSanierungOptimizationService.cs` | Prompt: Einsturz als `BBB-Codes` | falsch | korrigiert auf `BAC-Codes` |
| `src/AuswertungPro.Next.Infrastructure/Ai/Sanierung/SanierungValidationService.cs` | Kommentar: `BBB` als eindringender Boden | falsch | Kommentar korrigiert; Verhalten war bereits `BAC/BABC` |
| `src/AuswertungPro.Next.Infrastructure/Ai/Teacher/VsaYoloClassMap.cs` | Kommentare fuer `BBA`/`BBB` vertauscht | falsch | nur Kommentare korrigiert; Klassen-IDs nicht getauscht |
| `src/AuswertungPro.Next.Application/Ai/Evaluation/EvalSetBenchmark.cs` | Router-Gruppen: `BBA -> wurzeln`, `BBB/BBC -> ablagerung` | akzeptable Grobklasse | keine |
| `src/AuswertungPro.Next.Infrastructure/Ai/Training/FewShotExampleBuilder.cs` | High-Value-Prefix-Liste ohne Bedeutungen | keine fachliche Mappingquelle | keine |

Nicht geaendert: YOLO-Klassen-IDs. Ein Tausch der IDs waere eine Daten-/Modellmigration und gehoert nicht in diesen Audit-Fix.
