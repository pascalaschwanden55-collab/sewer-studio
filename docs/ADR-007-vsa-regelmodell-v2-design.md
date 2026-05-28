# ADR-007 C.0: VSA-Regelmodell v2 fuer Zustandsklassifizierung

**Status:** Accepted
**Datum:** 2026-05-28
**Bezug:** `docs/ADR-007-vsa-zustandsklassifizierung-und-anwendungsregeln.md`
**Accepted:** 2026-05-28
**Quelle:** `docs/vsa-zustandsklassifizierung-2023-schwellen.md`

---

## Ziel

Die VSA-Zustandsbewertung darf nicht mehr aus der alten Datei
`classification_channels.json` / `classification_manholes.json` abgeleitet
werden. Diese Dateien enthalten fachlichen Drift:

- `BAA` wurde als Riss bewertet, ist aber Verformung.
- `BAB` wurde als Bruch bewertet, ist aber Riss.
- `BBA` wurde als Deformation bewertet, ist aber Wurzeln.
- weitere Schacht-/Kanal-Regeln sind zu grob oder nicht belegbar.

V2 ersetzt das durch ein belegtes Regelmodell aus der PDF-Tabelle. Es ist
wichtig: **keine Schwellen werden geraten**. Unklare Stellen bleiben explizit
als `missing-vsa-source` oder `needs-review` markiert.

---

## Nicht-Ziele

- Keine automatische Korrektur alter Reports.
- Keine Umlabelung von Trainingsdaten.
- Kein Umbau des VSA-Code-Katalogs aus ADR-006.
- Keine Loeschung der Legacy-JSON-Dateien, bevor V2 komplett getestet ist.
- Keine geschaetzten Schwellenwerte.

---

## Grundmodell

### RuleSet

```json
{
  "schemaVersion": 2,
  "source": "VSA_Rili_ Zustandsbeurteilung von Entwaesserungsanlagen.pdf",
  "assetKind": "channel",
  "rules": []
}
```

`assetKind`:

- `channel`
- `manhole`

V2 muss Kanal- und Schachtregeln gleichzeitig laden koennen. Die heutige
Logik nimmt den ersten vorhandenen Kandidatenpfad und kann dadurch implizit
nur eine Tabelle aktiv verwenden. Das darf in V2 nicht so bleiben.

### Rule

```json
{
  "id": "C-T07-BAA-S-rigid",
  "code": "BAA",
  "codeMatch": "exact",
  "ch1": [],
  "ch2": [],
  "requirement": "S",
  "parameter": "q1",
  "unit": "%",
  "scope": {
    "pipeFlexibility": "rigid",
    "areas": []
  },
  "classification": {
    "mode": "range",
    "ranges": [
      { "ez": 0, "minInclusive": 7.0 },
      { "ez": 1, "minInclusive": 5.0, "maxExclusive": 7.0 },
      { "ez": 2, "minInclusive": 3.0, "maxExclusive": 5.0 },
      { "ez": 3, "minInclusive": 1.0, "maxExclusive": 3.0 },
      { "ez": 4, "maxExclusive": 1.0 }
    ]
  },
  "status": "ok",
  "sourceRef": "PDF-Dateiseite 22 / gedruckte Seite 20 / Anhang C Tabelle 7",
  "notes": []
}
```

### Felder

`codeMatch`:

- `exact`: nur genau dieser Code.
- `prefix`: nur falls fachlich bewusst gesetzt. Kein heimliches Prefix-Matching.

`ch1` / `ch2`:

- leere Liste bedeutet Wildcard / alle.
- konkrete Werte sind normalisierte Grossbuchstaben, z.B. `["A", "B"]`.

`requirement`:

- `D` = Dichtheit
- `S` = Standsicherheit
- `B` = Betriebssicherheit
- `null` nur fuer Regeln mit `status = "needs-review"` erlaubt.

`parameter`:

- `q1`
- `q2`
- `none`

`unit`:

- `%`
- `mm`
- `deg`
- `Anzahl`
- `none`

`status`:

- `ok`: Regel darf produktiv klassifizieren.
- `missing-vsa-source`: Quelle unvollstaendig oder nicht lesbar, Regel darf
  nicht klassifizieren.
- `needs-review`: fachliche Klaerung offen, Regel darf nicht klassifizieren.

---

## EZ-Skala

Die Richtlinie nutzt **EZ 0 bis EZ 4**.

- `EZ 0` = kritisch / schlecht
- `EZ 4` = gut / unkritisch

V2 uebernimmt diese Skala direkt. Es gibt keine Umrechnung auf 1 bis 5.
Die alte JSON-Struktur mit Werten wie 1/3/4 darf nicht als neues Schema
interpretiert werden.

---

## Klassifizierungsmodi

### 1. Range

Quantifizierte Regeln mit Schwellen, z.B. `BAA` Verformung in Prozent.

```json
{
  "mode": "range",
  "ranges": [
    { "ez": 0, "minInclusive": 7.0 },
    { "ez": 4, "maxExclusive": 1.0 }
  ]
}
```

Grenzen muessen bewusst inklusiv/exklusiv modelliert werden. Standard:

- Untergrenze inklusiv.
- Obergrenze exklusiv.
- Offene Ober-/Untergrenzen sind erlaubt.

### 2. Fixed

Regeln ohne Quantifizierung oder mit fixer Bewertung.

```json
{
  "mode": "fixed",
  "ez": 4
}
```

In der Markdown-Tabelle bedeutet ein Eintrag `alle` in einer EZ-Spalte:
diese Regel klassifiziert fest auf diese EZ-Stufe.

`alle*` wird genauso klassifiziert, aber der Stern muss in `notes` erhalten
bleiben.

### 3. Missing

Unklare PDF-Stellen:

```json
{
  "mode": "missing",
  "reason": "missing-vsa-source"
}
```

Diese Regeln erzeugen eine Diagnose, aber keinen EZ-Wert.

---

## Scope und Materiallogik

### Kanal: biegesteif / biegeweich

Einige Regeln unterscheiden nach Rohrsteifigkeit, z.B. `BAA`.

Material-Mapping:

| Rohrmaterial | Scope |
|---|---|
| Beton | `rigid` |
| Steinzeug | `rigid` |
| Guss / Gusseisen | `rigid` |
| Mauerwerk | `rigid` |
| PVC | `flexible` |
| PE | `flexible` |
| PP | `flexible` |
| GFK | `flexible` |
| Kunststoff allgemein | `flexible` |

Wenn das Material fehlt oder unbekannt ist:

- Regeln mit `pipeFlexibility = "any"` duerfen matchen.
- Regeln mit `rigid` oder `flexible` duerfen nicht geraten werden.
- Ergebnisdiagnose: `scope-unresolved`.

### Schacht: Bereiche

Schachtregeln nutzen Bereiche wie `A`, `B`, `D`, `F`, `I`, `J`.

Wenn der Bereich in der Beobachtung fehlt:

- nur Regeln mit Bereich `alle` duerfen matchen.
- spezifische Bereichsregeln erzeugen Diagnose `area-missing`.

---

## Selektor-Logik

Der V2-Selektor arbeitet deterministisch:

1. Code normalisieren (`Trim`, uppercase).
2. RuleSet bestimmen:
   - expliziter Asset-Typ hat Vorrang.
   - sonst Prefix `B` -> Kanal, Prefix `D` -> Schacht.
3. Code matchen:
   - `exact` zuerst.
   - `prefix` nur wenn in der Regel explizit gesetzt.
4. `Ch1` filtern.
5. `Ch2` filtern.
6. `requirement` filtern (`D`, `S`, `B`).
7. Scope aufloesen:
   - Kanal: Material -> `rigid` / `flexible`.
   - Schacht: Bereich.
8. Quantifizierung aufloesen:
   - `parameter = q1`: `Quantifizierung1`.
   - `parameter = q2`: `Quantifizierung2`.
   - `parameter = none`: kein Wert noetig.
9. Klassifikation berechnen:
   - `range`: Wert in Schwelle einsortieren.
   - `fixed`: festen EZ-Wert verwenden.
   - `missing`: Diagnose, kein EZ-Wert.
10. Wenn mehrere Regeln gleich gut matchen:
   - exact vor prefix
   - Ch1 konkret vor Wildcard
   - Ch2 konkret vor Wildcard
   - Scope konkret vor `any`
   - sonst Diagnose `ambiguous-rule`

---

## Ergebnisobjekt

V2 muss mehr liefern als nur `EZD/EZS/EZB`.

```csharp
public sealed record VsaClassificationOutcome(
    string Code,
    VsaRequirementOutcome? D,
    VsaRequirementOutcome? S,
    VsaRequirementOutcome? B,
    IReadOnlyList<VsaRuleDiagnostic> Diagnostics);

public sealed record VsaRequirementOutcome(
    string Requirement,
    int Ez,
    string RuleId,
    string SourceRef);

public sealed record VsaRuleDiagnostic(
    string Code,
    string Requirement,
    string Reason,
    string Message);
```

`VsaEvaluationService` kann daraus vorerst weiter die bekannten Felder
`VSA_Zustandsnote_D`, `VSA_Zustandsnote_S`, `VSA_Zustandsnote_B` schreiben.
Die Diagnosen muessen aber fuer Tests und Log sichtbar bleiben.

---

## Neue Dateien in Phase C

### Generierte Daten

- `src/AuswertungPro.Next.UI/Data/vsa_zustandsklassifizierung_2023_channels.json`
- `src/AuswertungPro.Next.UI/Data/vsa_zustandsklassifizierung_2023_manholes.json`

### Code

Vorschlag:

- `src/AuswertungPro.Next.Infrastructure/Vsa/Classification/VsaClassificationRuleSet.cs`
- `src/AuswertungPro.Next.Infrastructure/Vsa/Classification/VsaClassificationRuleLoader.cs`
- `src/AuswertungPro.Next.Infrastructure/Vsa/Classification/VsaClassificationRuleSelector.cs`
- `src/AuswertungPro.Next.Infrastructure/Vsa/Classification/VsaMaterialScopeResolver.cs`

Die alte Klasse `VsaClassificationTable` bleibt vorerst bestehen, wird aber
als Legacy markiert, sobald V2 produktiv laeuft.

---

## Teststrategie

### C.1 JSON-Generierung

- JSON laedt ohne Fehler.
- Alle Regeln haben `sourceRef`.
- Keine Regel mit `status = ok` darf `mode = missing` haben.
- Jede `missing-vsa-source`-Regel erzeugt bewusst keine Klassifizierung.
- EZ-Werte sind nur `0,1,2,3,4`.

### C.2 Selektor

Pflichtfaelle:

- `BAA` mit Beton und Q1 = 0.5 % -> biegesteife BAA-Regel.
- `BAA` mit PVC und Q1 = 0.5 % -> biegeweiche BAA-Regel.
- `BAB` Riss mit mm-Wert -> Rissregel, nicht BAA.
- `BAC` Ch1 C -> EZ 0 fuer D/S/B.
- `BBA` Wurzeln -> nicht Deformation.
- Schachtregel mit Bereich fehlt -> Diagnose `area-missing`.
- Material fehlt bei BAA -> Diagnose `scope-unresolved`.
- `missing-vsa-source` -> Diagnose, kein EZ.

### D Umstellung

- Drift-Test aus Phase A wird entskippt oder durch gruene V2-Tests ersetzt.
- Legacy-Tests, die falsche Bedeutungen zementieren, werden entfernt oder
  fachlich korrigiert.
- Full solution test muss gruen bleiben.

---

## Migration

### Phase C.1

Markdown-Tabelle in V2-JSON ueberfuehren. Noch keine produktive Service-
Umstellung.

### Phase C.2

Loader, Materialresolver und Selektor implementieren. Tests rein gegen V2.

### Phase D.1

V2 im Shadow-Modus neben Legacy ausfuehren. Drift sichtbar protokollieren,
bis zur fachlichen Freigabe nicht produktiv blockieren. Erledigt vor D.2.

### Phase D.2

Erledigt 2026-05-28: `VsaEvaluationService` nutzt V2 produktiv. Legacy-JSON
wird nur noch geladen, wenn `VsaUseV2Engine=false` als Rollback gesetzt ist.

### Phase E

ADR-007 ist `Accepted`. Legacy-Dateien bleiben fuer Rollback/Shadow vorhanden,
sind aber nicht mehr produktive Quelle.

---

## Offene Restpunkte nach produktiver Umstellung

1. **DBH:** In der PDF-Tabelle ist die Anforderung nicht sauber lesbar. Nicht
   raten. `needs-review`, bis fachlich bestaetigt.
2. **BAJ / einzelne Stern-Fussnoten:** Stern- und unvollstaendige Zeilen aus
   der Markdown-Tabelle muessen als `notes` erhalten bleiben.
3. **Schacht-Bereich im Datenmodell:** Klaeren, aus welchem Feld der Bereich
   fuer D-Codes kommt.
4. **Material-Synonyme:** Die Materialliste muss gegen den FieldCatalog
   final abgeglichen werden.
5. **Unbekannte Materialien:** Kein Default. Diagnose statt geratenem Scope.

---

## Codex-Auftrag fuer Phase C.1

1. Parser/Generator fuer `docs/vsa-zustandsklassifizierung-2023-schwellen.md`
   schreiben.
2. Zwei JSON-Dateien im V2-Format erzeugen:
   - channels
   - manholes
3. Validierungstests schreiben:
   - Schema laedt.
   - EZ nur 0..4.
   - `BAA/BAB/BAC/BBA/BBB/BBC` sind fachlich korrekt im JSON.
   - `missing-vsa-source` klassifiziert nicht.
4. Noch keine Umstellung von `VsaEvaluationService`.

Erst nach gruenen C.1-Tests startet C.2.
