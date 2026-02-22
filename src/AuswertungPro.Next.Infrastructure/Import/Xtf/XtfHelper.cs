using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf
{
    public class XtfHoldingInfo
    {
        public string HaltungId { get; set; } = string.Empty;
        public string SchachtOben { get; set; } = string.Empty;
        public string SchachtUnten { get; set; } = string.Empty;
    }

    public static class XtfHelper
    {
        // Sucht im XTF nach Haltungseinträgen und gibt eine Liste mit Haltungsnummer und Schacht oben/unten zurück
        public static List<XtfHoldingInfo> ParseHoldingsFromXtf(string xtfPath)
        {
            var result = new List<XtfHoldingInfo>();
            if (!File.Exists(xtfPath)) return result;
            var doc = XDocument.Load(xtfPath);
            XNamespace ns = doc.Root?.Name.Namespace ?? "";

            // SIA405: <SIA405_Abwasser.SIA405_Abwasser.Kanal ... Haltung="11111-2222" SchachtOben="..." SchachtUnten="..." />
            var kanalElements = doc.Descendants().Where(e => e.Name.LocalName.EndsWith("Kanal"));
            foreach (var kanal in kanalElements)
            {
                var haltung = kanal.Attribute("Haltung")?.Value ?? string.Empty;
                var schachtOben = kanal.Attribute("SchachtOben")?.Value ?? string.Empty;
                var schachtUnten = kanal.Attribute("SchachtUnten")?.Value ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(haltung))
                {
                    result.Add(new XtfHoldingInfo
                    {
                        HaltungId = haltung,
                        SchachtOben = schachtOben,
                        SchachtUnten = schachtUnten
                    });
                }
            }
            return result;
        }

        // Findet das XTF mit gleichem Basisnamen wie das PDF (im gleichen oder bekannten Ordner)
        public static string? FindMatchingXtf(string pdfPath, IEnumerable<string> xtfFiles)
        {
            var pdfName = Path.GetFileNameWithoutExtension(pdfPath);
            // Suche nach XTF mit gleichem Namen (ggf. mit Suffixen)
            var match = xtfFiles.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Contains(pdfName, StringComparison.OrdinalIgnoreCase));
            return match;
        }
    }
}
