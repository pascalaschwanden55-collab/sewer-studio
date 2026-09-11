using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

internal static class XtfZusatzReader
{
    internal static void Uebernehme(XDocument doc, Project projekt, ImportStats stats)
    {
        XNamespace ns = "http://www.interlis.ch/INTERLIS2.3";
        var ziele = doc.Descendants().Where(e => e.Attribute("TID") is not null)
            .GroupBy(e => (string)e.Attribute("TID")!, StringComparer.Ordinal)
            .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);
        var gruppen = doc.Descendants(ns + XtfZusatzangaben.Klasse)
            .GroupBy(e => (Tid: (string?)e.Element(ns + "ObjektTid") ?? "", Feld: (string?)e.Element(ns + "Feld") ?? ""));
        foreach (var gruppe in gruppen)
        {
            var (tid, feld) = gruppe.Key;
            if (gruppe.Count() != 1 || !XtfZusatzangaben.IstErlaubt(feld) || !ziele.TryGetValue(tid, out var objekt))
            {
                Melde($"Zusatzangabe {tid}/{feld}: unbekannt, doppelt oder ohne eindeutiges Ziel; nicht uebernommen.");
                continue;
            }
            var wert = (string?)gruppe.Single().Element(ns + "Wert");
            if (string.IsNullOrWhiteSpace(wert)) continue;
            var klasse = objekt.Name.LocalName.Split('.').Last();
            if (!objekt.Name.LocalName.StartsWith("SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.", StringComparison.Ordinal))
                continue;
            var standardfeld = XtfZusatzangaben.Standardfeld(feld);
            var kanalTid = (string?)objekt.Element(ns + "AbwasserbauwerkRef")?.Attribute("REF");
            var kanal = kanalTid is not null && ziele.TryGetValue(kanalTid, out var k) ? k : null;
            if (standardfeld is not null && (objekt.Element(ns + standardfeld) is not null
                || klasse == "Haltung" && kanal?.Element(ns + standardfeld) is not null))
            {
                Melde($"Zusatzangabe {tid}/{feld}: das Standardfeld ist vorhanden und hat Vorrang.");
                continue;
            }
            var nummer = HoldingKeyNormalizer.Normalize((string?)objekt.Element(ns + "Bezeichnung"));
            if (string.IsNullOrEmpty(nummer)) continue;
            if (klasse == "Haltung")
            {
                var treffer = projekt.Data.Where(h => HoldingKeyNormalizer.Normalize(h.GetFieldValue(FieldKeys.HoldingName)) == nummer).ToArray();
                if (treffer.Length == 1) treffer[0].SetFieldValue(feld, wert, FieldSource.Xtf405, userEdited: false);
                else Melde($"Zusatzangabe {tid}/{feld}: Haltung im Projekt nicht eindeutig; nicht uebernommen.");
            }
            else if (AbwasserbauwerkVokabular.Auswahl.Contains(klasse))
            {
                var treffer = projekt.SchaechteData.Where(s => HoldingKeyNormalizer.Normalize(XtfSchachtPlanBuilder.Wert(s, "Schachtnummer")) == nummer).ToArray();
                if (treffer.Length == 1) treffer[0].SetFieldValue(XtfZusatzangaben.Schachtfeld(treffer[0], feld), wert, FieldSource.Xtf405, userEdited: false);
                else Melde($"Zusatzangabe {tid}/{feld}: Bauwerk im Projekt nicht eindeutig; nicht uebernommen.");
            }
        }
        void Melde(string message) => stats.Messages.Add(new ImportMessage { Level = "Warn", Context = "XTF-ZUSATZ", Message = message });
    }
}
