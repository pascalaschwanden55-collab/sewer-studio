# Gesamtaudit SewerStudio – 27.09.2026

**SewerStudio baut erfolgreich, ist aber derzeit nicht bereit für eine ungeprüfte Freigabe.** Der vollständige Release-Build hatte null Fehler und null Warnungen. Die UI-Tests meldeten 13 Fehler. Zusätzlich bestehen sechs bekannte Sicherheitslücken in festgelegten Python-Paketen.

Geprüft wurde der damalige Arbeitsstand einschließlich der bereits vorhandenen Änderungen. Beim Audit wurden kein Produktcode und keine Kundendaten verändert. Dieser Bericht hält die Ergebnisse der Codex-Sitzung „SewerStudio vollständig auditieren“ vom 27.09.2026 fest; die Prüfungen wurden für diese Datei nicht erneut ausgeführt.

## Befunde nach Priorität

| Priorität | Befund | Beleg und nächster Schritt |
|---|---|---|
| **Hoch** | **Die UI-Tests sind rot: 13 Fehler bei 7’492 Tests.** Mehrere Tests suchen nach früheren Quelltextstellen. Der neue Ablauf übergibt Bild und Meter inzwischen gemeinsam. Das erklärt einzelne Fehlschläge, belegt aber noch nicht, dass alle 13 harmlos sind. | [UI-Architekturtest](../../tests/AuswertungPro.Next.UI.Tests/PlayerWindowCodingMultiModelArchitectureTests.cs), [Test zur Bildbindung](../../tests/AuswertungPro.Next.UI.Tests/DesignAuditPlayerCodingSidePanelTests.cs), [aktueller Aufrufweg](../../src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Ai.MultiModel.cs). Jeden Fehler gegen das heutige Verhalten prüfen und die Tests gezielt anpassen. |
| **Hoch** | **Sechs bekannte Python-Paketlücken bleiben offen.** Die Paketprüfung fand keine neue Lücke, akzeptiert aber fünf Funde in `transformers` und einen in `setuptools` als dokumentierte Ausnahmen. | [Ausnahmen mit Gründen](../../sidecar/security/lock_audit_exceptions.json). Den Ersatz des derzeitigen Grounding-DINO-Paketwegs und die Paketverträglichkeit gesondert prüfen; die Ausnahmen nicht einfach entfernen. |
| **Mittel** | **WebGIS hat ein verbleibendes Zeitfenster für Fremdänderungen.** Der Client vergleicht den zuletzt gelesenen Stand vor `saveData`. Eine Änderung nach diesem Lesen kann er ohne serverseitige Versionsprüfung nicht sicher erkennen. Der Code benennt diese Grenze selbst. | [WebGIS-Schreibweg](../../src/AuswertungPro.Next.Infrastructure/WebGis/GeonisWebGisClient.cs). Mit dem WebGIS-Anbieter klären, ob eine serverseitige Versionsprüfung möglich ist. |
| **Mittel** | **Drei Modellpakete bleiben außerhalb der automatischen Sicherheitsprüfung:** die lokalen CUDA-Versionen von `torch` und `torchvision` sowie der Git-Stand von SAM 2. | [Paketprüfung](../../sidecar/security/audit_lock.py), [dokumentierte Grenze](../../sidecar/security/lock_audit_exceptions.json). Für diese drei Stände eine eigene, nachvollziehbare Prüfung pflegen. |
| **Mittel** | **Eine Wartbarkeitsprüfung ist rot.** Zwei Produktionsdateien überschreiten die festgelegte Grenze von 1’000 Zeilen: `AnnotationWorkbenchService.cs` mit 1’034 und `MultiModelAnalysisService.cs` mit 1’016 Zeilen. Die Größe allein beweist keinen Funktionsfehler. | [Prüfregel](../../tests/AuswertungPro.Next.UI.Tests/MaintainabilityFitnessTests.cs). Verantwortlichkeiten beim nächsten fachlichen Eingriff gezielt trennen. |
| **Offene Fachprüfung** | **Die Zuordnung von Breite und Höhe einer Haltung zum WebGIS ist laut Projektbeschreibung noch nicht abschließend geklärt.** | [Dokumentierter offener Punkt](../../CLAUDE.md). An einem geeigneten Eiprofil mit echten Feldwerten abgleichen, bevor diese Zuordnung erweitert wird. |

## Ergebnis der Prüfungen

| Prüfung | Ergebnis |
|---|---:|
| Vollständiger Release-Build | **Bestanden**, 0 Fehler, 0 Warnungen |
| Infrastrukturtests | **7’498 bestanden**, 6 übersprungen |
| KI-Pipeline-Tests | **2’853 bestanden**, 3 übersprungen |
| UI-Tests | **7’446 bestanden, 13 fehlgeschlagen**, 33 übersprungen |
| ProjectModernizer-Tests | **62 bestanden** |
| Python-Sidecar ohne GPU | **577 bestanden**, 2 abgewählt |
| QGIS-Tests | **14 bestanden** |
| Prüfung der Python-Sperrdatei | **88 Pakete geprüft**; 6 bekannte Ausnahmen, 3 nicht automatisch prüfbare Pakete |

**Gesamtbewertung:** Die geprüften Import-, Sicherungs-, Pipeline- und Werkzeugtests sind grün. Der wichtigste nächste Schritt ist die Klärung der 13 UI-Fehler. Danach sollten die bekannten Paketlücken und die WebGIS-Grenze für die Freigabe ausdrücklich bewertet werden.

Dieses Audit umfasst Build, automatisierte Tests, Paketprüfung und gezielte Prüfung kritischer Aufrufwege. Es ist keine vollständige Bedienabnahme mit Kundenprojekt, echtem WebGIS, GPU-Modellen oder Wiederherstellung einer Sicherung. Aus den grünen Tests allein lässt sich deren Verhalten nicht bestätigen.
