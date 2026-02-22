using System.Collections.Generic;

namespace AuswertungPro.Next.Domain.Models
{
    public class VsaFinding
    {
        public string KanalSchadencode { get; set; } = string.Empty;
        public string? Quantifizierung1 { get; set; }
        public string? Quantifizierung2 { get; set; }
        public double? SchadenlageAnfang { get; set; }
        public double? SchadenlageEnde { get; set; }
        public double? LL { get; set; }
        public string? Raw { get; set; } // Optional: Originaltext

        // WinCan/Export/Overlay Felder
        public double? MeterStart { get; set; }
        public double? MeterEnd { get; set; }
        public string? MPEG { get; set; }
        public System.DateTime? Timestamp { get; set; }
        public string? FotoPath { get; set; }

        // Für VSA-Auswertung
        public int? EZD { get; set; }
        public int? EZS { get; set; }
        public int? EZB { get; set; }
    }
}
