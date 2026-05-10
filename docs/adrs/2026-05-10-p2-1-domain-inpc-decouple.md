# P2.1 Domain-INPC Entkoppeln — Mini-ADR

Datum: 2026-05-10
Status: **Entwurf** (Sicherheits-Pfad: nur Plan + additive Skeletons + Architektur-Test;
keine Migration der UI-Konsumenten in dieser Session)

Vorgeschichte:
- AUDIT_SEWERSTUDIO_2026-04-23.md ARCH-H1: `HaltungRecord` + `SchachtRecord`
  implementieren `INotifyPropertyChanged` direkt im Domain-Layer.
  Blockiert Headless-Use (Sidecar/CLI) und mischt UI-Concerns in Domain.
- ROADMAP P2.1 / docs/STANDORTBESTIMMUNG_2026-05-10.md Item C: Migration
  ist 3-5 Tage Arbeit, 50+ UI-Konsumenten, 133 XAML-Bindings. **Doku-Warnung:
  "NICHT in autonomer Session machbar — manueller WPF-Smoke unverzichtbar."**

## User-Entscheidung 2026-05-10

User wurde explizit gefragt ob die Doku-Warnung respektiert werden soll und
hat gewaehlt:
> "Sicher: Mini-ADR + Architektur-Test + Wrapper-Skeletons (additiv, ~30min)"

Damit ist der Scope dieser ADR begrenzt auf:
1. Diesen Mini-ADR mit dokumentiertem Migrations-Plan.
2. Architektur-Test der INPC-Praesenz im Domain-Layer detektiert
   (heute "skipped/dokumentiert", wird "Pass" wenn Migration durch).
3. Wrapper-VM-Klassen `HaltungRecordViewModel` + `SchachtRecordViewModel`
   als additive Skeletons im UI-Layer. Halten erstmal nur einen Reference
   auf den POCO-Record und delegieren Property-Changes — sie sind noch
   NICHT die kanonische DataContext-Quelle.

**Was diese ADR ausdruecklich NICHT macht:**
- Keine Aenderung an HaltungRecord/SchachtRecord (INPC bleibt drin).
- Keine Migration von UI-Konsumenten oder XAML-Bindings.
- Keine Tests umstellen, keine ViewModels umbauen.

## Bestandsaufnahme

| Aspekt | Wert |
|---|---|
| Domain-Klassen mit INPC | 2 (HaltungRecord, SchachtRecord) |
| HaltungRecord Caller (.cs Files) | 74 |
| SchachtRecord Caller (.cs Files) | 13 |
| XAML-Bindings auf Record-Properties | ~133 (Doku-Schaetzung) |

HaltungRecord-Implementation: einfach. `event PropertyChangedEventHandler?
PropertyChanged;` plus 3 Invocations in `SetFieldValue` (Fields, Fields[name],
ModifiedAtUtc).

SchachtRecord: dasselbe Muster, kuerzer.

## Migrations-Plan (5 Steps, NICHT in dieser Session)

### Step 1 (heute): Architektur-Test + VM-Skeletons (additiv)

**Architektur-Test** (`tests/AuswertungPro.Next.Pipeline.Tests/ArchitectureTests/DomainInpcTests.cs`):
- Reflection ueber `AuswertungPro.Next.Domain.Models`-Namespace.
- Soll Pass werden wenn KEIN Type INPC implementiert.
- Heute: Test ist da, hat ein **`Skip`** mit Hinweis auf Migration-Plan,
  damit er die Build-Pipeline nicht rot macht. Nach Step 4 wird `Skip`
  entfernt und der Test schaltet als Quality-Gate scharf.

**Wrapper-Skeletons** (`src/AuswertungPro.Next.UI/ViewModels/Domain/`):
- `HaltungRecordViewModel : ObservableObject` mit Reference auf POCO.
- `SchachtRecordViewModel : ObservableObject` analog.
- Skeletons sind NICHT die kanonische DataContext-Quelle in dieser
  Session — Bindings zeigen weiter direkt auf HaltungRecord (POCO mit
  INPC). Skeletons existieren als Vorbereitung fuer Step 2-4.

### Step 2 (Folge-Session, mehrere Stunden): UI-Konsumenten umstellen

Pro UI-Konsument (Page/Window/Dialog/ViewModel):
1. POCO-HaltungRecord -> HaltungRecordViewModel im DataContext-Pfad.
2. XAML-Bindings auf VM.Field-Property aendern (oder Field-Indexer ueber
   IDictionary-Access bleiben, wenn Wrapper das delegiert).
3. Build + UI-Smoke pro Konsument.

Reihenfolge: Pages, dann Windows, dann Dialogs. ~50+ Konsumenten — pro
Session ~10-15 realistisch.

### Step 3 (Folge-Session): XAML-Bindings prueft

`{Binding Fields[xxx]}` und `{Binding xxx}` jeweils gegen VM-Schema
verifizieren. Resharper/Roslyn-Refactor-Skript empfohlen.

### Step 4 (Folge-Session, finale Stunde): INPC raus aus Domain

- HaltungRecord/SchachtRecord: `event PropertyChangedEventHandler` + 3x
  PropertyChanged-Invoke entfernen.
- `: INotifyPropertyChanged`-Interface raus.
- Architektur-Test `Skip` entfernen → Test wird scharf, Pipeline schuetzt
  vor Regression.

### Step 5: Doku, ADR auf Done, Memory-Eintrag

## Heutiges Risiko-Niveau

- Architektur-Test: 0 Risiko (mit Skip).
- VM-Skeletons: 0 Risiko (additiv, kein Caller).
- Build/Tests: muessen weiter gruen bleiben.

## Erwartetes Ergebnis nach dieser Session

- Plan dokumentiert, Folge-Sessions koennen direkt mit Step 2 starten.
- Architektur-Test als Quality-Gate-Vorbereitung.
- VM-Skeletons stehen bereit fuer den ersten Konsumenten in Step 2.
