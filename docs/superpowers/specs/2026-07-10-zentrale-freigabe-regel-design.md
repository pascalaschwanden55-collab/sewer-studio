# Design-Spec: Zentrale KI-Freigabe-Regel (Audit Fix 3)

- **Datum:** 2026-07-10
- **Branch:** feature/gis-karte
- **Bezug:** Programm-Audit PR #23, Befund P0-3; folgt auf Commit 1f96fb99 (Fix 1/2/4)
- **Status:** Design zur Freigabe

## 1. Problem

Heute existieren zwei sich widersprechende Freigabe-Regeln fuer denselben Zweck
(„ist ein KI-Befund verlaesslich genug?"):

| Regel | Ort | Kriterium | genutzt? |
|-------|-----|-----------|----------|
| `DefectStatusPolicy.GetStatus` | Application | nur Confidence ≥ 0.85 | ja (Live-Anzeige) |
| `AutoApprovalService.Evaluate` | Infrastructure | Confidence ≥ 0.92 **und** QualityGate Green **und** KB-Übereinstimmung **und** niedrige Unsicherheit | nein (nur Tests) |

Folge: Ein Befund wird dem Operateur beim Codieren gruen als „Auto-Akzeptiert"
angezeigt, sobald die reine KI-Sicherheit ≥ 0.85 ist — **auch wenn das QualityGate
ihn gar nicht bestaetigt**. Die strenge, durchdachte Regel liegt ungenutzt daneben.
Beim Codieren (Stufe 2) verleitet die falsche gruene Markierung zum Ueberspringen.

## 2. Ziel & Umfang

**Eine** zentrale Regel, die beide ersetzt und fuer alle Freigabe-Wege **ohne
bekanntes Vergleichsprotokoll** gilt: Live-Codieren und Video-Vollanalyse.

**Nicht im Umfang (YAGNI):**
- Das naechtliche Selbsttraining. Es entscheidet gegen ein bekanntes Protokoll
  (`MatchLevel`) — ein anderer Kontext. `SelfTrainingAutoAcceptPolicy` und
  `SelfTrainingReviewRouting` bleiben unberuehrt.
- Echtes automatisches Uebernehmen ins Protokoll. Die KI uebernimmt weiterhin
  nichts von selbst; die Regel liefert nur eine verlaessliche Einstufung
  (Anzeige/Statistik). Die Architektur laesst echtes Durchwinken spaeter zu.
- Keine neue Einstell-UI fuer Schwellenwerte (Konstanten, spaeter konfigurierbar).

## 3. Verhalten der Regel

Drei Ausgaenge: **AutoAccept** (gruen, verlaesslich) / **Review** (gelb, pruefen)
/ **Reject** (rot, unbedingt pruefen).

Leitprinzip: **kontextabhaengig streng** — alle im jeweiligen Kontext *vorhandenen*
Belege muessen passen, und es muessen **mindestens zwei unabhaengige** Belege
vorhanden sein. Ein einzelnes Signal reicht nie fuer AutoAccept.

Vier moegliche Belege (Signale):
1. KI-Sicherheit (Confidence)
2. QualityGate-Ampel (Green/Yellow/Red)
3. Datenbank-Abgleich (KB-Übereinstimmung, nur Video-Pfad vorhanden)
4. Mehrfach-Pruef-Unsicherheit (Epistemic Uncertainty, nur Video-Pfad vorhanden)

**AutoAccept** nur wenn ALLE zutreffen:
- Confidence ≥ 0.92
- QualityGate ist vorhanden **und** Green
- falls KB-Übereinstimmung vorhanden: muss `true` sein
- falls Unsicherheit vorhanden: muss < 0.15 sein
- mindestens 2 unabhaengige Belege vorhanden

**Reject** wenn: Confidence < 0.60 **oder** QualityGate == Red.

**Review**: alles dazwischen (inkl. „Signale fehlen, Kriterien nicht alle erfuellt").

### Konsequenz je Kontext
- **Live-Codieren** (Belege: Confidence + Ampel): gruen, wenn Ampel=Green **und**
  Confidence ≥ 0.92. Hohe Sicherheit bei roter/gelber Ampel → **nicht** mehr gruen.
  Das behebt die falsche Markierung.
- **Video-Vollanalyse** (zusaetzlich KB + Unsicherheit): gruen nur mit voller Kette;
  KB-Widerspruch oder hohe Unsicherheit → Review, trotz hoher Confidence.

## 4. Architektur

Neue, reine Logik in `AuswertungPro.Next.Application/Ai` (kein I/O, wie
`DefectStatusPolicy`):

- `AiDecisionSignals` (record): `double Confidence`, `TrafficLight? QualityGate`,
  `bool? KbAgreement`, `double? EpistemicUncertainty`. `null` = Signal fehlt.
- `AiDecisionOutcome` (enum): `AutoAccept`, `Review`, `Reject`.
- `AiDecision` (record): `AiDecisionOutcome Outcome`, `string Reason`.
- `IAiDecisionPolicy` (interface): `AiDecision Decide(AiDecisionSignals signals)`.
- `StandardAiDecisionPolicy` (implementiert das Interface): die Regel aus §3,
  Schwellen als benannte Konstanten. Zustandslos; eine `Default`-Instanz fuer die
  statischen Aufrufer.

**Umstellung der zwei bestehenden Regeln — beide delegieren an die zentrale Regel:**

- `DefectStatusPolicy.GetStatus`: Nutzer-Entscheidungen (Accepted/Rejected/…)
  bleiben unveraendert. Fuer noch-nicht-entschiedene Events (Decision=Ignored)
  baut es `AiDecisionSignals` aus dem `AiContext` (Confidence + geparstes
  `QualityGateLevel`) und ruft die Policy. Mapping: AutoAccept→`AutoAccepted`,
  Review→`Pending`, Reject→`ReviewRequired`.
- `AutoApprovalService.Evaluate`: wird ein duenner Adapter — baut `AiDecisionSignals`
  aus dem `MappedProtocolEntry` (Confidence, QualityGateResult.TrafficLight,
  Evidence.KbCodeAgreement, Uncertainty.EpistemicUncertainty) und ruft die Policy.
  `IsApproved = (Outcome == AutoAccept)`. Bestehende API und Testfaelle bleiben gueltig.

## 5. Notwendiger Zusatz: Ampel beim Anlegen speichern

Damit die Live-Anzeige die Ampel schon bei Decision=Ignored kennt (heute wird
`CodingEventAiContext.QualityGateLevel` erst bei Bestaetigung gesetzt), schreiben
die KI-Event-Factories das QualityGate-Ergebnis beim Anlegen in den `AiContext`.
Das Ergebnis wird ohnehin via `CodingMultiModelQualityGatePolicy` ausgewertet;
es muss nur durchgereicht werden. Betroffen: die Factories, die KI-Befunde mit
QualityGate erzeugen (v.a. `CodingMultiModelEventFactory`, `CodingLiveFindingEventFactory`).
Ohne diesen Schritt bliebe im Live-Codieren nur 1 Beleg (Confidence) → nie gruen.

## 6. Testplan

- `StandardAiDecisionPolicy`: alle Faelle — Live 2-Belege gruen; Live rote/gelbe
  Ampel trotz hoher Confidence nicht gruen; Video volle Kette gruen; Video
  KB-Widerspruch → Review; Video hohe Unsicherheit → Review; Confidence < 0.60 →
  Reject; ein Einzelsignal → nie AutoAccept.
- `AutoApprovalService`: bestehende 6 Tests bleiben gruen (gleiche Urteile, Gruende
  kompatibel halten).
- `DefectStatusPolicy`: bestehende Tests (entschiedene Events) bleiben gruen; neue
  fuer die Zonen-Zuordnung ueber die zentrale Regel.
- Factory-Anreicherung: `QualityGateLevel` wird im `AiContext` beim Anlegen gesetzt.
- Volllauf: Infrastructure.Tests + UI.Tests + Application-Tests ohne Regression.

## 7. Nicht-Ziele / Risiken

- **Sichtbare Aenderung:** Beim Codieren wird kuenftig weniger gruen als heute
  (nur noch mit gruener Ampel, nicht schon ab 85 % Sicherheit). Das ist gewollt.
- Kein Umbau des QualityGate selbst, der Evidence-Sammlung oder der Pipeline.
- Keine Aenderung an Dedup/Voting oder am Selbsttraining.
