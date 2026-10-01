using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der Kontrollschacht 80409 (Utzigmattweg Altdorf, Zone 1.15) so, wie ihn Protokoll vom
/// 24.09.2025, QGIS-Kopie und GeoShop-Kataster am 19.09.2026 beschreiben. Gemeinsame
/// Grundlage der Schachtgrafik-Tests.
/// </summary>
internal static class SchachtgrafikBeispiel
{
    public static SchachtRecord Schacht80409(bool mitTiefe = true, bool mitTabelle = true, bool mitProtokoll = true)
    {
        var s = new SchachtRecord();
        s.SetFieldValue("Schachtnummer", "80409", FieldSource.Pdf, false);
        if (mitTiefe)
            s.SetFieldValue("Schachttiefe", "3.45", FieldSource.Pdf, false);
        s.SetFieldValue(FieldKeys.ShaftDimension1Mm, "900", FieldSource.Pdf, false);
        s.SetFieldValue(FieldKeys.ShaftDimension2Mm, "1100", FieldSource.Pdf, false);
        s.SetFieldValue("Schachtform", "Oval", FieldSource.Pdf, false);
        s.SetFieldValue("Medium", "Mischabwasser", FieldSource.Pdf, false);
        s.SetFieldValue("Deckeldurchmesser", "660", FieldSource.Pdf, false);
        s.SetFieldValue("Deckelmaterial", "Guss und Beton", FieldSource.Pdf, false);
        s.SetFieldValue("Steighilfe", "vorhanden", FieldSource.Pdf, false);
        s.SetFieldValue("Bemerkungen", "Einlauf 3 Ausgebrochen", FieldSource.Pdf, false);

        if (mitTabelle)
        {
            s.SetzeAnschluesse(
            [
                new SchachtAnschluss { Nr = 1, Art = "Auslauf", DnMm = 250, TiefeM = 3.45m, Material = "Beton", Quelle = "PDF" },
                new SchachtAnschluss { Nr = 2, Art = "Einlauf", DnMm = 250, TiefeM = 3.38m, Material = "Beton", Quelle = "PDF" },
                new SchachtAnschluss { Nr = 3, Art = "Einlauf", DnMm = 100, TiefeM = 0.60m, Material = "Polyvinylchlorid", Quelle = "PDF" },
                new SchachtAnschluss { Nr = 4, Art = "Einlauf", DnMm = 200, TiefeM = 3.25m, Material = "Polyvinylchlorid", Quelle = "PDF" },
            ]);
        }

        if (mitProtokoll)
        {
            s.Protocol = new ProtocolDocument
            {
                Current = new ProtocolRevision
                {
                    Entries =
                    {
                        new ProtocolEntry { Code = "Konus", Beschreibung = "Verkalkung" },
                        new ProtocolEntry { Code = "Bankett", Beschreibung = "ausgebrochen" },
                        new ProtocolEntry { Code = "Bankett", Beschreibung = "Ablagerungen" },
                        new ProtocolEntry { Code = "Durchlaufrinne", Beschreibung = "Ablagerungen" },
                    }
                }
            };
        }

        return s;
    }

    /// <summary>Die drei Haltungen des Projekts «Zone 1.15» — dort sind Schacht_oben/unten leer, der Name traegt sie.</summary>
    public static List<HaltungRecord> Haltungen80409()
    {
        return
        [
            Haltung("80547-80409", "250", "Zement"),
            Haltung("80467-80409", "100", "Polyvinylchlorid"),
            Haltung("80409-80538", "250", "Zement"),
        ];

        static HaltungRecord Haltung(string name, string dn, string material)
        {
            var h = new HaltungRecord();
            h.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, false);
            h.SetFieldValue(FieldKeys.NominalDiameterMm, dn, FieldSource.Manual, false);
            h.SetFieldValue(FieldKeys.PipeMaterial, material, FieldSource.Manual, false);
            return h;
        }
    }

    public static SchachtgrafikZusatz Zusatz80409(bool mitLage = true, bool mitKoten = true)
    {
        var lage = mitLage
            ? new SchachtLage(
                new XtfPunkt(2692630.471, 1192370.448),
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["80547-80409"] = 28.4,
                    ["80467-80409"] = 145.2,
                    ["80409-80538"] = 274.6,
                })
            : null;
        var koten = mitKoten
            ? new SchachtKoten(498.620m, 495.150m, new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["80547-80409"] = 495.160m,
                ["80409-80538"] = 495.140m,
            })
            : null;
        return new SchachtgrafikZusatz(lage, koten);
    }

    public static SchachtgrafikModell Modell80409(bool mitTiefe = true, bool mitTabelle = true, bool mitLage = true, bool mitKoten = true)
        => SchachtgrafikModellBuilder.Baue(
            Schacht80409(mitTiefe, mitTabelle),
            Haltungen80409(),
            catalog: null,
            Zusatz80409(mitLage, mitKoten),
            SchachtgrafikAnsichtBuilder.Markenfarbe);
}
