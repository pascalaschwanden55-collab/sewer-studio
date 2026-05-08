# ADR-0004: INotifyPropertyChanged in Domain-Modellen — Tech-Debt-Akzeptanz mit Migrationspfad

- **Status**: accepted
- **Datum**: 2026-05-08
- **Verantwortlich**: Solo-Entwicklung

## Kontext

Zwei zentrale Domain-Modelle implementieren `INotifyPropertyChanged`
direkt:

- `AuswertungPro.Next.Domain.Models.HaltungRecord`
- `AuswertungPro.Next.Domain.Models.SchachtRecord`

(`Project` benutzt eigene Notification-Mechanismen, kein direktes
`INotifyPropertyChanged`.)

`INotifyPropertyChanged` ist ein WPF-/UI-Pattern. Es gehoert
**architektonisch nicht** in die Domain-Schicht — die soll frei von
UI-Frameworks sein, damit sie spaeter auch Headless (Server, CLI,
WebAPI) verwendbar bleibt.

`HaltungRecord.cs` selbst dokumentiert das als Tech-Debt:

```csharp
// ARCH-H1 (Audit 2026-04-23): INotifyPropertyChanged in Domain ist Tech-Debt.
//   1. POCO-Record `HaltungRecord` (ohne INotifyPropertyChanged) in Domain.
//   2. ViewModel-Wrapper in UI mit INotifyPropertyChanged.
//   3. Migration der UI-Bindings auf den Wrapper.
```

Stand 2026-05-08:
- `HaltungRecordViewModel` existiert bereits in `src/AuswertungPro.Next.UI/ViewModels/Records/`.
- ABER: `HaltungRecord` wird noch in **50+ Code-Stellen** direkt
  referenziert (Imports, Reports, KI-Wrapper, UI-ViewModels).
- 133 XAML-Bindings in 10 XAML-Files referenzieren Properties.
- Keine vergleichbaren Wrapper fuer `SchachtRecord` und `Project`.

## Entscheidung

**Tech-Debt akzeptieren** mit klarem, dokumentiertem Migrationspfad
statt jetzt eine grosse, riskante Migration zu starten.

**Begruendung der Akzeptanz:**

1. Eine vollstaendige Migration ist **mehrtaegig**: 50+ Berührungspunkte,
   133 XAML-Bindings, 3 Domain-Modelle. Hohes Risiko fuer schleichende
   Bugs (UI-Updates die ploetzlich nicht mehr triggern).

2. Bestehende `INotifyPropertyChanged`-Implementierung **funktioniert**.
   Die Domain ist nicht broken — sie ist nur **architektonisch nicht
   sauber**.

3. Die wirklichen Schmerz-Punkte sind woanders (KI-Erkennungsqualitaet
   bei 52 % Validation-Accuracy). Architektur-Sauberkeit allein bringt
   keine Verbesserung der Produktqualitaet.

**Migrationspfad fuer spaeter:**

```
Phase 1 (1 Tag):
- Wrapper komplettieren: SchachtRecordViewModel + ProjectViewModel
- Architecture-Test der die direkte Verwendung von HaltungRecord
  als WPF-Binding-Source aufzeigt

Phase 2 (2-3 Tage):
- 10 XAML-Files: alle Bindings auf ViewModel umstellen
- ViewModels: alle Property-Lookups auf den Wrapper umstellen
- Tests fuer Wrapper-Verhalten

Phase 3 (1 Tag):
- INotifyPropertyChanged + PropertyChanged-Handler aus Domain entfernen
- Domain-Modelle als sealed records umstellen
- Architektur-Test sichert Domain-Sauberkeit dauerhaft
```

## Alternativen erwogen

1. **Sofortige Migration in dieser Session**: verworfen wegen Risiko
   und Zeitbudget. Auto-Mode-Sessions sollen keine schleichenden
   UI-Bugs hinterlassen.

2. **`INotifyPropertyChanged` ueber Source Generator**: theoretisch
   moeglich, aber loest das Architektur-Problem nicht — nur das
   Boilerplate. Domain bleibt UI-abhaengig.

3. **Tech-Debt ignorieren**: nicht akzeptabel, weil der Migrationspfad
   sonst verloren geht. Dokumentation hier macht ihn explizit.

## Konsequenzen

**Positiv:**
- Keine schleichenden UI-Bugs durch unvollstaendige Migration.
- Migrationspfad ist dokumentiert — kann zu jedem Zeitpunkt aufgegriffen
  werden.
- Solo-Entwickler-Ressourcen werden auf Punkte mit hoeherem Hebel
  fokussiert (KI-Datenqualitaet).

**Negativ:**
- Domain-Layer-Test "Domain darf keine UI-Frameworks importieren"
  schlaegt aktuell an. Wird mit `[Trait("Status", "TechDebt-ARCH-H1")]`
  oder `Skip = "ADR-0004"` markiert.
- "Headless-Betrieb moeglich" bleibt eine Behauptung, kein verifiziertes
  Faktum.

## Architekturtest

Ein Test in `tests/AuswertungPro.Next.Pipeline.Tests/ArchitectureLayerGuardTests.cs`
sollte den heutigen Zustand explizit festhalten:

```csharp
[Fact]
public void Domain_ImplementsInotifyPropertyChanged_TechDebtAccepted_AdrOhO4()
{
    // ARCH-H1 / ADR-0004: HaltungRecord, SchachtRecord, Project
    // implementieren INotifyPropertyChanged. Akzeptiert mit Migrationspfad.
    // Wenn diese Liste schrumpft (Migration fortschreitet), Test anpassen.

    var domainAssembly = typeof(AuswertungPro.Next.Domain.Models.HaltungRecord).Assembly;
    var inpcImplementers = domainAssembly.GetTypes()
        .Where(t => typeof(INotifyPropertyChanged).IsAssignableFrom(t))
        .Select(t => t.Name)
        .OrderBy(n => n)
        .ToArray();

    Assert.Equal(
        new[] { "HaltungRecord", "SchachtRecord" },
        inpcImplementers);
}
```

Wenn die Migration startet:
1. ViewModel anlegen.
2. Bindings umstellen.
3. INotifyPropertyChanged aus Domain entfernen.
4. **Testliste oben anpassen** — Test wird gruen mit kuerzerer Liste.
5. Wenn die Liste leer ist: ADR-0004 auf `superseded` setzen, neuer
   ADR ueber die Domain-Sauberkeit.

## Referenzen

- Original-Tech-Debt-Marker: `src/AuswertungPro.Next.Domain/Models/HaltungRecord.cs:3`
- Audit-Dokument: `docs/audits/v4.3/AUDIT_SUMMARY.md` (ARCH-H1)
- Wrapper: `src/AuswertungPro.Next.UI/ViewModels/Records/HaltungRecordViewModel.cs`
- Wrapper-Tests: `tests/AuswertungPro.Next.Pipeline.Tests/HaltungRecordViewModelTests.cs`
