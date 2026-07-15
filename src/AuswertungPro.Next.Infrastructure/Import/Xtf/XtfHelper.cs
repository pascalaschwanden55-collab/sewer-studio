using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

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
        private static IXtfHoldingFileReader _holdingReader = new XtfHoldingFileReader();

        public static IXtfHoldingFileReader CurrentHoldingReader =>
            Volatile.Read(ref _holdingReader);

        public static void UseHoldingReader(IXtfHoldingFileReader holdingReader) =>
            Volatile.Write(
                ref _holdingReader,
                holdingReader ?? throw new ArgumentNullException(nameof(holdingReader)));

        // Sucht im XTF nach Haltungseintraegen und gibt Haltungsnummer und Schaechte zurueck.
        public static List<XtfHoldingInfo> ParseHoldingsFromXtf(string xtfPath) =>
            CurrentHoldingReader.ParseHoldingsFromXtf(xtfPath);

        // Findet das XTF mit gleichem Basisnamen wie das PDF.
        public static string? FindMatchingXtf(string pdfPath, IEnumerable<string> xtfFiles)
        {
            var pdfName = Path.GetFileNameWithoutExtension(pdfPath);
            return xtfFiles.FirstOrDefault(xtf =>
                Path.GetFileNameWithoutExtension(xtf)
                    .Contains(pdfName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
