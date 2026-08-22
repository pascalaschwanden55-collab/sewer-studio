using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Reports;

/// <summary>
/// Das Haltungsprotokoll ist ein offizielles Dokument - es darf nichts behaupten,
/// was nie erfasst wurde. Zwei solche Stellen sind bei der Sichtpruefung vom
/// 2026-08-21 aufgefallen; diese Tests halten die Korrekturen fest.
/// </summary>
public sealed class ProtokollPdfEhrlichkeitTests
{
    private static ProtocolEntry Anschluss(int? uhr)
    {
        var e = new ProtocolEntry
        {
            Code = "BCA",
            MeterStart = 12.0,
            MeterEnd = 12.0,
            Beschreibung = "Seitlicher Anschluss",
        };
        if (uhr is not null)
        {
            e.CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = "BCA",
                Parameters = { ["ClockPos1"] = uhr.Value.ToString() },
            };
        }
        return e;
    }

    private static string Svg(ProtocolEntry eintrag)
        => HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            length: 40.0,
            entries: new[] { eintrag },
            photoNumbers: null,
            startNode: "80638",
            endNode: "80631",
            flowDown: true);

    [Fact]
    public void Grafik_ohne_erfasste_Uhrlage_behauptet_keine()
    {
        // Vorher stand am Anschluss-Stutzen "3h", obwohl nie eine Uhrlage erfasst
        // wurde - eine erfundene Messangabe. Der Stutzen darf als Orientierung
        // gezeichnet werden, aber ohne Zahl und erkennbar unbestimmt (gestrichelt).
        var svg = Svg(Anschluss(uhr: null));

        Assert.DoesNotContain(">3h<", svg, StringComparison.Ordinal);
        Assert.Contains("stroke-dasharray", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Grafik_mit_erfasster_Uhrlage_zeigt_sie()
    {
        var svg = Svg(Anschluss(uhr: 9));

        Assert.Contains(">9h<", svg, StringComparison.Ordinal);
    }

    // ── Seitenwahl wie in echten WinCan-Protokollen ─────────────────────────
    //
    // Referenz: Fretz/WinCan-Protokoll 06.71273-77775 vom 28.04.2022 mit neun
    // Anschluessen. Dort gilt die ehrliche Draufsicht: Die Kamera blickt in
    // Inspektionsrichtung (auf dem Blatt nach unten), ihr Rechts (1-5 Uhr)
    // erscheint auf dem Blatt LINKS, ihr Links (7-11 Uhr) RECHTS. 12 und 6 Uhr
    // liegen ueber/unter dem Rohr und werden als Kreis AUF dem Rohr gezeichnet.

    private static double? StutzenEndeX(string svg)
    {
        // Der Anschluss-Stutzen ist die einzige 3px-Linie in Grau #6B7280.
        var treffer = System.Text.RegularExpressions.Regex.Match(
            svg, @"<line [^>]*x2='([0-9.]+)'[^>]*stroke='#6B7280' stroke-width='3'");
        return treffer.Success
            ? double.Parse(treffer.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)
            : null;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Kamera_rechts_erscheint_auf_dem_Blatt_links(int uhr)
    {
        var x2 = StutzenEndeX(Svg(Anschluss(uhr)));

        Assert.NotNull(x2);
        Assert.True(x2 < HaltungsgrafikSvgBuilder.LineX,
            $"{uhr} Uhr muss links der Rohrachse liegen (x2={x2}).");
    }

    [Theory]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(11)]
    public void Kamera_links_erscheint_auf_dem_Blatt_rechts(int uhr)
    {
        var x2 = StutzenEndeX(Svg(Anschluss(uhr)));

        Assert.NotNull(x2);
        Assert.True(x2 > HaltungsgrafikSvgBuilder.LineX,
            $"{uhr} Uhr muss rechts der Rohrachse liegen (x2={x2}).");
    }

    // ── Uhrlage aus dem Befundtext ──────────────────────────────────────────
    //
    // Der alte WinCan-Viewer-MDB-Import (z.B. Andermatt) schreibt KEINE
    // strukturierten Parameter - die Uhrlage steht nur im Befundtext
    // ("Anschluss eingespitzt, offen bei 2 Uhr"). Ohne Text-Rueckfall fielen
    // alle diese Anschluesse auf "Lage unbekannt" zurueck, obwohl der Operateur
    // die Lage erfasst hat.

    private static ProtocolEntry AnschlussMitText(string beschreibung)
        => new()
        {
            Code = "BCA",
            MeterStart = 12.0,
            MeterEnd = 12.0,
            Beschreibung = beschreibung,
        };

    [Theory]
    [InlineData("Anschluss eingespitzt, offen bei 2 Uhr", 2)]
    [InlineData("Anschluss unvollständig eingebunden bei 11 Uhr", 11)]
    [InlineData("Anschluss, von 4 Uhr bis 8 Uhr sichtbar", 4)]
    [InlineData("Einragender Anschluss 9 Uhr", 9)]
    public void Uhrlage_aus_dem_Befundtext_wird_gelesen(string text, int erwartet)
    {
        Assert.Equal(erwartet, ProtocolTextHelpers.ExtractClockHour(AnschlussMitText(text)));
    }

    [Theory]
    [InlineData("Anschluss eingespitzt, offen")]
    [InlineData("Anschluss, 150mm, 10% der Leitungshöhe")]
    [InlineData("")]
    public void Text_ohne_Uhrlage_erfindet_keine(string text)
    {
        Assert.Null(ProtocolTextHelpers.ExtractClockHour(AnschlussMitText(text)));
    }

    [Fact]
    public void Strukturierte_Parameter_gehen_dem_Text_vor()
    {
        var e = AnschlussMitText("Anschluss bei 2 Uhr");
        e.CodeMeta = new ProtocolEntryCodeMeta
        {
            Code = "BCA",
            Parameters = { ["ClockPos1"] = "10" },
        };

        Assert.Equal(10, ProtocolTextHelpers.ExtractClockHour(e));
    }

    [Fact]
    public void Textuhrlage_steuert_auch_die_Grafikseite()
    {
        // Genau der Andermatt-Fall: nur Text, keine Parameter - der Stutzen
        // muss trotzdem auf der richtigen Seite liegen (2 Uhr -> Blatt links).
        var svg = Svg(AnschlussMitText("Anschluss eingespitzt, offen bei 2 Uhr"));
        var x2 = StutzenEndeX(svg);

        Assert.NotNull(x2);
        Assert.True(x2 < HaltungsgrafikSvgBuilder.LineX);
        Assert.Contains(">2h<", svg, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(6)]
    public void Scheitel_und_Sohle_liegen_auf_dem_Rohr(int uhr)
    {
        // Ein Anschluss bei 12/6 Uhr liegt ueber bzw. unter dem Rohr - in der
        // Draufsicht gibt es keine Seite. WinCan zeichnet ihn als Kreis auf dem
        // Rohr; ein seitlicher Stutzen waere eine erfundene Richtung.
        var svg = Svg(Anschluss(uhr));

        Assert.Null(StutzenEndeX(svg));
        Assert.Contains($">{uhr}h<", svg, StringComparison.Ordinal);
    }

    // ── Betreiber-Quelle ────────────────────────────────────────────────────

    private static (Project Projekt, HaltungRecord Haltung) ProjektMit(string? haltungsEigentuemer)
    {
        var projekt = new Project();
        // Der Projekt-Standard setzt Metadata["Eigentuemer"] = "Privat" - genau
        // deshalb darf das Protokoll nicht blind aus dem Projekt lesen.
        var rec = new HaltungRecord();
        if (haltungsEigentuemer is not null)
            rec.SetFieldValue("Eigentuemer", haltungsEigentuemer, FieldSource.Manual, userEdited: false);
        projekt.Data.Add(rec);
        return (projekt, rec);
    }

    [Fact]
    public void Betreiber_kommt_aus_der_Haltung_nicht_aus_dem_Projektstandard()
    {
        // Real passiert: Zone 1.15 hat 87 AWU-Haltungen, aber das Protokoll
        // druckte "Betreiber Privat", weil der PROJEKT-Standardwert gelesen wurde.
        var (projekt, rec) = ProjektMit("AWU");

        Assert.Equal("AWU", ProtocolPdfExporter.ResolveBetreiber(projekt, rec));
    }

    [Fact]
    public void Ohne_Haltungswert_gilt_das_Projekt()
    {
        var (projekt, rec) = ProjektMit(null);
        projekt.Metadata["Eigentuemer"] = "Gemeinde";

        Assert.Equal("Gemeinde", ProtocolPdfExporter.ResolveBetreiber(projekt, rec));
    }

    // ── Kopfzeilen ohne Doppelnennung ───────────────────────────────────────

    [Fact]
    public void Kopf_zeigt_GEP_und_Projektname_nicht_doppelt()
    {
        var (projekt, rec) = ProjektMit("AWU");
        projekt.Name = "Altdorf Zone 1.15";
        // Ohne eigene Beschreibung sind GEP (= project.Name) und Projektname
        // identisch - dann traegt die zweite Zeile nichts.
        var zeilen = ProtocolPdfExporter.BuildHaltungsprotokollHeaderTable(
            projekt, rec, "24.09.2025", 47.0, "80638-80631");

        Assert.Single(zeilen, z => z.Value == "Altdorf Zone 1.15");
    }

    [Fact]
    public void Kopf_zeigt_beide_wenn_sie_sich_unterscheiden()
    {
        var (projekt, rec) = ProjektMit("AWU");
        projekt.Name = "Altdorf Zone 1.15";
        projekt.Description = "Auswertung GEP Altdorf";

        var zeilen = ProtocolPdfExporter.BuildHaltungsprotokollHeaderTable(
            projekt, rec, "24.09.2025", 47.0, "80638-80631");

        Assert.Contains(zeilen, z => z.Value == "Altdorf Zone 1.15");
        Assert.Contains(zeilen, z => z.Value == "Auswertung GEP Altdorf");
    }

    // ── Kostentabelle: einheitliches Zahlenbild ─────────────────────────────

    [Fact]
    public void Mengen_und_Geld_tragen_dasselbe_Tausenderzeichen()
    {
        // Vorher: Menge "1,234.56" (InvariantCulture) neben "1'234.56 CHF" -
        // zwei Tausenderzeichen in derselben Tabelle.
        Assert.Equal("1'234.56", HaltungsDossierPdfBuilder.FmtDec(1234.56m));
        Assert.Equal("47.00", HaltungsDossierPdfBuilder.FmtDec(47m));
    }
}
