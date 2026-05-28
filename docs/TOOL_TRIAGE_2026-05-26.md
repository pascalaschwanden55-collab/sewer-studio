# Tool-Triage 2026-05-26

Ziel: echte Werkzeuge sichern, lokale Auswertungsordner nicht versehentlich committen.

## Wiederherstellen

Diese Ordner hatten echten Quellcode in der Git-Historie und wurden wiederhergestellt:

| Ordner | Status |
| --- | --- |
| `tools/CadasterDbReader` | wiederhergestellt, Build gruen |
| `tools/FachwissenIndexer` | wiederhergestellt, Build gruen |
| `tools/kb_audit` | wiederhergestellt, Python-Hilfsskripte |
| `tools/SewerStudio.AiTestRunner` | wiederhergestellt, an aktuellen Sidecar-Client angepasst, Build gruen |
| `tools/SewerStudioMcpServer` | wiederhergestellt, an aktuellen PDF-Extractor angepasst, Build gruen |
| `tools/StammdatenExporter` | wiederhergestellt, benoetigte Import-Hilfsklassen wiederhergestellt, Build gruen |

## Nicht Committen

Diese Ordner sind lokale Auswertungen, Berichte, Modelle oder Zwischenresultate. Sie bleiben vorerst unberuehrt:

| Ordner | Grund |
| --- | --- |
| `tools/CrossValidationReport` | Ergebnisdaten |
| `tools/Db3PilotReader` | Pilot-Ausgaben |
| `tools/FrameMultiExtractor` | Manifest-/Frame-Ausgaben |
| `tools/GeminiPhotoCheck` | Review-Ausgaben |
| `tools/HaltungTopologyExtractor` | Topologie-Ausgaben |
| `tools/HoldingDistributionSmokeCheck` | Smoke-Test-Ausgaben |
| `tools/ImageQualityAudit` | Audit-Ausgaben |
| `tools/PdfPhotoCoverageProbe` | Berichtsausgaben |
| `tools/PdfPhotoLabelReview` | Review-Ausgaben |
| `tools/ProtocolPipelineDiagnostics` | Diagnose-Ausgaben |
| `tools/SewerStudioTrainingBatch` | Trainingslauf-Ausgaben |
| `tools/TrainingSourceReport` | Berichtsausgaben |
| `tools/VideoprojekteInventory` | Inventar-Ausgabe |
| `tools/XtfPilotReader` | Pilot-Ausgaben |
| `tools/__pycache__` | Python-Zwischenspeicher |

## Loeschen

Aktuell nichts geloescht.

Loeschungen nur nach separater Freigabe. Das ist Absicht, weil mehrere Ordner lokale Pruefresultate enthalten koennen.
