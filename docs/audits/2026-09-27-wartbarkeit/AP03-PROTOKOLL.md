# AP03 – Arbeitsprotokoll: zwei gemeinsame Regeln an einer Stelle

Stand: 28.09.2026. Plan: [UMSETZUNGSPLAN.md](UMSETZUNGSPLAN.md), Paket AP03 (Befund W10).

## Teil A – Preisermittlung

**Ausgangsstand:** `QtyMatches`, `FindNearestDnCandidates` und `BuildNearestDnPriceHint` standen
Zeichen für Zeichen gleich in `Application/Cost/CatalogPriceResolver.cs` und
`Infrastructure/Costs/CostCalculatorLogicService.cs`. Im Produktcode rief niemand die
Infrastruktur-Fassung auf, nur Tests.

**Umsetzung:** `CatalogPriceResolver` bleibt die fachliche Quelle. Die drei öffentlichen Helfer
in `CostCalculatorLogicService` delegieren dorthin; Signaturen unverändert. `ParseDn` bleibt
bewusst getrennt: Die Application-Fassung parst mit `InvariantCulture`, die Infrastruktur-Fassung
mit der aktuellen Kultur (wie im Plan vorgesehen, nicht ungeprüft zusammengelegt).

**Tests:** vorhanden waren gleiche Entfernung nach oben/unten, Mengengrenzen und Hinweistext.
Neu: leere Preisliste, exakte DN in zwei Bereichen (stabile Reihenfolge) und ein Gleichstandstest
Fassade = Application-Regel über fünf DN-Werte, alle Preisstufen und Mengengrenzen.

## Teil B – Klassenkartenbindung der Negativbilder

**Ausgangsstand:** `ValidateStrictNegativeClassMapBinding` stand identisch in
`TrainingExportPlanService` (Application, Planer) und `TrainingExportPlanInputBuilder`
(Infrastructure, Planer-Eingabe).

**Umsetzung:** Neue reine Regel `Application/Ai/Training/ExportPlans/TrainingNegativeClassMapBinding.cs`
(`Validate`). Beide Grenzen rufen sie an derselben Stelle wie bisher auf; keine der beiden
Prüfungen wurde entfernt. Meldung und Ausnahme-Typ unverändert.

**Tests:** neu `TrainingNegativeClassMapBindingTests` – korrekte Karte v3; Negativbild an Version 2
gebunden; aktive Karte Version 2; aktive Karte ohne Hash; anderer Kartenhash; anderer VSA-Hash;
Klasse fehlt; Klassen vertauscht (BCC_bogen nicht auf 14); Lücke in den Klassen-IDs.

## Gegenproben (danach zurückgesetzt)

| Eingebauter Fehler | Ergebnis |
|---|---|
| Regel prüft BCC_bogen auf Position 14 nicht mehr | 1 Test rot |
| Aufruf an der Planer-Eingabe entfernt | 1 Test rot (bestehender Infrastrukturtest) |
| Aufruf im Planer entfernt | 1 Test rot (bestehender Infrastrukturtest) |
| Preis-Hinweistext verändert | 1 Test rot |

Jede der zwei Schutzgrenzen ist damit einzeln abgesichert.

## Ergebnis

Eine Regeldefinition je Fall, bisherige öffentliche Aufrufe verfügbar, beide Schutzgrenzen
aktiv, kein geändertes Preis- oder Exportergebnis. Prüfungen: siehe Commit-Beschreibung.

## Restgrenze

- Die Infrastruktur-Helfer der Preisregel haben weiterhin keine Aufrufer im Produktcode. Sie
  zu entfernen wäre eine Änderung der öffentlichen Fassade und ist nicht Teil dieses Pakets.
- Die zwei `ParseDn`-Fassungen bleiben verschieden; ein Zusammenlegen braucht eine eigene
  Prüfung, wo die kulturabhängige Fassung heute tatsächlich verwendet wird.
- Rücknahme: Delegation bzw. gemeinsame Regel zusammen mit ihren Aufrufern zurücknehmen.
