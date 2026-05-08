# Phase 1.5 Status — Photo-Assistant Service Migration

**Datum:** 2026-05-08
**Slice:** 1.5 (PhotoMeasurementWindow.xaml.cs Service-Migration)
**Status:** BLOCKIERT (konservative Entscheidung — keine Migration durchgefuehrt)

## Auftrag

Lagere die rein-rechnerische Logik aus `PhotoMeasurementWindow.xaml.cs` (2235 Zeilen)
in Application-Services aus. Konkret: pruefe ob `BendAngleToolService`,
`DeformationToolService`, `LateralToolService` von
`src/AuswertungPro.Next.UI/Ai/PhotoAssistant/` nach
`src/AuswertungPro.Next.Application/Ai/PhotoAssistant/` migrierbar sind.

## Befund

Alle drei Service-Klassen sind grundsaetzlich pure Math/Geometry-Logik (keine
Canvas-/Brush-/Color-Verwendung), aber sie nutzen `System.Windows.Point` aus
`PresentationCore.dll` als Datentyp fuer 2D-Punkte. Konkret:

| Service | `System.Windows.Point`-Verwendung |
| --- | --- |
| `BendAngleToolService` | Rueckgabetyp `ProjectedRing.AxisCenterScreen`, `ProjectedRing.RingPoints`, Rueckgabe-Tupel `KinkPointScreen` |
| `DeformationToolService` | Parameter `center` in `ComputePoints`, Rueckgabe `IReadOnlyList<Point>` |
| `LateralToolService` | Parameter `pipeCenter` in `ComputeSichelCenter`, Rueckgabe `Point` |

Das `AuswertungPro.Next.Application`-Projekt zielt auf `net10.0` (kein
`UseWPF=true`) — `System.Windows.Point` ist dort nicht verfuegbar. Eine
Migration erfordert daher **eines** von:

1. **Type-Wechsel auf eigenen `Point2D`-Record im Application-Layer** —
   bricht die API-Signaturen, alle Caller (vor allem
   `PhotoMeasurementWindow.PhotoAssistant.cs` mit ~10 Aufrufstellen) muessten
   Konvertierungen anwenden. Das ist KEINE reine Datei-Verschiebung mehr.
2. **`UseWPF=true` im Application-Projekt aktivieren** — verletzt das
   Architektur-Prinzip "Application ist UI-frei" (CLAUDE.md, Thin-AI).
3. **Wrapper-Schicht bauen, die `Point` <-> `Point2D` mappt** — fuegt
   Komplexitaet hinzu ohne klaren Mehrwert.

## Entscheidung

**Konservativ belassen** — die drei Services bleiben in
`src/AuswertungPro.Next.UI/Ai/PhotoAssistant/`. Begruendung:

- Auftrag fordert explizit "KEINE Verhaltens-Aenderung — nur Datei-Move".
- Keine der drei Migrations-Optionen ist ein reiner Datei-Move.
- Risiko/Nutzen-Verhaeltnis ist im aktuellen Slice ungunstig: 10+ Caller,
  Test-Anpassungen, und die Services sind heute schon klar gekapselt
  (statisch, keine UI-Abhaengigkeit zur Laufzeit).

## Vorbestehender Build-Issue (NICHT von dieser Phase verursacht)

`dotnet build` schlaegt bereits am Baseline-Stand fehl mit:

```
NU1011: Die folgenden PackageVersion-Elemente koennen keine unverankerte
Version angeben: Polly
```

Ursache: `Directory.Packages.props` enthaelt `<PackageVersion Include="Polly"
Version="8.*" />`. Unter Central Package Management mit
`CentralPackageTransitivePinningEnabled=true` sind unverankerte Versionen
(`*`) nicht erlaubt. Fix: pin auf konkrete Version (z.B. `8.5.2`).

Dieser Issue blockiert die Akzeptanz-Pruefung "build gruen" fuer Phase 1.5
unabhaengig vom Service-Move.

## Folge-Slice-Vorschlag (1.5b)

**Ziel:** Photo-Assistant-Services nach Application migrieren mit
sauberem `Point2D`-Wert-Typ.

1. Lege `AuswertungPro.Next.Application/Ai/PhotoAssistant/Point2D.cs` an
   (record struct mit X/Y double).
2. Migriere die drei Services Datei fuer Datei nach Application und ersetze
   `System.Windows.Point` durch `Point2D`.
3. Passe `PhotoMeasurementWindow.PhotoAssistant.cs` an: an den ~10
   Aufrufstellen Konvertierung `(Point2D p) => new Point(p.X, p.Y)` /
   umgekehrt einfuegen — am besten als kleine Extension `ToWpfPoint()` /
   `ToPoint2D()` im UI-Layer.
4. Tests umstellen (3 Test-Dateien unter `tests/.../PhotoAssistant/`).
5. Build + Tests gruen pruefen.

**Voraussetzung:** vorbestehender `Polly 8.*`-Issue muss zuerst gefixt sein,
sonst ist `dotnet build` nicht aussagekraeftig.

## Geprueft / nicht migriert

- `src/AuswertungPro.Next.UI/Ai/PhotoAssistant/BendAngleToolService.cs` (185 Zeilen)
- `src/AuswertungPro.Next.UI/Ai/PhotoAssistant/DeformationToolService.cs` (92 Zeilen)
- `src/AuswertungPro.Next.UI/Ai/PhotoAssistant/LateralToolService.cs` (122 Zeilen)

## Tests (existieren bereits, NICHT angefasst)

- `tests/AuswertungPro.Next.Pipeline.Tests/PhotoAssistant/BendAngleToolServiceTests.cs`
- `tests/AuswertungPro.Next.Pipeline.Tests/PhotoAssistant/DeformationToolServiceTests.cs`
- `tests/AuswertungPro.Next.Pipeline.Tests/PhotoAssistant/LateralToolServiceTests.cs`
