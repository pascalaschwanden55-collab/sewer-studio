# Fehlerpruefung: Zentrale KI-Freigabe-Regel

- Datum: 11.07.2026
- Gepruefter Stand: `6c3977bbb`
- Ausgangsdokument: `docs/superpowers/specs/2026-07-10-zentrale-freigabe-regel-design.md`
- Ziel: Bugs und Korrektheit der Freigabe-Regel sowie ihrer Anzeige-, Uebernahme- und Lernwege
- Massstab: stabil im Alltag, keine falsche Sicherheitsanzeige, keine ungeprueften KI-Codes im Fachprotokoll

## Gesamturteil

**Die reine Entscheidungsregel ist inzwischen konservativ und deutlich besser als der alte Stand. Das Gesamtsystem ist aber noch nicht bereit, einen Befund als `verlaesslich` auszuweisen.**

Der Hauptgrund liegt nicht in den drei Schwellenwerten, sondern in den Daten davor und der Anzeige danach:

1. Der angeblich unabhaengige Datenbank-Beleg kann sich selbst bestaetigen.
2. Das zentrale Urteil wird vor der Vollprotokoll-Uebernahme nicht angezeigt.
3. An anderer Stelle wird der Schadensgrad weiterhin als KI-Sicherheit ausgegeben.

Damit ist der urspruengliche Fehler "falsches Gruen" in `DefectStatusPolicy` behoben, aber ueber andere Wege weiterhin moeglich.

## Gepruefter Umfang

Geprueft wurden:

- zentrale Regel und Grenzwerte
- Live-Codieren und Multi-Modell-Codieren
- Video-Vollanalyse und VSA-Code-Mapping
- Knowledge-Base-Abgleich
- Anzeige von Status, Ampel, Sicherheit und Begruendung
- Uebernahme in das Fachprotokoll
- Umwandlung in Trainingsdaten und KB-Indexierung
- Statistik und Nachvollziehbarkeit
- vorhandene Unit- und Integrationstests

Nicht praktisch ausgefuehrt wurden eine echte GPU-Videoanalyse und eine Messung gegen das reale Eval-Set. Diese Punkte benoetigen Modelle und Fachdaten. Alle nachfolgenden Fehler sind direkt am Code belegt.

## Kritisch 1: Der Datenbank-Beleg ist nicht unabhaengig

**Beleg**

- Die KB-Suche erhaelt bereits den aktuellen `VisionCode` als Suchhinweis: `ProtocolEntryFactory.BuildKnowledgeQuery`, `src/AuswertungPro.Next.Infrastructure/Ai/ProtocolEntryFactory.cs:69-98`.
- Dieselben KB-Treffer werden danach in den LLM-Prompt geschrieben: `ProtocolEntryFactory.BuildPrompt`, `src/AuswertungPro.Next.Infrastructure/Ai/ProtocolEntryFactory.cs:50-58`.
- Anschliessend gilt der KB-Beleg als bestaetigt, wenn der vom beeinflussten LLM ausgegebene Code dem ersten KB-Treffer entspricht: `FullProtocolGenerationService.MapDetectionAsync`, `src/AuswertungPro.Next.Infrastructure/Ai/FullProtocolGenerationService.cs:243-255`.
- Die aktuelle Haltung und der aktuelle Meterbereich stehen in der Suchanfrage. `CaseId` wird beim Bau von `KbExample` aber verworfen, deshalb kann die aktuelle Haltung nicht ausgeschlossen werden: `FullProtocolGenerationService.GetKnowledgeExamplesAsync`, `src/AuswertungPro.Next.Infrastructure/Ai/FullProtocolGenerationService.cs:306-325`.
- Es gibt keinen Mindestwert fuer die Aehnlichkeit. `RankAndFilter` liefert die besten vorhandenen Treffer auch bei sehr schwacher Aehnlichkeit: `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/RetrievalService.cs:79-147`.

**Folge**

Ein Vision-Hinweis kann die KB-Suche auf denselben Code lenken. Dieser Treffer lenkt das LLM auf denselben Code. Danach meldet der Vergleich eine "unabhaengige" Bestaetigung. Das ist ein Kreisschluss und kann zu falschem Gruen fuehren.

**Konkreter Fix**

1. Zwei getrennte KB-Wege einfuehren: Beispiele fuer die LLM-Hilfe und ein blinder Validierungsabruf nach der LLM-Antwort.
2. Beim Validierungsabruf weder `VsaCodeHint` noch vorgeschlagenen Code in die Suche aufnehmen.
3. `CaseId`, `HumanConfirmed`, `Corrected` und Roh-Aehnlichkeit bis zum Urteil mitfuehren.
4. Treffer aus derselben Haltung ausschliessen.
5. `KbAgreement=true` nur bei menschlich bestaetigtem Treffer, kalibriertem Mindest-Score und vorzugsweise Uebereinstimmung mehrerer Treffer setzen.
6. Das Validierungsbeispiel nie vor der Entscheidung in den LLM-Prompt schreiben.

**Pflichttests**

- Vision-Hinweis `BAB`, schwache KB-Treffer `BAB` -> kein AutoAccept.
- Treffer aus derselben `CaseId` -> kein KB-Beleg.
- Menschlich abgelehnter Treffer -> kein KB-Beleg.
- LLM-Code stimmt erst nach Few-Shot-Hinweis ueberein -> gilt nicht als unabhaengige Bestaetigung.
- Zwei bestaetigte, fremde Faelle mit ausreichendem Score -> KB-Beleg darf gelten.

## Kritisch 2: Das Urteil ist vor der Protokoll-Uebernahme unsichtbar

**Beleg**

- Die Vollanalyse erzeugt fuer jeden vorgeschlagenen Code sofort einen Protokolleintrag, unabhaengig von `AutoAccept`, `Review` oder `Reject`: `FullProtocolGenerationService.GenerateFromDetectionsAsync`, `src/AuswertungPro.Next.Infrastructure/Ai/FullProtocolGenerationService.cs:126-129`.
- Das Urteil wird nur als Text an `Warnings` angehaengt: `FullProtocolGenerationService.MitZentralerFreigabe`, `src/AuswertungPro.Next.Infrastructure/Ai/FullProtocolGenerationService.cs:148-159`.
- Das Analysefenster zeigt danach nicht `MappedEntries`, sondern die rohen `Detections`: `VideoAnalysisPipelineWindow`, `src/AuswertungPro.Next.UI/Views/Windows/VideoAnalysisPipelineWindow.xaml.cs:224-229`.
- Dadurch wird sogar die vorhandene Methode `DetectionItem.FromMapped` nicht benutzt. Der Ampelpunkt bleibt beim Standardwert Gelb: `src/AuswertungPro.Next.UI/Views/Windows/VideoAnalysisPipelineModels.cs:127-182`.
- Weder `result.Warnings` noch die Freigabe-Begruendung werden im Fenster gelesen.
- Der Knopf `Protokoll uebernehmen` prueft nur, ob irgendein Dokument vorhanden ist: `VideoAnalysisPipelineWindow.Accept_Click`, `src/AuswertungPro.Next.UI/Views/Windows/VideoAnalysisPipelineWindow.xaml.cs:776-785`.
- Danach wird das gesamte Protokoll ersetzt: `DataPageVideoAnalysisController.Open`, `src/AuswertungPro.Next.UI/DataPage/DataPageVideoAnalysisController.cs:89-114`.
- `AiFlags` werden zwar im ViewModel geladen, sind aber in keiner XAML-Review-Ansicht gebunden: `src/AuswertungPro.Next.UI/ViewModels/Protocol/ProtocolEntryVM.cs:274-284`.

**Folge**

Die neue Regel hat vor der Uebernahme praktisch keine Schutzwirkung. Der Nutzer sieht rohe Erkennungen und kann mit einem Klick auch rote oder ungepruefte Codes in das angezeigte Fachprotokoll uebernehmen.

Die Aussage im Revisionsblock der Design-Spec, der Hinweis sei "sichtbar in der Review", ist am aktuellen Code falsch.

**Konkreter Fix**

1. `MappedProtocolEntry` um ein strukturiertes `AiDecision` erweitern. Nicht nur einen Text in `Warnings` speichern.
2. Im Analysefenster `result.MappedEntries` mit `DetectionItem.FromMapped` anzeigen.
3. Pro Zeile Code, Ergebnis (`Verlaesslich`, `Pruefen`, `Ablehnen`) und konkreten Grund zeigen.
4. Zeilen einzeln auswaehlbar machen. `Review` und `Reject` duerfen nur nach ausdruecklicher Auswahl in das Protokoll.
5. Vor dem Ersetzen eine Zusammenfassung anzeigen: Anzahl verlaesslich, zu pruefen, abgelehnt und ausgeschlossen.
6. `ProtocolEntryAiMeta.Accepted` entsprechend der echten Nutzerentscheidung setzen.

**Pflichttests**

- Ein Ergebnis mit je einem AutoAccept-, Review- und Reject-Eintrag wird korrekt angezeigt.
- Ohne ausdrueckliche Auswahl landet kein Review-/Reject-Eintrag im neuen Protokoll.
- Der angezeigte Grund entspricht dem strukturierten Urteil.
- Fehler- und KB-Fallback-Pfade zeigen ebenfalls ein Urteil.
- Bestehende manuelle Eintraege bleiben wie bisher archiviert und wiederherstellbar.

## Kritisch 3: Schadensgrad wird als KI-Sicherheit ausgegeben

**Beleg**

- `LiveFrameFinding` besitzt nur `Severity`, aber kein Feld fuer Modell-Sicherheit: `src/AuswertungPro.Next.Application/Ai/LiveDetectionModels.cs:10-24`.
- Trotzdem wird `Severity / 5.0` als `QwenVisionConf` in das QualityGate gegeben: `CodingLiveFindingQualityGatePolicy.BuildEvidence`, `src/AuswertungPro.Next.UI/Ai/CodingLiveFindingQualityGatePolicy.cs:9-22`.
- In der Player-Anzeige wird `Severity * 20` als `ConfidencePercent` berechnet und gruen eingefarbt: `src/AuswertungPro.Next.UI/Views/Windows/AiFindingDisplayItem.cs:55-77`.
- Dieser Wert wird sichtbar als Confidence ausgegeben: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml:683-687`.
- Auch `RawVideoDetection.Confidence` leitet die Sicherheit nur aus dem Text `high/mid/low` ab: `src/AuswertungPro.Next.Application/Ai/VideoPipelineContracts.cs:95-121`.

**Folge**

Ein schwerer Schaden (`Severity=5`) erscheint als gruene `100%`-Sicherheit, auch wenn das Modell unsicher war. Schadensschwere und Erkennungssicherheit sind fachlich verschiedene Werte. Diese Anzeige kann den Kanalinspekteur direkt irrefuehren.

**Konkreter Fix**

1. `LiveFrameFinding` um echte, nullable Modell-Sicherheiten erweitern, inklusive Herkunft des Werts.
2. Ohne echten Wert `Sicherheit nicht verfuegbar` anzeigen, niemals einen Ersatzwert erfinden.
3. `Severity` separat als `Schadensgrad 1-5` darstellen.
4. `QwenVisionConf` nur mit einem echten Qwen-Wert fuellen; sonst `null`.
5. `RawVideoDetection.Confidence` entfernen oder auf einen echten Pipeline-Wert umstellen.

**Pflichttests**

- `Severity=5`, keine Confidence -> Anzeige `nicht verfuegbar`, kein gruener Prozentwert.
- `Severity=1`, echte Confidence 0.95 -> Schadensgrad 1 und Sicherheit 95% bleiben getrennt.
- Ein fehlender Modellwert zaehlt im QualityGate nicht als Beleg.

## Wichtig 1: Drei Ergebnisse werden auf zwei reduziert

**Beleg**

- `AutoApprovalService` bildet `Review` und `Reject` beide auf `AutoApprovalResult.Rejected` ab: `src/AuswertungPro.Next.Infrastructure/Ai/SelfImproving/AutoApprovalService.cs:16-27`.
- `AlsHinweis` nennt beide nur `pruefen`: `AutoApprovalService.cs:35-44`.
- `DefectStatusPolicy` uebernimmt zwar die drei Zonen, verwirft aber die Begruendung der zentralen Regel: `src/AuswertungPro.Next.Application/Ai/DefectStatusPolicy.cs:45-58`.

**Folge**

Ein gelber Grenzfall und ein harter roter Datenfehler sind in der Vollanalyse nicht unterscheidbar. Die zugesagte Erklaerbarkeit geht verloren.

**Fix und Test**

- `AutoApprovalResult` durch ein Ergebnis mit `Outcome`, stabilem `ReasonCode` und Text ersetzen.
- Ergebnis unveraendert bis UI und Protokollmetadaten durchreichen.
- Integrationstest fuer alle drei Ausgaenge vom Policy-Aufruf bis zur Anzeige.

## Wichtig 2: QualityGate zaehlt gleiche Quellen mehrfach

**Beleg**

- Im Vollanalyse-Pfad werden `LlmCodeConf` und `PlausibilityScore` mit demselben Wert `confidence` bzw. `checked_.Confidence` gefuellt: `src/AuswertungPro.Next.Infrastructure/Ai/FullProtocolGenerationService.cs:248-256`.
- `QualityGateService` zaehlt Felder, nicht unabhaengige Quellen. Zwei Felder reichen fuer Gruen: `src/AuswertungPro.Next.Infrastructure/Ai/QualityGate/QualityGateService.cs:18-24` und `48-87`.
- KB-Aehnlichkeit und KB-Code-Uebereinstimmung stecken bereits im Composite. Danach verlangt die zentrale Regel `KbAgreement` nochmals: `QualityGateService.cs:53-57` und `AiDecisionPolicy.cs:72-78`.

**Folge**

Die Anzahl der Belege wirkt groesser als sie ist. Ein Signal kann ueber mehrere Felder mehrfach Gewicht bekommen.

**Fix und Test**

- Jeder Beleg braucht eine Quellenfamilie, zum Beispiel Vision, Segmentierung, Textmodell, KB und Plausibilitaetsregel.
- `MinSignalsForGreen` muss unterschiedliche Quellenfamilien zaehlen.
- Ein einzelner Wert, der in zwei Feldern steht, darf nur einmal zaehlen.
- KB entweder im Composite oder als gesonderte Pflichtbedingung werten, nicht beides ungeprueft.

## Wichtig 3: Manuelle Befunde sehen wie perfekte KI-Treffer aus

**Beleg**

- Ein manueller Protokolleintrag erhaelt trotzdem einen `CodingEventAiContext` mit `SuggestedCode` und `Confidence=1.0`: `src/AuswertungPro.Next.UI/Ai/CodingManualEventFactory.cs:19-40`.
- Der Trainingsmapper behandelt jedes Event mit `AiContext` als KI-Fall, setzt `KiCode` und bei Annahme `ExactMatch`: `src/AuswertungPro.Next.Application/Ai/Training/CodingEventToSampleMapper.cs:35-67` und `119-128`.
- Bei Annahme wird daraus ein `Approved`-Sample mit `HumanConfirmed=true`: `CodingEventToSampleMapper.cs:77-92`.

**Folge**

Eine reine Eingabe des Kanalinspekteurs wird in Statistik und Lerndaten als 100-prozentiger KI-Volltreffer gespeichert. Der fachliche Code kann korrekt sein, aber Herkunft und KI-Qualitaetsmessung werden verfaultscht.

**Fix und Test**

- Manuellen Events keinen `AiContext` geben. Fuer die Bestaetigungs-UI einen getrennten `ReviewContext` verwenden.
- Herkunft explizit speichern: `Manual`, `AiSuggestion`, `AiCorrected`, `Imported`.
- Bei manuellen Samples `KiCode=null` und kein KI-MatchLevel setzen.
- Test: angenommener manueller Befund bleibt gutes Gold-Label, zaehlt aber nicht als KI-Treffer.

## Wichtig 4: Ein zweiter Abschlussweg umgeht die Freigabe

**Beleg**

- Der sichtbare Apply-Weg filtert korrekt und verlangt bei KI-Events `Accepted` oder `AcceptedWithEdit`: `src/AuswertungPro.Next.UI/Ai/CodingEventProtocolApplyPolicy.cs:9-28`.
- `CodingSessionService.CompleteSession` und `CompleteSessionAsync` schreiben dagegen alle Events in das Protokoll, auch `Ignored` und `Rejected`: `src/AuswertungPro.Next.Infrastructure/Ai/CodingSessionService.cs:125-152` und `169-196`.
- Derselbe Dienst persistiert danach alle Events als Trainingssamples: `CodingSessionService.cs:216-240`.
- Die Schutzregel liegt im UI-Projekt und kann vom Infrastructure-Dienst nicht wiederverwendet werden.

**Folge**

Die zentrale Invariante gilt nicht an der eigentlichen Fachgrenze. Der aktuelle XAML-Weg scheint den Abschlussbefehl nicht zu binden; der oeffentliche Service- und ViewModel-Weg bleibt aber fehlerhaft und kann spaeter unbemerkt aktiviert werden.

**Fix und Test**

- `CodingEventProtocolApplyPolicy` in die Application-Schicht verschieben.
- Jeden Protokoll-Bauweg durch dieselbe Regel schicken.
- Test mit Accepted, Ignored und Rejected: Nur Accepted erscheint im Fachprotokoll.
- Negative Trainingsbeispiele duerfen separat gespeichert werden, aber nicht als positive KB-Eintraege.

## Wichtig 5: Die Statistik nennt manuelle Entscheidungen "Auto-Akzeptiert"

**Beleg**

- `CodingStatisticsPolicy` zaehlt `Accepted` und `AcceptedWithEdit` zu `AutoAccepted`: `src/AuswertungPro.Next.UI/Ai/CodingStatisticsPolicy.cs:41-47`.
- Die Kachel heisst sichtbar `Auto-Akzeptiert`: `src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanel.xaml:498-504`.
- `CodingSessionViewModel.RefreshStatistics` verwendet nochmals eine andere Regel und laesst `AcceptedWithEdit` weg: `src/AuswertungPro.Next.UI/ViewModels/Windows/CodingSessionViewModel.cs:577-586`.
- Der bestehende Test erwartet die falsche Zusammenzaehlung: `tests/AuswertungPro.Next.UI.Tests/CodingStatisticsPolicyTests.cs:24-46`.

**Folge**

Die Anzeige kann den Erfolg der KI deutlich uebertreiben. Zwei Ansichten koennen fuer dieselbe Session unterschiedliche Zahlen zeigen.

**Fix und Test**

- Eine einzige Statistikquelle verwenden.
- Getrennte Werte: `KI als verlaesslich eingestuft`, `menschlich bestaetigt`, `korrigiert`, `abgelehnt`, `offen`.
- Durchschnitt nur aus echten, endlichen KI-Sicherheiten bilden.
- Manuellen `Confidence=1.0`-Ersatzwert vollstaendig ausschliessen.

## Wichtig 6: Die Unsicherheit ist kein unabhaengiger Mehrfach-Test

**Beleg**

- Die Vollanalyse erzeugt `UncertaintyEstimate.FromSinglePass(compositeConfidence)`: `src/AuswertungPro.Next.Infrastructure/Ai/FullProtocolGenerationService.cs:258-271`.
- `FromSinglePass` berechnet die Unsicherheit nur mathematisch aus derselben Confidence: `src/AuswertungPro.Next.Application/Ai/QualityGate/UncertaintyEstimate.cs:23-34`.
- Bei Confidence 0.92 entsteht Unsicherheit 0.16. Wegen `>= 0.15` liegt die tatsaechliche Freigabegrenze bei mehr als rund 0.925, nicht bei der dokumentierten 0.92.
- Negative Unsicherheit wird in `StandardAiDecisionPolicy` nicht als ungueltig erkannt und kann AutoAccept passieren: `src/AuswertungPro.Next.Application/Ai/AiDecisionPolicy.cs:55-80`.
- Die Design-Spec bestaetigt selbst, dass die Schwellen nicht am Eval-Set kalibriert sind.

**Folge**

Die Unsicherheit sieht wie ein weiterer Beleg aus, ist aber nur eine zweite Darstellung desselben Werts. Die dokumentierte Grenze entspricht nicht dem echten Verhalten.

**Fix und Test**

- Ohne echten Ensemble-/Monte-Carlo-Lauf `EpistemicUncertainty=null` setzen.
- Wertebereich 0 bis 1 auch fuer Unsicherheit erzwingen.
- Exakte Grenztests fuer 0.60, 0.92 und 0.15 ergaenzen.
- Schwellen erst nach Precision-/Recall-Messung am bereinigten Eval-Set als `verlaesslich` bezeichnen.

## Wichtig 7: KB-Sicherheit haengt von perfekter Aufrufer-Disziplin ab

**Beleg**

- `KnowledgeBaseManager.IsIndexWorthy` prueft Code, Beschreibung und Plausibilitaet, aber weder `Status=Approved` noch `HumanConfirmed`: `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs:408-433`.
- `RebuildAsync` kann deshalb jedes fachlich plausible Sample wieder indexieren, wenn der Aufrufer auch abgelehnte Samples uebergibt: `KnowledgeBaseManager.cs:175-205` und `238-254`.
- `RetrievalService` liest `HumanConfirmed` und `Corrected`, filtert diese Werte beim Ranking aber nicht: `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/RetrievalService.cs:254-295` und `79-147`.

**Folge**

Der aktuelle Review-Ablehnungsweg deindexiert korrekt. Ein spaeterer Rebuild oder ein anderer Aufrufer kann abgelehnte bzw. nie bestaetigte Samples jedoch wieder zu einem positiven Freigabe-Beleg machen.

**Fix und Test**

- Positive Freigabe-KB nur aus `Approved` und `HumanConfirmed=true` aufbauen.
- Unbestaetigte und abgelehnte Beispiele in getrennten Bestaenden halten.
- Defense-in-Depth sowohl beim Schreiben als auch beim Abruf pruefen.
- Rebuild-Test mit Approved, New und Rejected: Nur bestaetigtes Gold wird indexiert.

## Nice-to-have 1: Farben und Begriffe widersprechen der Regel

- Confidence wird schon ab 0.85 gruen, obwohl die zentrale Regel mindestens 0.92 plus KB-Beleg verlangt: `CodingSessionViewModel.GetConfidenceBrush`, `src/AuswertungPro.Next.UI/ViewModels/Windows/CodingSessionViewModel.cs:605-612`.
- `Pending` und `ReviewRequired` erhalten beim Zonenpunkt beide Grau, obwohl die Texte Yellow/Red sagen: `src/AuswertungPro.Next.UI/Ai/CodingDefectStatusDisplayPolicy.cs:19-38`.
- `Auto-Akzeptiert` ist missverstaendlich, weil laut Design nichts automatisch uebernommen wird.

**Fix:** Prozentfarbe neutral halten oder nur die echte zentrale Zone verwenden. Text besser: `KI als verlaesslich eingestuft`, `Pruefen`, `Nicht verwenden`.

## Nice-to-have 2: Urteil ist spaeter nicht reproduzierbar

`AiDecision` besitzt nur freien Text. In `CodingEventAiContext` und `ProtocolEntryAiMeta` fehlen Policy-Version, stabiler Grundcode, Modellversion und verwendete Schwellen. Nach einer Regel- oder Modell-Aenderung kann ein altes Urteil nicht exakt erklaert werden.

**Fix:** `PolicyVersion`, `ReasonCode`, Modell-/Gate-Version und Signal-Snapshot speichern. Freitext nur fuer die Anzeige verwenden.

## Nice-to-have 3: Zentrale Policy ist nicht wirklich austauschbar

`DefectStatusPolicy` greift direkt auf `StandardAiDecisionPolicy.Default` zu. `FullProtocolGenerationService` erzeugt `AutoApprovalService` direkt als Feld. Dadurch sind Konfiguration, Versionierung und Integrationstests unnoetig schwer.

**Fix:** `IAiDecisionPolicy` ueber Konstruktoren einspeisen und an einer Stelle registrieren. Defaults nur am Composition Root setzen.

## Was bereits gut ist

- Fehlendes oder nicht-gruenes QualityGate fuehrt nicht mehr zu AutoAccept.
- Ein KB-Beleg ist fuer AutoAccept inzwischen zwingend.
- NaN, unendliche und ausserhalb 0 bis 1 liegende Confidence werden abgefangen.
- Benutzerentscheidungen schlagen das KI-Urteil.
- Der sichtbare Coding-Apply-Weg verlangt eine ausdrueckliche Freigabe.
- Auch die Fehlerpfade der Vollanalyse erhalten seit `6c3977bbb` ein zentrales Urteil.
- Build und vorhandene Tests sind sauber.

## Empfohlene Reihenfolge

### Stufe 1: Falsches Vertrauen stoppen

1. Schadensgrad und Confidence trennen.
2. Strukturiertes Urteil in `MappedProtocolEntry` speichern und im Vollanalyse-Fenster anzeigen.
3. Review-/Reject-Zeilen nur nach ausdruecklicher Auswahl uebernehmen.
4. KB-Validierung vom LLM-Prompt trennen und Same-Case/Low-Score blockieren.

### Stufe 2: Daten und Statistik bereinigen

5. Manuelle Events ohne falschen KI-Kontext speichern.
6. Protokoll-Freigaberegel in die Application-Schicht verschieben und in allen Abschlusswegen nutzen.
7. Statistik in eine Quelle zusammenfuehren und Bezeichnungen korrigieren.
8. KB-Indexierung auf menschlich bestaetigtes Gold begrenzen.

### Stufe 3: Messbar und nachvollziehbar machen

9. Single-Pass-Pseudo-Unsicherheit aus der Freigabe entfernen.
10. ReasonCode, Policy-Version und Signal-Snapshot persistieren.
11. Eval-Set bereinigen und Schwellen kalibrieren.
12. Erst danach den Begriff `verlaesslich` fachlich freigeben.

## Verifikation des aktuellen Stands

Ausgefuehrt am 11.07.2026:

- `dotnet build AuswertungPro.sln --nologo --verbosity minimal`
  - 0 Fehler
  - 0 Warnungen
- gezielte Policy-/Gate-Tests
  - 46 von 46 bestanden
- gezielte UI-/Adapter-Tests
  - 23 von 23 bestanden
- `dotnet test AuswertungPro.sln --no-build --nologo --verbosity minimal`
  - 8.217 bestanden
  - 1 bewusst uebersprungen
  - 0 fehlgeschlagen

Die gruene Testsuite widerspricht den Findings nicht: Die kritischen Fehler liegen vor allem zwischen den getrennt getesteten Bausteinen. Genau fuer diese Uebergaenge fehlen End-to-End-Tests.

## Abnahmekriterien fuer den Fix

Die zentrale Freigabe ist erst abgeschlossen, wenn alle Punkte erfuellt sind:

- Kein Severity-Wert wird als Confidence angezeigt oder gewertet.
- Jeder Vollanalyse-Eintrag zeigt vor der Uebernahme Outcome und Grund.
- Kein Review-/Reject-Eintrag gelangt ohne ausdrueckliche Auswahl in das Fachprotokoll.
- KB-Bestaetigung ist blind, fallfremd, menschlich bestaetigt und ueber Mindest-Score abgesichert.
- Manuelle Befunde werden nicht als KI-Volltreffer gezaehlt.
- Alle Protokoll-Bauwege verwenden dieselbe Freigabe-Invariante.
- Drei Policy-Ausgaenge bleiben bis UI und Persistenz erhalten.
- Schwellen und Unsicherheit sind mit Grenztests und Eval-Messung belegt.
