# Eigentümerdossier — alle sichtbaren Felder ausfüllbar (Umsetzungsplan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Jedes Feld, das im erzeugten Eigentümerdossier sichtbar ist, wird im Programm ausgefüllt; Logo und Wappen stecken fest in jedem Dossier.

**Architecture:** Additiv auf dem bestehenden Dossier-Baustein. Ein neuer, kleiner Infrastructure-Baustein setzt Bilder in Word-Platzhalter ein (`DocxImagePlaceholderFiller` mit `ImageSizeReader`); das Datenmodell bekommt eine Eigentümer-Zeilenliste, einen Planbildpfad und ein Autorenfeld samt reiner Umstellungsfunktion (Formatversion 1 → 2); die Vorlage wird auf den Aufbau des Originals umgebaut; die beiden Eingabefenster bekommen die fehlenden Felder.

**Tech Stack:** C# / .NET 10, WPF (Fenster ohne MVVM, reines Code-behind), DocumentFormat.OpenXml 3.1.1, xUnit. **Keine neuen NuGet-Pakete.**

**Spec:** `docs/superpowers/specs/2026-08-23-eigentuemerdossier-felder-design.md`

## Global Constraints

- **Keine neuen NuGet-Pakete.** `DocumentFormat.OpenXml` 3.1.1 ist bereits in `AuswertungPro.Next.Infrastructure` referenziert.
- **Kommentare und UI-Texte auf Deutsch.** Bezeichner im Code deutsch oder englisch wie im umliegenden Bestand; im Dossier-Baustein sind Klassennamen englisch, Kommentare deutsch.
- `AuswertungPro.Next.Infrastructure` ist `net10.0` **ohne WPF und ohne System.Drawing** — Bildmasse müssen selbst aus den Dateibytes gelesen werden.
- **Im fertigen Word darf nie `{{` oder `}}` stehen bleiben.** Das gilt auch für nicht ersetzte Bildplatzhalter.
- **Kundendateien werden nie verändert.** Das Original-PDF wird nur gelesen.
- Bauen: `dotnet build AuswertungPro.sln`
- Testen: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj`
- Zielaufbau des Dokuments und alle Entscheidungen stehen in der Spec.

---

## Dateiübersicht

| Datei | Verantwortung |
|---|---|
| `Export_Vorlage/Dossier_Logo.png` (neu) | festes Firmenlogo für jedes Dossier |
| `Export_Vorlage/Dossier_Wappen.png` (neu) | festes Wappen für jedes Dossier |
| `src/AuswertungPro.Next.Infrastructure/Dossiers/ImageSizeReader.cs` (neu) | liest Breite und Höhe aus PNG- und JPEG-Bytes |
| `src/AuswertungPro.Next.Infrastructure/Dossiers/DocxImagePlaceholderFiller.cs` (neu) | ersetzt `{{@Name}}` durch ein eingebettetes Bild |
| `src/AuswertungPro.Next.Domain/Models/Dossiers/DossierModels.cs` (ändern) | `DossierOwnerRow`, `Owners`, `OverviewPlanPath`, `Authors`, Formatversion 2 |
| `src/AuswertungPro.Next.Application/Dossiers/DossierDocumentMigration.cs` (neu) | reine Umstellung Version 1 → 2 |
| `src/AuswertungPro.Next.Infrastructure/Dossiers/DossierFileStore.cs` (ändern) | ruft die Umstellung beim Laden auf, kennt Version 2 |
| `src/AuswertungPro.Next.Infrastructure/Dossiers/DossierWordTemplateBuilder.cs` (ändern) | Vorlage nach dem Aufbau des Originals |
| `src/AuswertungPro.Next.Infrastructure/Dossiers/DossierWordTemplateExportService.cs` (ändern) | Eigentümerzeilen, Autoren, Bilder einsetzen |
| `src/AuswertungPro.Next.UI/Views/Windows/DossierEditWindow.xaml(.cs)` (ändern) | Eigentümertabelle, Übersichtsplan wählen |
| `src/AuswertungPro.Next.UI/Views/Windows/DossierAreaWindow.xaml(.cs)` (ändern) | Feld „Autoren" |
| `src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj` (ändern) | die zwei Bilddateien ins Ausgabeverzeichnis kopieren |
| `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierImageTests.cs` (neu) | Bildmasse und Bild-Einbettung |
| `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierMigrationTests.cs` (neu) | Umstellung Version 1 → 2 |
| `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierWordTests.cs` (ändern) | Eigentümerzeilen, Autoren, Bilder im Export |

---

### Task 1: Bildmasse aus PNG- und JPEG-Bytes lesen

Ohne WPF und ohne System.Drawing muss das Seitenverhältnis aus den Dateibytes kommen, sonst wird das Bild im Word verzerrt.

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Dossiers/ImageSizeReader.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierImageTests.cs`

**Interfaces:**
- Consumes: nichts
- Produces: `internal static class ImageSizeReader` mit
  `public static bool TryRead(ReadOnlySpan<byte> bytes, out int width, out int height)`.
  Der Test greift über `InternalsVisibleTo` **nicht** zu — die Klasse wird deshalb `public` deklariert (siehe Schritt 3), damit der Test sie direkt aufrufen kann.

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

Neue Datei `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierImageTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using AuswertungPro.Next.Infrastructure.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class ImageSizeReaderTests
{
    [Fact]
    public void Liest_Breite_und_Hoehe_eines_PNG()
    {
        var png = TestImages.Png(width: 716, height: 297);

        Assert.True(ImageSizeReader.TryRead(png, out var width, out var height));
        Assert.Equal(716, width);
        Assert.Equal(297, height);
    }

    [Fact]
    public void Liest_Breite_und_Hoehe_eines_JPEG()
    {
        var jpeg = TestImages.Jpeg(width: 177, height: 213);

        Assert.True(ImageSizeReader.TryRead(jpeg, out var width, out var height));
        Assert.Equal(177, width);
        Assert.Equal(213, height);
    }

    [Fact]
    public void Unbekannte_Bytes_ergeben_kein_Ergebnis_statt_geratener_Masse()
    {
        var muell = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        Assert.False(ImageSizeReader.TryRead(muell, out var width, out var height));
        Assert.Equal(0, width);
        Assert.Equal(0, height);
    }
}

/// <summary>
/// Baut die kleinsten gueltigen Bilddateien, die der Groessenleser verstehen
/// muss. Bewusst von Hand zusammengesetzt: die Testbibliothek hat keine
/// Bildbibliothek, und fuer die Kopfdaten braucht es auch keine.
/// </summary>
internal static class TestImages
{
    /// <summary>PNG-Signatur plus IHDR-Block mit Breite und Hoehe.</summary>
    public static byte[] Png(int width, int height)
    {
        var bytes = new List<byte>
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            (byte)'I', (byte)'H', (byte)'D', (byte)'R'
        };

        bytes.AddRange(BigEndian(width));
        bytes.AddRange(BigEndian(height));
        bytes.AddRange(new byte[] { 8, 6, 0, 0, 0 });
        return bytes.ToArray();
    }

    /// <summary>JPEG-Start plus ein SOF0-Segment mit Hoehe und Breite.</summary>
    public static byte[] Jpeg(int width, int height)
    {
        var bytes = new List<byte>
        {
            0xFF, 0xD8,
            // APP0-Segment mit 4 Nutzbytes: wird uebersprungen.
            0xFF, 0xE0, 0x00, 0x06, 1, 2, 3, 4,
            // SOF0: Laenge 17, Genauigkeit 8, dann Hoehe und Breite.
            0xFF, 0xC0, 0x00, 0x11, 0x08
        };

        bytes.Add((byte)(height >> 8));
        bytes.Add((byte)(height & 0xFF));
        bytes.Add((byte)(width >> 8));
        bytes.Add((byte)(width & 0xFF));
        bytes.AddRange(new byte[] { 3, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1 });
        return bytes.ToArray();
    }

    private static byte[] BigEndian(int value) => new[]
    {
        (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value
    };
}
```

- [ ] **Step 2: Test laufen lassen und Fehlschlag prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ImageSizeReaderTests"
```

Erwartet: Übersetzungsfehler `CS0103` / `The name 'ImageSizeReader' does not exist`.

- [ ] **Step 3: Die kleinste Umsetzung schreiben**

Neue Datei `src/AuswertungPro.Next.Infrastructure/Dossiers/ImageSizeReader.cs`:

```csharp
using System;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Liest Breite und Hoehe aus den Kopfdaten einer PNG- oder JPEG-Datei.
///
/// Die Infrastructure-Schicht ist reines net10.0 — es gibt hier weder WPF noch
/// System.Drawing. Fuer das Seitenverhaeltnis eines Bildes im Word reichen die
/// Kopfdaten aber vollstaendig aus.
///
/// Bei allem, was nicht sicher erkannt wird, wird NICHT geraten: ein falsches
/// Seitenverhaeltnis wuerde das Logo im fertigen Dossier verzerren.
/// </summary>
public static class ImageSizeReader
{
    private static readonly byte[] PngSignature =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static bool TryRead(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (TryReadPng(bytes, out width, out height))
            return true;

        return TryReadJpeg(bytes, out width, out height);
    }

    private static bool TryReadPng(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;

        // Signatur (8) + Blocklaenge (4) + "IHDR" (4) + Breite (4) + Hoehe (4)
        if (bytes.Length < 24)
            return false;

        for (var i = 0; i < PngSignature.Length; i++)
        {
            if (bytes[i] != PngSignature[i])
                return false;
        }

        if (bytes[12] != 'I' || bytes[13] != 'H' || bytes[14] != 'D' || bytes[15] != 'R')
            return false;

        width = ReadInt32(bytes, 16);
        height = ReadInt32(bytes, 20);
        return width > 0 && height > 0;
    }

    private static bool TryReadJpeg(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            return false;

        var index = 2;
        while (index + 9 < bytes.Length)
        {
            if (bytes[index] != 0xFF)
                return false;

            var marker = bytes[index + 1];
            var segmentLength = ReadUInt16(bytes, index + 2);
            if (segmentLength < 2)
                return false;

            if (IsStartOfFrame(marker))
            {
                height = ReadUInt16(bytes, index + 5);
                width = ReadUInt16(bytes, index + 7);
                return width > 0 && height > 0;
            }

            index += 2 + segmentLength;
        }

        return false;
    }

    /// <summary>
    /// Alle Bildanfangs-Marker. C4, C8 und CC sind ausgenommen: das sind
    /// Huffman-Tabellen und Erweiterungen, keine Bildmasse.
    /// </summary>
    private static bool IsStartOfFrame(byte marker)
        => marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset)
        => (bytes[offset] << 24) | (bytes[offset + 1] << 16)
           | (bytes[offset + 2] << 8) | bytes[offset + 3];

    private static int ReadUInt16(ReadOnlySpan<byte> bytes, int offset)
        => (bytes[offset] << 8) | bytes[offset + 1];
}
```

- [ ] **Step 4: Test laufen lassen und Erfolg prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ImageSizeReaderTests"
```

Erwartet: 3 Tests grün.

- [ ] **Step 5: Committen**

```bash
git add src/AuswertungPro.Next.Infrastructure/Dossiers/ImageSizeReader.cs tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierImageTests.cs
git commit -m "feat(dossier): Bildmasse aus PNG- und JPEG-Kopfdaten lesen"
```

---

### Task 2: Bild-Platzhalter im Word ersetzen

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Dossiers/DocxImagePlaceholderFiller.cs`
- Modify: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierImageTests.cs` (Testklasse anhängen)

**Interfaces:**
- Consumes: `ImageSizeReader.TryRead(ReadOnlySpan<byte>, out int, out int)` aus Task 1
- Produces:
  - `public sealed record DocxImagePlacement(string PlaceholderName, string ImagePath, double MaxWidthCm)`
  - `public static class DocxImagePlaceholderFiller` mit
    `public static void Fill(WordprocessingDocument document, IReadOnlyList<DocxImagePlacement> placements)`
  - `public const string MarkerPrefix = "{{@"` auf `DocxImagePlaceholderFiller`

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

An `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierImageTests.cs` anhängen:

```csharp
public sealed class DocxImagePlaceholderFillerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "dossier_bilder_" + Guid.NewGuid().ToString("N"));

    public DocxImagePlaceholderFillerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Ein Aufraeumfehler darf den Testlauf nicht rot machen.
        }
    }

    [Fact]
    public void Setzt_ein_Bild_ein_und_entfernt_den_Platzhalter()
    {
        var bildPfad = Path.Combine(_root, "logo.png");
        File.WriteAllBytes(bildPfad, TestImages.Png(width: 716, height: 297));

        using var stream = new MemoryStream();
        using (var document = CreateDocument(stream, "{{@Logo}}"))
        {
            DocxImagePlaceholderFiller.Fill(document, new[]
            {
                new DocxImagePlacement("Logo", bildPfad, MaxWidthCm: 4.5)
            });
            document.MainDocumentPart!.Document.Save();
        }

        stream.Position = 0;
        using var reopened = WordprocessingDocument.Open(stream, false);
        var mainPart = reopened.MainDocumentPart!;

        var text = string.Concat(
            mainPart.Document.Body!.Descendants<Text>().Select(t => t.Text));

        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
        Assert.Single(mainPart.ImageParts);
        Assert.NotEmpty(mainPart.Document.Body!.Descendants<Drawing>());
    }

    [Fact]
    public void Behaelt_das_Seitenverhaeltnis_des_Bildes()
    {
        var bildPfad = Path.Combine(_root, "logo.png");
        File.WriteAllBytes(bildPfad, TestImages.Png(width: 200, height: 100));

        using var stream = new MemoryStream();
        using (var document = CreateDocument(stream, "{{@Logo}}"))
        {
            // 2 cm breit, halb so hoch wie breit -> 1 cm hoch.
            DocxImagePlaceholderFiller.Fill(document, new[]
            {
                new DocxImagePlacement("Logo", bildPfad, MaxWidthCm: 2.0)
            });
            document.MainDocumentPart!.Document.Save();
        }

        stream.Position = 0;
        using var reopened = WordprocessingDocument.Open(stream, false);
        var extent = reopened.MainDocumentPart!.Document.Body!
            .Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>()
            .Single();

        Assert.Equal(720_000L, extent.Cx!.Value);
        Assert.Equal(360_000L, extent.Cy!.Value);
    }

    [Fact]
    public void Fehlende_Bilddatei_laesst_die_Stelle_leer_statt_den_Platzhalter_stehen()
    {
        var fehlt = Path.Combine(_root, "gibtesnicht.png");

        using var stream = new MemoryStream();
        using (var document = CreateDocument(stream, "Vorne {{@Logo}} hinten"))
        {
            DocxImagePlaceholderFiller.Fill(document, new[]
            {
                new DocxImagePlacement("Logo", fehlt, MaxWidthCm: 4.5)
            });
            document.MainDocumentPart!.Document.Save();
        }

        stream.Position = 0;
        using var reopened = WordprocessingDocument.Open(stream, false);
        var mainPart = reopened.MainDocumentPart!;

        var text = string.Concat(
            mainPart.Document.Body!.Descendants<Text>().Select(t => t.Text));

        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
        Assert.Contains("Vorne", text, StringComparison.Ordinal);
        Assert.Contains("hinten", text, StringComparison.Ordinal);
        Assert.Empty(mainPart.ImageParts);
    }

    [Fact]
    public void Findet_den_Platzhalter_auch_wenn_Word_ihn_zerlegt_hat()
    {
        var bildPfad = Path.Combine(_root, "wappen.jpg");
        File.WriteAllBytes(bildPfad, TestImages.Jpeg(width: 177, height: 213));

        using var stream = new MemoryStream();
        using (var document = new Func<WordprocessingDocument>(() =>
               {
                   var doc = WordprocessingDocument.Create(
                       stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
                   var part = doc.AddMainDocumentPart();
                   part.Document = new Document();
                   var body = part.Document.AppendChild(new Body());
                   var paragraph = body.AppendChild(new Paragraph());
                   paragraph.Append(
                       NewRun("{{@"), NewRun("Wap"), NewRun("pen"), NewRun("}}"));
                   return doc;
               })())
        {
            DocxImagePlaceholderFiller.Fill(document, new[]
            {
                new DocxImagePlacement("Wappen", bildPfad, MaxWidthCm: 2.0)
            });
            document.MainDocumentPart!.Document.Save();
        }

        stream.Position = 0;
        using var reopened = WordprocessingDocument.Open(stream, false);
        var mainPart = reopened.MainDocumentPart!;

        var text = string.Concat(
            mainPart.Document.Body!.Descendants<Text>().Select(t => t.Text));

        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
        Assert.Single(mainPart.ImageParts);
    }

    private static WordprocessingDocument CreateDocument(MemoryStream stream, string text)
    {
        var document = WordprocessingDocument.Create(
            stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());
        body.AppendChild(new Paragraph()).Append(NewRun(text));
        return document;
    }

    private static Run NewRun(string text)
        => new(new Text(text) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
}
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DocxImagePlaceholderFillerTests"
```

Erwartet: Übersetzungsfehler `The name 'DocxImagePlaceholderFiller' does not exist`.

- [ ] **Step 3: Die Umsetzung schreiben**

Neue Datei `src/AuswertungPro.Next.Infrastructure/Dossiers/DocxImagePlaceholderFiller.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>Ein Bild, das an die Stelle eines Platzhalters gehoert.</summary>
/// <param name="PlaceholderName">Name ohne Klammern, z.B. "Logo".</param>
/// <param name="ImagePath">Pfad zur Bilddatei.</param>
/// <param name="MaxWidthCm">Breite im Dokument in Zentimetern.</param>
public sealed record DocxImagePlacement(
    string PlaceholderName,
    string ImagePath,
    double MaxWidthCm);

/// <summary>
/// Ersetzt Platzhalter der Form <c>{{@Name}}</c> durch ein eingebettetes Bild.
///
/// Dasselbe Problem wie beim Textfueller: Word zerlegt den Platzhalter in
/// mehrere Textstuecke. Deshalb wird der Absatztext zuerst zusammengesetzt.
///
/// Wichtig ist die Reihenfolge im Aufrufer: dieser Fueller laeuft VOR
/// <see cref="DocxPlaceholderFiller"/>. Sonst wuerde der Textfueller den
/// Bildplatzhalter als unbekannten Textplatzhalter leeren, und das Bild fehlte
/// im fertigen Dossier ohne jede Meldung.
///
/// Fehlt die Bilddatei oder ist sie unlesbar, wird der Platzhalter trotzdem
/// entfernt: eine geschweifte Klammer darf der Eigentuemer nie zu sehen
/// bekommen.
/// </summary>
public static class DocxImagePlaceholderFiller
{
    /// <summary>Kennzeichnet einen Bildplatzhalter.</summary>
    public const string MarkerPrefix = "{{@";

    private const long EmuPerCm = 360_000L;

    public static void Fill(
        WordprocessingDocument document,
        IReadOnlyList<DocxImagePlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(placements);

        var mainPart = document.MainDocumentPart
            ?? throw new InvalidOperationException("Die Word-Vorlage hat keinen Hauptteil.");

        var body = mainPart.Document?.Body;
        if (body is null)
            return;

        var drawingId = 1U;

        foreach (var placement in placements)
        {
            if (string.IsNullOrWhiteSpace(placement.PlaceholderName))
                continue;

            var marker = MarkerPrefix + placement.PlaceholderName.Trim() + "}}";

            foreach (var paragraph in body.Descendants<Paragraph>().ToList())
            {
                var texts = paragraph.Descendants<Text>().ToList();
                if (texts.Count == 0)
                    continue;

                var combined = string.Concat(texts.Select(t => t.Text));
                if (!combined.Contains(marker, StringComparison.Ordinal))
                    continue;

                // Den Marker immer entfernen, auch wenn kein Bild folgt.
                for (var i = 1; i < texts.Count; i++)
                    texts[i].Text = string.Empty;

                texts[0].Text = combined.Replace(marker, string.Empty, StringComparison.Ordinal);
                texts[0].Space = SpaceProcessingModeValues.Preserve;

                var run = texts[0].Ancestors<Run>().FirstOrDefault();
                if (run is null)
                    continue;

                var drawing = TryCreateDrawing(mainPart, placement, drawingId);
                if (drawing is null)
                    continue;

                run.AppendChild(drawing);
                drawingId++;
            }
        }
    }

    /// <summary>
    /// Legt den Bildteil an und baut das Zeichnungselement. Liefert null, wenn
    /// die Datei fehlt, unlesbar ist oder ihre Masse nicht erkannt werden.
    /// </summary>
    private static Drawing? TryCreateDrawing(
        MainDocumentPart mainPart,
        DocxImagePlacement placement,
        uint drawingId)
    {
        byte[] bytes;
        try
        {
            if (string.IsNullOrWhiteSpace(placement.ImagePath) || !File.Exists(placement.ImagePath))
                return null;

            bytes = File.ReadAllBytes(placement.ImagePath);
        }
        catch (Exception ex)
        {
            // Ein unlesbares Logo darf das ganze Dossier nicht verhindern.
            BestEffort.ReportWarning(
                $"[Dossiers] Bild '{placement.ImagePath}' nicht lesbar: {ex.Message}");
            return null;
        }

        if (!ImageSizeReader.TryRead(bytes, out var pixelWidth, out var pixelHeight))
        {
            BestEffort.ReportWarning(
                $"[Dossiers] Bildmasse von '{placement.ImagePath}' nicht erkannt.");
            return null;
        }

        var partType = ResolvePartType(placement.ImagePath);
        if (partType is null)
            return null;

        var imagePart = mainPart.AddImagePart(partType.Value);
        using (var source = new MemoryStream(bytes))
            imagePart.FeedData(source);

        var relationshipId = mainPart.GetIdOfPart(imagePart);

        var widthEmu = (long)Math.Round(placement.MaxWidthCm * EmuPerCm);
        var heightEmu = (long)Math.Round(widthEmu * (double)pixelHeight / pixelWidth);

        return BuildDrawing(relationshipId, widthEmu, heightEmu, drawingId, placement.PlaceholderName);
    }

    private static ImagePartType? ResolvePartType(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => ImagePartType.Png,
            ".jpg" or ".jpeg" => ImagePartType.Jpeg,
            _ => null
        };

    private static Drawing BuildDrawing(
        string relationshipId,
        long widthEmu,
        long heightEmu,
        uint drawingId,
        string name) => new(
        new DW.Inline(
            new DW.Extent { Cx = widthEmu, Cy = heightEmu },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = drawingId, Name = name },
            new DW.NonVisualGraphicFrameDrawingProperties(
                new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(
                new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = 0U, Name = name },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(
                            new A.Blip { Embed = relationshipId },
                            new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(
                                new A.Offset { X = 0L, Y = 0L },
                                new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                            new A.PresetGeometry(new A.AdjustValueList())
                            {
                                Preset = A.ShapeTypeValues.Rectangle
                            })))
                {
                    Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture"
                }))
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U
        });
}
```

**Hinweis:** `BestEffort` liegt in `src/AuswertungPro.Next.Application/Common/BestEffort.cs`, Namensraum `AuswertungPro.Next.Application.Common`. Die neue Datei braucht deshalb zusätzlich `using AuswertungPro.Next.Application.Common;`.

- [ ] **Step 4: Tests laufen lassen und Erfolg prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DocxImagePlaceholderFillerTests"
```

Erwartet: 4 Tests grün.

- [ ] **Step 5: Committen**

```bash
git add src/AuswertungPro.Next.Infrastructure/Dossiers/DocxImagePlaceholderFiller.cs tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierImageTests.cs
git commit -m "feat(dossier): Bilder in Word-Platzhalter einsetzen"
```

---

### Task 3: Datenmodell und Umstellung auf Formatversion 2

**Files:**
- Modify: `src/AuswertungPro.Next.Domain/Models/Dossiers/DossierModels.cs`
- Create: `src/AuswertungPro.Next.Application/Dossiers/DossierDocumentMigration.cs`
- Modify: `src/AuswertungPro.Next.Infrastructure/Dossiers/DossierFileStore.cs:137-141`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierMigrationTests.cs`

**Interfaces:**
- Consumes: nichts
- Produces:
  - `DossierOwnerRow` mit `HouseNumber`, `ParcelNumber`, `Name`, `Phone`, `Mail`, `Occupancy` (alle `string`, Vorgabe `""`)
  - `DossierDefinition.Owners` : `List<DossierOwnerRow>`
  - `DossierDefinition.OverviewPlanPath` : `string`
  - `DossierAreaSettings.Authors` : `string`
  - `DossierDocument.CurrentSchemaVersion` : `const int = 2`
  - `public static class DossierDocumentMigration` mit
    `public static DossierDocument MigrateToCurrent(DossierDocument document)`

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

Neue Datei `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierMigrationTests.cs`:

```csharp
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierDocumentMigrationTests
{
    [Fact]
    public void Uebernimmt_den_bisherigen_einzelnen_Eigentuemer_in_die_erste_Zeile()
    {
        var document = new DossierDocument { SchemaVersion = 1 };
        document.Dossiers.Add(new DossierDefinition
        {
            HouseNumbers = "3",
            ParcelNumbers = "170",
            OwnerName = "Martin Muster",
            ContactPhone = "079 858 53 74",
            ContactMail = "markus@example.ch",
            Occupancy = "Einfamilienhaus"
        });

        var result = DossierDocumentMigration.MigrateToCurrent(document);

        var row = Assert.Single(result.Dossiers[0].Owners);
        Assert.Equal("3", row.HouseNumber);
        Assert.Equal("170", row.ParcelNumber);
        Assert.Equal("Martin Muster", row.Name);
        Assert.Equal("079 858 53 74", row.Phone);
        Assert.Equal("markus@example.ch", row.Mail);
        Assert.Equal("Einfamilienhaus", row.Occupancy);
        Assert.Equal(2, result.SchemaVersion);
    }

    [Fact]
    public void Nimmt_Eigentuemeradresse_und_Zustaendigkeit_mit_statt_sie_zu_verlieren()
    {
        var document = new DossierDocument { SchemaVersion = 1 };
        document.Dossiers.Add(new DossierDefinition
        {
            OwnerName = "Lubag AG",
            OwnerAddress = "Landenbergstrasse 34, 6005 Luzern",
            ContactName = "Sandro Sigrist"
        });

        var result = DossierDocumentMigration.MigrateToCurrent(document);

        var row = Assert.Single(result.Dossiers[0].Owners);
        Assert.Contains("Lubag AG", row.Name);
        Assert.Contains("Landenbergstrasse 34", row.Name);
        Assert.Contains("Zuständigkeit: Sandro Sigrist", row.Name);
    }

    [Fact]
    public void Ein_Dossier_ohne_Eigentuemerangaben_bekommt_keine_Leerzeile()
    {
        var document = new DossierDocument { SchemaVersion = 1 };
        document.Dossiers.Add(new DossierDefinition { Name = "Nur ein Name" });

        var result = DossierDocumentMigration.MigrateToCurrent(document);

        Assert.Empty(result.Dossiers[0].Owners);
    }

    [Fact]
    public void Bereits_vorhandene_Zeilen_werden_nicht_angetastet()
    {
        var document = new DossierDocument { SchemaVersion = 2 };
        var dossier = new DossierDefinition { OwnerName = "Alt" };
        dossier.Owners.Add(new DossierOwnerRow { Name = "Neu" });
        document.Dossiers.Add(dossier);

        var result = DossierDocumentMigration.MigrateToCurrent(document);

        var row = Assert.Single(result.Dossiers[0].Owners);
        Assert.Equal("Neu", row.Name);
    }
}
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DossierDocumentMigrationTests"
```

Erwartet: Übersetzungsfehler `DossierOwnerRow` und `DossierDocumentMigration` unbekannt.

- [ ] **Step 3: Datenmodell erweitern**

In `src/AuswertungPro.Next.Domain/Models/Dossiers/DossierModels.cs`:

Vor `DossierDefinition` einfügen:

```csharp
/// <summary>
/// Eine Zeile der Tabelle "Eigentumsverhaeltnisse". Eine Liegenschaft kann
/// mehrere haben — Stockwerkeigentum, Doppelhaus, mehrere Hausnummern.
/// </summary>
public sealed class DossierOwnerRow
{
    public string HouseNumber { get; set; } = "";
    public string ParcelNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Mail { get; set; } = "";

    /// <summary>Objektbewohner, z.B. "Mehrfamilienhaus".</summary>
    public string Occupancy { get; set; } = "";
}
```

In `DossierAreaSettings` nach `FooterLine` einfügen:

```csharp
    /// <summary>
    /// Autoren fuer die Zeile "Autoren:" auf Seite 2. Bleibt das Feld leer,
    /// nimmt die Ausgabe den Windows-Benutzernamen — der heisst aber je nach
    /// Rechner "Besitzer" und gehoert nicht in ein Dokument fuer den Eigentuemer.
    /// </summary>
    public string Authors { get; set; } = "";
```

In `DossierDefinition` nach `Occupancy` einfügen:

```csharp
    /// <summary>
    /// Die Zeilen der Tabelle "Eigentumsverhaeltnisse". Die Einzelfelder oben
    /// bleiben bestehen: sie speisen weiterhin das Deckblatt.
    /// </summary>
    public List<DossierOwnerRow> Owners { get; set; } = new();

    /// <summary>Bilddatei des Uebersichtsplans fuer Kapitel 1.</summary>
    public string OverviewPlanPath { get; set; } = "";
```

In `DossierDocument`:

```csharp
    /// <summary>Formatversion, die diese Programmversion schreibt.</summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>Formatversion. Unbekannt hoehere Versionen werden nicht ueberschrieben.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
```

- [ ] **Step 4: Umstellung schreiben**

Neue Datei `src/AuswertungPro.Next.Application/Dossiers/DossierDocumentMigration.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Stellt ein gespeichertes Dossier-Dokument auf die aktuelle Formatversion um.
/// Reine Logik ohne Dateisystem.
///
/// Version 1 kannte je Liegenschaft genau einen Eigentuemer in Einzelfeldern.
/// Version 2 hat eine Zeilenliste. Die Einzelfelder bleiben erhalten — sie
/// speisen weiterhin das Deckblatt, und ein Wegwerfen waere Datenverlust.
/// </summary>
public static class DossierDocumentMigration
{
    public static DossierDocument MigrateToCurrent(DossierDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Area ??= new DossierAreaSettings();
        document.Dossiers ??= new List<DossierDefinition>();

        foreach (var dossier in document.Dossiers)
        {
            dossier.Owners ??= new List<DossierOwnerRow>();

            // Wer schon Zeilen hat, wird nicht angefasst.
            if (dossier.Owners.Count > 0)
                continue;

            var row = BuildRowFromLegacyFields(dossier);
            if (row is not null)
                dossier.Owners.Add(row);
        }

        document.SchemaVersion = DossierDocument.CurrentSchemaVersion;
        return document;
    }

    /// <summary>
    /// Liefert null, wenn in den Altfeldern nichts steht — eine leere Zeile in
    /// der Tabelle waere schlechter als gar keine.
    /// </summary>
    private static DossierOwnerRow? BuildRowFromLegacyFields(DossierDefinition dossier)
    {
        var name = JoinInline(dossier.OwnerName, dossier.OwnerAddress);

        if (!string.IsNullOrWhiteSpace(dossier.ContactName))
        {
            var responsibility = "Zuständigkeit: " + dossier.ContactName.Trim();
            name = name.Length == 0 ? responsibility : name + "\n" + responsibility;
        }

        var row = new DossierOwnerRow
        {
            HouseNumber = Trim(dossier.HouseNumbers),
            ParcelNumber = Trim(dossier.ParcelNumbers),
            Name = name,
            Phone = Trim(dossier.ContactPhone),
            Mail = Trim(dossier.ContactMail),
            Occupancy = Trim(dossier.Occupancy)
        };

        var hasContent =
            row.HouseNumber.Length > 0 || row.ParcelNumber.Length > 0 || row.Name.Length > 0
            || row.Phone.Length > 0 || row.Mail.Length > 0 || row.Occupancy.Length > 0;

        return hasContent ? row : null;
    }

    private static string Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string JoinInline(params string?[] parts)
        => string.Join(
            " ",
            parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
}
```

- [ ] **Step 5: Dateispeicher an die neue Version anpassen**

In `src/AuswertungPro.Next.Infrastructure/Dossiers/DossierFileStore.cs`, Methode `ReadAsync`, den Block ab Zeile 137 ersetzen:

```csharp
        // Ein neueres Format als bekannt: nicht raten, sondern melden. Ein
        // stiller Weiterlauf wuerde beim naechsten Speichern Felder verlieren.
        if (document.SchemaVersion > DossierDocument.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"'{path}' hat Formatversion {document.SchemaVersion}. "
                + $"Diese Programmversion kennt nur Version {DossierDocument.CurrentSchemaVersion}.");
        }

        // Aeltere Staende werden beim Laden umgestellt; gespeichert wird erst,
        // wenn Pascal wirklich etwas aendert.
        return DossierDocumentMigration.MigrateToCurrent(document);
```

Die beiden Zeilen `document.Area ??= ...` und `document.Dossiers ??= ...` entfallen, weil die Umstellung sie übernimmt. `using AuswertungPro.Next.Application.Dossiers;` ist in der Datei bereits vorhanden (`IDossierStore`); sonst ergänzen.

- [ ] **Step 6: Den bestehenden Versionstest nachziehen**

`tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierStoreAndAssemblyTests.cs:43`
prüft heute `Assert.Equal(1, document.SchemaVersion);`. Ein frisches Dokument trägt jetzt
Version 2. Die Zeile ersetzen durch:

```csharp
        Assert.Equal(DossierDocument.CurrentSchemaVersion, document.SchemaVersion);
```

Der Test `Neuere_Formatversion_wird_nicht_erraten` (Zeile 129) verwendet Version 99 und
bleibt damit unverändert gültig — er muss weiterhin fehlschlagen lassen.

- [ ] **Step 7: Tests laufen lassen und Erfolg prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Dossier"
```

Erwartet: die 4 neuen Umstellungstests grün, alle bisherigen Dossier-Tests weiterhin grün —
insbesondere `Erstlauf_ohne_Datei_ergibt_ein_leeres_Dokument` und
`Neuere_Formatversion_wird_nicht_erraten`.

- [ ] **Step 8: Committen**

```bash
git add src/AuswertungPro.Next.Domain/Models/Dossiers/DossierModels.cs src/AuswertungPro.Next.Application/Dossiers/DossierDocumentMigration.cs src/AuswertungPro.Next.Infrastructure/Dossiers/DossierFileStore.cs tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierMigrationTests.cs tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierStoreAndAssemblyTests.cs
git commit -m "feat(dossier): Eigentuemerzeilen, Planbild und Autoren im Datenmodell"
```

---

### Task 4: Vorlage auf den Aufbau des Originals umbauen

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Dossiers/DossierWordTemplateBuilder.cs`
- Modify: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierWordTests.cs:322-368` (Klasse `DossierWordTemplateBuilderTests`)

**Interfaces:**
- Consumes: `DocxImagePlaceholderFiller.MarkerPrefix` (nur als Formatvorgabe `{{@Name}}`)
- Produces: Vorlage mit den Platzhaltern `{{@Logo}}`, `{{@Wappen}}`, `{{@Uebersichtsplan}}`, `{{Autoren}}`, `{{#Eigentuemer}}`, `{{Haus_Nr}}`, `{{Pz_Nr}}`, `{{Eigentuemer_Zelle}}` — die Namen verwendet Task 5 wörtlich.

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

In `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierWordTests.cs` die Klasse `DossierWordTemplateBuilderTests` ersetzen durch:

```csharp
public sealed class DossierWordTemplateBuilderTests
{
    [Fact]
    public void Die_Vorlage_ist_eine_gueltige_Word_Datei_mit_allen_Platzhaltern()
    {
        var text = ReadTemplateText();

        foreach (var expected in new[]
                 {
                     "{{Gebietstitel}}", "{{Parzellen_Zeile}}", "{{Eigentuemer_Block}}",
                     "{{Revision}}", "{{Datum}}", "{{Autoren}}",
                     "{{@Logo}}", "{{@Wappen}}", "{{@Uebersichtsplan}}",
                     "{{#Eigentuemer}}", "{{Haus_Nr}}", "{{Pz_Nr}}", "{{Eigentuemer_Zelle}}",
                     "{{#Haltungen}}",
                     "{{Ausfuehrungstermin}}", "{{Hausanschluss}}", "{{Meteorwasser}}",
                     "{{Rueckmeldung}}"
                 })
        {
            Assert.Contains(expected, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Die_Vorlage_traegt_die_vier_Kapitel_des_Vorbilds()
    {
        var text = ReadTemplateText();

        Assert.Contains("Eigentümerdossier", text, StringComparison.Ordinal);
        Assert.Contains("1.  Übersichtsplan Werkleitungen", text, StringComparison.Ordinal);
        Assert.Contains("2.  Eigentumsverhältnisse", text, StringComparison.Ordinal);
        Assert.Contains("3.  Betroffene Abwasserleitungen", text, StringComparison.Ordinal);
        Assert.Contains("4.  Informationen Sanierung", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Rueckmeldung_steht_in_der_Info_Tabelle_und_nicht_als_eigenes_Kapitel()
    {
        var text = ReadTemplateText();

        Assert.Contains("Rückmeldung / Einverständnis Eigentümer", text, StringComparison.Ordinal);
        Assert.DoesNotContain("5.  Rückmeldung", text, StringComparison.Ordinal);
        Assert.Contains("Unterschrift(en)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Das_Deckblatt_traegt_keinen_Logo_Hinweistext_mehr()
    {
        var text = ReadTemplateText();

        Assert.DoesNotContain("Logo hier einfügen", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{Logo_Hinweis}}", text, StringComparison.Ordinal);
    }

    private static string ReadTemplateText()
    {
        var bytes = DossierWordTemplateBuilder.Build();

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);

        return string.Concat(
            document.MainDocumentPart!.Document.Body!
                .Descendants<Text>()
                .Select(t => t.Text));
    }
}
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DossierWordTemplateBuilderTests"
```

Erwartet: FAIL, weil `{{@Logo}}`, `{{#Eigentuemer}}` und `3.  Betroffene Abwasserleitungen` fehlen.

- [ ] **Step 3: Deckblatt umbauen**

In `DossierWordTemplateBuilder.BuildCoverPage`, die Zeile

```csharp
        cell.AppendChild(Paragraph("{{Logo_Hinweis}}", size: 18, color: HintColor, italic: true));
```

ersetzen durch:

```csharp
        // Logo links, Wappen rechts — die Anordnung des Vorbilds. Beide Bilder
        // sind fest mitgeliefert und stecken in jedem Dossier.
        var brandRow = new Table();
        brandRow.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            NoBorders()));
        brandRow.AppendChild(new TableGrid(
            new GridColumn { Width = "6000" }, new GridColumn { Width = "3000" }));
        brandRow.AppendChild(new TableRow(
            BorderlessCell(Paragraph("{{@Logo}}", size: 18)),
            BorderlessCell(Paragraph(
                "{{@Wappen}}", size: 18, alignment: JustificationValues.Right))));
        cell.AppendChild(brandRow);
```

- [ ] **Step 4: Seite 2 und Inhaltsverzeichnis anpassen**

In `BuildChangeLogPage` die Zeile

```csharp
        meta.AppendChild(BorderlessRow("Autoren:", "{{Autor}}"));
```

ersetzen durch:

```csharp
        meta.AppendChild(BorderlessRow("Autoren:", "{{Autoren}}"));
```

und den Inhaltsverzeichnis-Block

```csharp
        toc.AppendChild(BorderlessRow("1.", "Übersichtsplan Werkleitungen"));
        toc.AppendChild(BorderlessRow("2.", "Eigentumsverhältnisse"));
        toc.AppendChild(BorderlessRow("3.", "Betroffene Abwasserleitungen"));
        toc.AppendChild(BorderlessRow("4.", "Informationen Sanierung"));
        toc.AppendChild(BorderlessRow("5.", "Rückmeldung / Einverständnis"));
```

ersetzen durch:

```csharp
        toc.AppendChild(BorderlessRow("1.", "Übersichtsplan Werkleitungen"));
        toc.AppendChild(BorderlessRow("2.", "Eigentumsverhältnisse"));
        toc.AppendChild(BorderlessRow("3.", "Betroffene Abwasserleitungen"));
        toc.AppendChild(BorderlessRow("4.", "Informationen Sanierung"));
```

- [ ] **Step 5: Kapitel 1 auf das Planbild umstellen**

In `BuildOverviewPlanPage` den Hinweisabsatz

```csharp
        body.AppendChild(Paragraph(
            "[Hier den Übersichtsplan einfügen: Register „Einfügen\" → „Bilder\". "
            + "Diesen Hinweis danach löschen.]",
            size: 20, color: HintColor, italic: true));
```

ersetzen durch:

```csharp
        // Das Planbild waehlt Pascal je Liegenschaft im Programm; hier steht
        // nur die Stelle, an die es kommt.
        body.AppendChild(Paragraph(
            "{{@Uebersichtsplan}}", size: 20, alignment: JustificationValues.Center));
```

- [ ] **Step 6: Kapitel 2 auf Wiederholzeilen umstellen**

In `BuildContentPages` den Block

```csharp
        var owner = NewTable(1400, 1400, 6200);
        owner.AppendChild(HeaderRow("Haus Nr.", "Pz. Nr.", "Eigentümer"));
        owner.AppendChild(BodyRow(
            "{{Hausnummern}}",
            "{{Parzellen}}",
            "{{Eigentuemer_Detail}}"));
        body.AppendChild(owner);
```

ersetzen durch:

```csharp
        var owner = NewTable(1400, 1400, 6200);
        owner.AppendChild(HeaderRow("Haus Nr.", "Pz. Nr.", "Eigentümer"));
        owner.AppendChild(BodyRow(
            "{{#Eigentuemer}}{{Haus_Nr}}",
            "{{Pz_Nr}}",
            "{{Eigentuemer_Zelle}}"));
        body.AppendChild(owner);
```

- [ ] **Step 7: Kapitel 5 in die Info-Tabelle holen**

In `BuildContentPages` die Zeile `info.AppendChild(BodyRow("Beilagen", "{{Beilagen}}"));` beibehalten und **direkt danach** einfügen:

```csharp
        info.AppendChild(ResponseRow());
```

Danach den gesamten Block ab

```csharp
        body.AppendChild(EmptyParagraph());
        body.AppendChild(EmptyParagraph());

        // 5. Rueckmeldung
        body.AppendChild(Paragraph("5.  Rückmeldung / Einverständnis Eigentümer", size: 24, bold: true));
```

bis zum Ende der Methode (einschliesslich `body.AppendChild(response);`) löschen. `body.AppendChild(info);` bleibt die letzte Anweisung der Methode.

Neuen Baustein bei den übrigen Zeilen-Bausteinen einfügen (nach `BodyRow`):

```csharp
    /// <summary>
    /// Die letzte Zeile der Info-Tabelle: Rueckmeldung samt Unterschriftslinien.
    /// Im Vorbild ist das keine eigene Seite, sondern eine Tabellenzeile.
    /// </summary>
    private static TableRow ResponseRow()
    {
        var row = new TableRow();

        var label = new TableCell();
        label.AppendChild(new TableCellProperties(
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
        label.AppendChild(Paragraph("Rückmeldung / Einverständnis Eigentümer", size: 18));
        row.AppendChild(label);

        var content = new TableCell();
        content.AppendChild(new TableCellProperties(
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
        content.AppendChild(Paragraph("{{Rueckmeldung}}", size: 18));
        content.AppendChild(EmptyParagraph());

        var signatureLines = NewTable(4400, 4400, borders: false);
        signatureLines.AppendChild(BorderlessRow(
            "..............................................",
            ".............................................."));
        signatureLines.AppendChild(BorderlessRow("Ort/Datum", "Unterschrift(en)"));
        content.AppendChild(signatureLines);
        content.AppendChild(EmptyParagraph());

        row.AppendChild(content);
        return row;
    }
```

- [ ] **Step 8: Tests laufen lassen und Erfolg prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DossierWordTemplateBuilderTests"
```

Erwartet: 4 Tests grün.

Falls die Übersetzung `HintColor` als ungenutzt meldet: die Konstante bleibt bestehen, sie wird von anderen Stellen verwendet. Nur wenn der Übersetzer eine Warnung wirft, das Feld entfernen.

- [ ] **Step 9: Committen**

```bash
git add src/AuswertungPro.Next.Infrastructure/Dossiers/DossierWordTemplateBuilder.cs tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierWordTests.cs
git commit -m "feat(dossier): Vorlage nach dem Aufbau des Originals"
```

---

### Task 5: Export füllt Eigentümerzeilen, Autoren und Bilder

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Dossiers/DossierWordTemplateExportService.cs`
- Modify: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierWordTests.cs` (Klasse `DossierWordTemplateExportServiceTests`)

**Interfaces:**
- Consumes: `DocxImagePlaceholderFiller.Fill(...)`, `DocxImagePlacement` (Task 2); `DossierOwnerRow`, `DossierAreaSettings.Authors`, `DossierDefinition.Owners`, `DossierDefinition.OverviewPlanPath` (Task 3); Platzhalternamen aus Task 4
- Produces:
  - `public static List<IReadOnlyDictionary<string, string>> BuildOwnerRows(DossierDefinition dossier)`
  - `public const string LogoFileName = "Dossier_Logo.png"`
  - `public const string CoatOfArmsFileName = "Dossier_Wappen.png"`

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

In `DossierWordTemplateExportServiceTests` diese Tests anhängen:

```csharp
    [Fact]
    public async Task Schreibt_jede_Eigentuemerzeile_als_eigene_Tabellenzeile()
    {
        var (request, templatePath) = BuildScenario();
        request.Dossier.Owners.Clear();
        request.Dossier.Owners.Add(new DossierOwnerRow
        {
            HouseNumber = "3",
            ParcelNumber = "170",
            Name = "Martin Muster",
            Phone = "079 858 53 74",
            Occupancy = "Einfamilienhaus"
        });
        request.Dossier.Owners.Add(new DossierOwnerRow
        {
            HouseNumber = "4",
            ParcelNumber = "171",
            Name = "Anna Gisler",
            Mail = "anna.gisler@example.ch"
        });

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        var text = ReadDocumentText(result.FilePath!);

        Assert.Contains("Martin Muster", text, StringComparison.Ordinal);
        Assert.Contains("Anna Gisler", text, StringComparison.Ordinal);
        Assert.Contains("Tel.: 079 858 53 74", text, StringComparison.Ordinal);
        Assert.Contains("Objektbewohner: Einfamilienhaus", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Eigentuemerzeile_bleibt_ein_klarer_Hinweis_statt_eines_Platzhalters()
    {
        var (request, templatePath) = BuildScenario();
        request.Dossier.Owners.Clear();

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        var text = ReadDocumentText(result.FilePath!);

        Assert.Contains("Keine Eigentümerangaben erfasst", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nimmt_die_Autoren_des_Gebiets_statt_des_Windows_Benutzers()
    {
        var (request, templatePath) = BuildScenario();
        request.Area.Authors = "Pascal Aschwanden/";

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        var text = ReadDocumentText(result.FilePath!);

        Assert.Contains("Pascal Aschwanden/", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setzt_Logo_Wappen_und_Uebersichtsplan_als_Bilder_ein()
    {
        var (request, templatePath) = BuildScenario();

        // Logo und Wappen liegen neben der Vorlage.
        var vorlagenOrdner = Path.GetDirectoryName(templatePath)!;
        File.WriteAllBytes(
            Path.Combine(vorlagenOrdner, DossierWordTemplateExportService.LogoFileName),
            TestImages.Png(716, 297));
        File.WriteAllBytes(
            Path.Combine(vorlagenOrdner, DossierWordTemplateExportService.CoatOfArmsFileName),
            TestImages.Png(407, 491));

        var planPfad = Path.Combine(_root, "plan.png");
        File.WriteAllBytes(planPfad, TestImages.Png(1200, 1600));
        request.Dossier.OverviewPlanPath = planPfad;

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        Assert.True(result.Success, result.Message);

        using var document = WordprocessingDocument.Open(result.FilePath!, false);
        Assert.Equal(3, document.MainDocumentPart!.ImageParts.Count());

        var text = ReadDocumentText(result.FilePath!);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fehlende_Bilder_erzeugen_trotzdem_ein_vollstaendiges_Dossier()
    {
        var (request, templatePath) = BuildScenario();
        request.Dossier.OverviewPlanPath = Path.Combine(_root, "gibtesnicht.png");

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        Assert.True(result.Success, result.Message);

        var text = ReadDocumentText(result.FilePath!);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
        Assert.Contains("Erstfeld West", text, StringComparison.Ordinal);
    }
```

Ausserdem in `BuildScenario` nach `Occupancy = "Mehrfamilienhaus",` die Zeile

```csharp
            ConstructionProcess = "Leitung 1 und 7 mittels Inliner sanieren."
```

so ergänzen, dass danach eine Eigentümerzeile gesetzt wird — direkt nach der Objektzuweisung `var dossier = new DossierDefinition { ... };` einfügen:

```csharp
        dossier.Owners.Add(new DossierOwnerRow
        {
            HouseNumber = "3+4+7+8",
            ParcelNumber = "762+756",
            Name = "Lubag AG Landenbergstrasse 34, 6005 Luzern",
            Phone = "041 360 00 50",
            Mail = "sandro.sigrist@lubag.ch",
            Occupancy = "Mehrfamilienhaus"
        });
```

`TestImages` liegt im selben Namensraum (`AuswertungPro.Next.Infrastructure.Tests.Dossiers`) und ist ohne zusätzlichen `using` erreichbar. Ergänze in der Datei den `using System.Linq;` (für `ImageParts.Count()`), falls er fehlt.

- [ ] **Step 2: Tests laufen lassen und Fehlschlag prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DossierWordTemplateExportServiceTests"
```

Erwartet: FAIL — `LogoFileName` unbekannt, Eigentümerzeilen erscheinen nicht.

- [ ] **Step 3: Die Umsetzung schreiben**

In `DossierWordTemplateExportService`:

Konstanten direkt nach `private static readonly CultureInfo Ch = ...` einfügen:

```csharp
    /// <summary>Das feste Firmenlogo, ausgeliefert neben der Word-Vorlage.</summary>
    public const string LogoFileName = "Dossier_Logo.png";

    /// <summary>Das feste Wappen, ausgeliefert neben der Word-Vorlage.</summary>
    public const string CoatOfArmsFileName = "Dossier_Wappen.png";
```

In `ExportAsync` den Füllblock

```csharp
                using (var document = WordprocessingDocument.Open(tempPath, isEditable: true))
                {
                    DocxPlaceholderFiller.FillRepeatingRows(
                        document,
                        "Haltungen",
                        BuildHoldingRows(request.Snapshot),
                        "Keine Leitungen zugeordnet");

                    DocxPlaceholderFiller.Fill(document, BuildValues(request));
                    document.MainDocumentPart?.Document?.Save();
                }
```

ersetzen durch:

```csharp
                using (var document = WordprocessingDocument.Open(tempPath, isEditable: true))
                {
                    DocxPlaceholderFiller.FillRepeatingRows(
                        document,
                        "Haltungen",
                        BuildHoldingRows(request.Snapshot),
                        "Keine Leitungen zugeordnet");

                    DocxPlaceholderFiller.FillRepeatingRows(
                        document,
                        "Eigentuemer",
                        BuildOwnerRows(request.Dossier),
                        "Keine Eigentümerangaben erfasst");

                    // Bilder VOR dem Textfueller: sonst wuerde der Textfueller
                    // "{{@Logo}}" als unbekannten Textplatzhalter leeren und das
                    // Bild fehlte im fertigen Dossier ohne jede Meldung.
                    DocxImagePlaceholderFiller.Fill(
                        document, BuildImagePlacements(request, templatePath));

                    DocxPlaceholderFiller.Fill(document, BuildValues(request));
                    document.MainDocumentPart?.Document?.Save();
                }
```

In `BuildValues` die Zeile

```csharp
            ["Logo_Hinweis"] = string.IsNullOrWhiteSpace(request.Area.LogoPath)
                ? "[Logo hier einfügen]"
                : string.Empty,
```

ersatzlos löschen und die Zeile

```csharp
            ["Autor"] = Environment.UserName,
```

ersetzen durch:

```csharp
            ["Autoren"] = string.IsNullOrWhiteSpace(request.Area.Authors)
                ? Environment.UserName
                : request.Area.Authors.Trim(),
```

Neue Methoden nach `BuildHoldingRows` einfügen:

```csharp
    /// <summary>
    /// Die Zeilen der Tabelle "Eigentumsverhaeltnisse". Oeffentlich, damit sie
    /// testbar sind.
    /// </summary>
    public static List<IReadOnlyDictionary<string, string>> BuildOwnerRows(
        DossierDefinition dossier)
    {
        ArgumentNullException.ThrowIfNull(dossier);

        var rows = new List<IReadOnlyDictionary<string, string>>();

        foreach (var owner in dossier.Owners)
        {
            rows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Haus_Nr"] = Clean(owner.HouseNumber),
                ["Pz_Nr"] = Clean(owner.ParcelNumber),
                ["Eigentuemer_Zelle"] = BuildOwnerCell(owner)
            });
        }

        return rows;
    }

    /// <summary>
    /// Der mehrzeilige Inhalt der Eigentuemerzelle — dieselbe Aufteilung wie im
    /// Vorbild. Leere Angaben erzeugen keine leere Beschriftungszeile.
    /// </summary>
    private static string BuildOwnerCell(DossierOwnerRow owner)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(owner.Name))
            parts.Add(owner.Name.Trim());

        if (!string.IsNullOrWhiteSpace(owner.Phone))
            parts.Add("Tel.: " + owner.Phone.Trim());

        if (!string.IsNullOrWhiteSpace(owner.Mail))
            parts.Add("Mail: " + owner.Mail.Trim());

        if (!string.IsNullOrWhiteSpace(owner.Occupancy))
            parts.Add("Objektbewohner: " + owner.Occupancy.Trim());

        return string.Join("\n", parts);
    }

    /// <summary>
    /// Logo und Wappen liegen fest neben der Word-Vorlage; der Uebersichtsplan
    /// gehoert zur einzelnen Liegenschaft. Ein relativer Planpfad wird am
    /// Projektordner aufgeloest.
    /// </summary>
    private static List<DocxImagePlacement> BuildImagePlacements(
        DossierExportRequest request,
        string templatePath)
    {
        var placements = new List<DocxImagePlacement>();
        var templateFolder = Path.GetDirectoryName(templatePath);

        if (!string.IsNullOrWhiteSpace(templateFolder))
        {
            placements.Add(new DocxImagePlacement(
                "Logo", Path.Combine(templateFolder, LogoFileName), MaxWidthCm: 4.5));
            placements.Add(new DocxImagePlacement(
                "Wappen", Path.Combine(templateFolder, CoatOfArmsFileName), MaxWidthCm: 2.0));
        }

        var plan = ResolvePlanPath(request);
        if (plan is not null)
            placements.Add(new DocxImagePlacement("Uebersichtsplan", plan, MaxWidthCm: 15.0));

        return placements;
    }

    private static string? ResolvePlanPath(DossierExportRequest request)
    {
        var configured = request.Dossier.OverviewPlanPath;
        if (string.IsNullOrWhiteSpace(configured))
            return null;

        try
        {
            return Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(request.ProjectRoot, configured);
        }
        catch
        {
            // Ein unsinniger Pfad darf das Dossier nicht verhindern; die Stelle
            // bleibt dann leer.
            return null;
        }
    }

    private static string Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
```

Da `BuildImagePlacements` den Vorlagenpfad braucht, muss `templatePath` in `ExportAsync` bereits als lokale Variable vorliegen — das ist ab Zeile 49 der Fall.

Die Methoden `BuildOwnerDetail` und die Platzhalter `Eigentuemer_Detail`, `Hausnummern`, `Parzellen` bleiben unverändert bestehen: `Parzellen_Zeile` und `Eigentuemer_Block` speisen weiterhin das Deckblatt, und ein unbenutzter Platzhalterwert schadet nicht.

- [ ] **Step 4: Deckblatt-Namen aus den Zeilen speisen**

In `BuildValues` die Zeile

```csharp
            ["Eigentuemer_Block"] = JoinLines(d.OwnerName, d.OwnerAddress),
```

ersetzen durch:

```csharp
            ["Eigentuemer_Block"] = BuildCoverOwnerBlock(d),
```

und diese Methode nach `BuildOwnerCell` einfügen:

```csharp
    /// <summary>
    /// Auf dem Deckblatt stehen die Namen aller Eigentuemerzeilen untereinander.
    /// Gibt es keine Zeile, gilt weiterhin die alte Einzelangabe.
    /// </summary>
    private static string BuildCoverOwnerBlock(DossierDefinition dossier)
    {
        var names = dossier.Owners
            .Select(owner => owner.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Split('\n')[0].Trim())
            .ToList();

        return names.Count > 0
            ? string.Join("\n", names)
            : JoinLines(dossier.OwnerName, dossier.OwnerAddress);
    }
```

- [ ] **Step 5: Tests laufen lassen und Erfolg prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Dossier"
```

Erwartet: alle Dossier-Tests grün, einschliesslich der fünf neuen.

- [ ] **Step 6: Committen**

```bash
git add src/AuswertungPro.Next.Infrastructure/Dossiers/DossierWordTemplateExportService.cs tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierWordTests.cs
git commit -m "feat(dossier): Eigentuemerzeilen, Autoren und Bilder in der Word-Ausgabe"
```

---

### Task 6: Logo und Wappen fest ausliefern

Pascal hat die beiden Bilder direkt geliefert — sauberer als die Auszüge aus dem PDF (dort klebte am Logo noch ein grauer Kasten). Es wird nichts aus dem PDF gezogen; `tools/PdfImageAnalyzer` bleibt unverändert.

**Quellen:**

- `C:\Users\Besitzer\Downloads\Abwasser Uri.png` — 697 × 286, Querformat, das Firmenlogo
- `C:\Users\Besitzer\Downloads\Uri Wappen.png` — 407 × 491, Hochformat, das Urner Wappen

**Files:**

- Create: `Export_Vorlage/Dossier_Logo.png`, `Export_Vorlage/Dossier_Wappen.png`
- Modify: `src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj:114-117`
- Modify: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierImageTests.cs`

**Interfaces:**

- Consumes: `DossierWordTemplateExportService.LogoFileName` = `"Dossier_Logo.png"` und `.CoatOfArmsFileName` = `"Dossier_Wappen.png"` aus Task 5 — die Dateinamen müssen wörtlich stimmen
- Produces: die zwei Bilddateien im Ausgabeverzeichnis neben `Eigentuemerdossier.docx`

- [ ] **Step 1: Die Dateien an ihren Platz kopieren**

```bash
cp "C:/Users/Besitzer/Downloads/Abwasser Uri.png" Export_Vorlage/Dossier_Logo.png
cp "C:/Users/Besitzer/Downloads/Uri Wappen.png" Export_Vorlage/Dossier_Wappen.png
ls -la Export_Vorlage/Dossier_*
```

Erwartet: zwei Dateien, rund 8 KB (Logo) und rund 16 KB (Wappen).

- [ ] **Step 2: Ausliefern**

In `src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj` nach dem Block für `Eigentuemerdossier.docx` einfügen:

```xml
    <None Include="..\..\Export_Vorlage\Dossier_Logo.png" Link="Export_Vorlage\Dossier_Logo.png">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    </None>
    <None Include="..\..\Export_Vorlage\Dossier_Wappen.png" Link="Export_Vorlage\Dossier_Wappen.png">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    </None>
```

- [ ] **Step 3: Test schreiben, dass die Dateien im Quellbestand liegen**

An `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierImageTests.cs` anhängen:

```csharp
public sealed class AusgelieferteDossierBilderTests
{
    [Fact]
    public void Logo_und_Wappen_liegen_im_Vorlagenordner_und_sind_lesbare_Bilder()
    {
        var wurzel = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);

        Assert.NotNull(wurzel);

        var logo = Path.Combine(wurzel!, "Export_Vorlage", "Dossier_Logo.png");
        var wappen = Path.Combine(wurzel!, "Export_Vorlage", "Dossier_Wappen.png");

        Assert.True(File.Exists(logo), $"'{logo}' fehlt.");
        Assert.True(File.Exists(wappen), $"'{wappen}' fehlt.");

        // Die Masse belegen zugleich, dass die beiden Dateien nicht vertauscht
        // sind: das Logo ist breiter als hoch, das Wappen hoeher als breit.
        Assert.True(ImageSizeReader.TryRead(File.ReadAllBytes(logo), out var logoW, out var logoH));
        Assert.Equal(697, logoW);
        Assert.Equal(286, logoH);

        Assert.True(ImageSizeReader.TryRead(
            File.ReadAllBytes(wappen), out var wappenW, out var wappenH));
        Assert.Equal(407, wappenW);
        Assert.Equal(491, wappenH);
    }
}
```

- [ ] **Step 4: Test laufen lassen und Erfolg prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AusgelieferteDossierBilderTests"
```

Erwartet: 1 Test grün.

- [ ] **Step 5: Committen**

```bash
git add Export_Vorlage/Dossier_Logo.png Export_Vorlage/Dossier_Wappen.png src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierImageTests.cs
git commit -m "feat(dossier): Logo und Wappen fest ausliefern"
```

---

### Task 7: Eingabemasken ergänzen

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/DossierEditWindow.xaml`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/DossierEditWindow.xaml.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/DossierAreaWindow.xaml`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/DossierAreaWindow.xaml.cs`

**Interfaces:**
- Consumes: `DossierOwnerRow`, `DossierDefinition.Owners`, `DossierDefinition.OverviewPlanPath`, `DossierAreaSettings.Authors` (Task 3)
- Produces: nichts für spätere Aufgaben

Beide Fenster arbeiten ohne MVVM: Werte werden in `Load()` gesetzt und in `OnSave` zurückgeschrieben. Dieses Muster wird beibehalten.

- [ ] **Step 1: Gebietsfenster um „Autoren" erweitern**

In `DossierAreaWindow.xaml` nach dem Block für „Gebietstitel" einfügen:

```xml
                <TextBlock Text="Autoren (erscheint auf Seite 2 unter „Autoren:“)"/>
                <TextBox x:Name="AuthorsBox"/>
```

In `DossierAreaWindow.xaml.cs` in `Load()` ergänzen:

```csharp
        AuthorsBox.Text = _target.Authors;
```

und in `OnSave` ergänzen:

```csharp
        _target.Authors = Trim(AuthorsBox.Text);
```

- [ ] **Step 2: Eigentümertabelle und Planbild ins Dossier-Fenster**

In `DossierEditWindow.xaml` **nach** dem Block „Objektbewohner" und **vor** `<TextBlock Text="Sanierung" .../>` einfügen:

```xml
                <TextBlock Text="Eigentumsverhältnisse (Tabelle im Dossier)"
                           Style="{StaticResource SectionStyle}"/>
                <TextBlock Text="Eine Zeile je Eigentümer bzw. Hausnummer. Diese Zeilen erscheinen in Kapitel 2."/>

                <DataGrid x:Name="OwnersGrid"
                          Height="150"
                          AutoGenerateColumns="False"
                          CanUserAddRows="False"
                          HeadersVisibility="Column"
                          GridLinesVisibility="Horizontal">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Haus Nr." Width="70"
                                            Binding="{Binding HouseNumber, UpdateSourceTrigger=PropertyChanged}"/>
                        <DataGridTextColumn Header="Pz. Nr." Width="70"
                                            Binding="{Binding ParcelNumber, UpdateSourceTrigger=PropertyChanged}"/>
                        <DataGridTextColumn Header="Name" Width="*"
                                            Binding="{Binding Name, UpdateSourceTrigger=PropertyChanged}"/>
                        <DataGridTextColumn Header="Telefon" Width="110"
                                            Binding="{Binding Phone, UpdateSourceTrigger=PropertyChanged}"/>
                        <DataGridTextColumn Header="Mail" Width="140"
                                            Binding="{Binding Mail, UpdateSourceTrigger=PropertyChanged}"/>
                        <DataGridTextColumn Header="Objektbewohner" Width="130"
                                            Binding="{Binding Occupancy, UpdateSourceTrigger=PropertyChanged}"/>
                    </DataGrid.Columns>
                </DataGrid>

                <StackPanel Orientation="Horizontal" Margin="0,6,0,0">
                    <Button Content="+ Zeile" Padding="10,4" Click="OnAddOwner"/>
                    <Button Content="Zeile entfernen" Padding="10,4" Margin="8,0,0,0"
                            Click="OnRemoveOwner"/>
                </StackPanel>

                <TextBlock Text="Übersichtsplan (Bild für Kapitel 1)"
                           Style="{StaticResource SectionStyle}"/>
                <StackPanel Orientation="Horizontal">
                    <Button Content="Bild wählen…" Padding="10,4" Click="OnChoosePlan"/>
                    <Button Content="Entfernen" Padding="10,4" Margin="8,0,0,0"
                            Click="OnClearPlan"/>
                </StackPanel>
                <TextBlock x:Name="PlanPathText" TextWrapping="Wrap"/>
```

- [ ] **Step 3: Code-behind des Dossier-Fensters ergänzen**

In `DossierEditWindow.xaml.cs`:

Oben die `using` ergänzen:

```csharp
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
```

Feld neben `_target` einfügen:

```csharp
    private readonly ObservableCollection<DossierOwnerRow> _owners = new();
    private string _planPath = "";
```

In `Load()` am Ende ergänzen:

```csharp
        _owners.Clear();
        foreach (var owner in _target.Owners)
        {
            // Arbeitskopie: ein Abbrechen darf die gespeicherten Zeilen nicht veraendern.
            _owners.Add(new DossierOwnerRow
            {
                HouseNumber = owner.HouseNumber,
                ParcelNumber = owner.ParcelNumber,
                Name = owner.Name,
                Phone = owner.Phone,
                Mail = owner.Mail,
                Occupancy = owner.Occupancy
            });
        }

        OwnersGrid.ItemsSource = _owners;

        _planPath = _target.OverviewPlanPath;
        ShowPlanPath();
```

Neue Methoden am Ende der Klasse einfügen:

```csharp
    private void OnAddOwner(object sender, RoutedEventArgs e)
    {
        var row = new DossierOwnerRow
        {
            // Die erste Zeile uebernimmt die Angaben der Liegenschaft als Vorschlag.
            HouseNumber = _owners.Count == 0 ? Trim(HouseBox.Text) : "",
            ParcelNumber = _owners.Count == 0 ? Trim(ParcelBox.Text) : ""
        };

        _owners.Add(row);
        OwnersGrid.SelectedItem = row;
    }

    private void OnRemoveOwner(object sender, RoutedEventArgs e)
    {
        if (OwnersGrid.SelectedItem is DossierOwnerRow row)
            _owners.Remove(row);
    }

    private void OnChoosePlan(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Übersichtsplan wählen",
            Filter = "Bilder (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        _planPath = dialog.FileName;
        ShowPlanPath();
    }

    private void OnClearPlan(object sender, RoutedEventArgs e)
    {
        _planPath = "";
        ShowPlanPath();
    }

    private void ShowPlanPath()
    {
        if (_planPath.Length == 0)
        {
            PlanPathText.Text = "Kein Bild gewählt — Kapitel 1 bleibt leer.";
            return;
        }

        PlanPathText.Text = File.Exists(_planPath)
            ? Path.GetFileName(_planPath)
            : Path.GetFileName(_planPath) + "  (Datei nicht gefunden)";
    }
```

In `OnSave` **vor** `DialogResult = true;` einfügen:

```csharp
        // Das Raster gibt die letzte Zelle erst beim Fokuswechsel frei.
        OwnersGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        _target.Owners = _owners
            .Where(owner =>
                !string.IsNullOrWhiteSpace(owner.HouseNumber)
                || !string.IsNullOrWhiteSpace(owner.ParcelNumber)
                || !string.IsNullOrWhiteSpace(owner.Name)
                || !string.IsNullOrWhiteSpace(owner.Phone)
                || !string.IsNullOrWhiteSpace(owner.Mail)
                || !string.IsNullOrWhiteSpace(owner.Occupancy))
            .ToList();

        _target.OverviewPlanPath = _planPath;
```

- [ ] **Step 4: Bindungen prüfen**

Jede `{Binding ...}`-Angabe im neuen XAML muss eine öffentliche Eigenschaft auf `DossierOwnerRow` treffen:
`HouseNumber`, `ParcelNumber`, `Name`, `Phone`, `Mail`, `Occupancy`. Alle sechs sind in Task 3 angelegt.

- [ ] **Step 5: Bauen und alle Tests laufen lassen**

```bash
dotnet build AuswertungPro.sln
dotnet test AuswertungPro.sln
```

Erwartet: 0 Fehler, 0 Warnungen im Build; alle Tests grün.

- [ ] **Step 6: Committen**

```bash
git add src/AuswertungPro.Next.UI/Views/Windows/DossierEditWindow.xaml src/AuswertungPro.Next.UI/Views/Windows/DossierEditWindow.xaml.cs src/AuswertungPro.Next.UI/Views/Windows/DossierAreaWindow.xaml src/AuswertungPro.Next.UI/Views/Windows/DossierAreaWindow.xaml.cs
git commit -m "feat(dossier): Eigentuemertabelle, Planbild und Autoren in den Masken"
```

---

### Task 8: Sichtprüfung an einem echten Dossier

Kein Test ersetzt den Blick auf das fertige Dokument. Diese Aufgabe erzeugt eines und vergleicht es mit dem Original.

**Files:** keine Änderung; nur Ausführung und Bericht.

- [ ] **Step 1: Programm starten und ein Dossier erzeugen**

```bash
dotnet build AuswertungPro.sln
```

Danach `SewerStudio.exe` starten, ein Projekt öffnen, im Bereich **Dossiers**:
1. Gebiets-Einstellungen öffnen, „Autoren" ausfüllen.
2. Eine Liegenschaft anlegen oder öffnen, zwei Eigentümerzeilen erfassen, einen Übersichtsplan wählen.
3. „Word erzeugen" klicken.

- [ ] **Step 2: Das Ergebnis prüfen**

Die erzeugte Word-Datei öffnen und Punkt für Punkt vergleichen mit
`C:\Users\Besitzer\Documents\Eigentümerdossier_Pz.170.pdf`:

| Prüfpunkt | Erwartung |
|---|---|
| Deckblatt | Logo links, Wappen rechts, beide unverzerrt |
| Deckblatt | keine geschweiften Klammern irgendwo im Dokument |
| Seite 2 | „Autoren:" zeigt den eingegebenen Text, nicht „Besitzer" |
| Kapitel 1 | der gewählte Plan ist sichtbar und passt auf die Seite |
| Kapitel 2 | beide Eigentümerzeilen erscheinen als eigene Tabellenzeilen |
| Kapitel 3 | die Leitungen der Auswertung mit Länge, Zustand, Massnahme, Kosten |
| Kapitel 4 | letzte Zeile „Rückmeldung / Einverständnis Eigentümer" mit Unterschriftslinien |
| Fusszeile | Projektkennung links, „Seite X von Y" rechts |

- [ ] **Step 3: Bestehendes Projekt auf Umstellung prüfen**

Ein Projekt öffnen, das bereits vor dieser Änderung Dossiers hatte. Erwartung: das
Dossier-Fenster zeigt **genau eine** Eigentümerzeile mit den bisherigen Angaben, und
nichts fehlt.

- [ ] **Step 4: Ergebnis melden**

Abweichungen sammeln und melden, statt sie stillschweigend zu beheben — bei Optik-Fragen
entscheidet Pascal.
