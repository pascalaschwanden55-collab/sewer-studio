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

        // PhotoMeasurement V4.3: Einheit pro Quantifizierungs-Wert.
        // Einheit wird aus VsaCodeTree.GetQuantificationUnit(code, idx) abgeleitet.
        // Beispiel: BAB → Einheit1 = "mm" (Rissbreite), BAA → Einheit1 = "%" (Verformung)
        public string? Einheit1 { get; set; }
        public string? Einheit2 { get; set; }

        // PhotoMeasurement V4.3: Werkzeug-Herkunft fuer Nachvollziehbarkeit.
        // Werte: "Lineal", "Wasserstand", "Ablagerung", "Hindernis", "Querschnitt",
        //        "Deformation", "Anschluss", "Abzweig", "Bogen"
        public string? MeasurementTool { get; set; }

        // PhotoMeasurement V4.3: Semantik bei mehrdeutigen Werkzeugen (v.a. Querschnitt).
        // Werte: "Wurzel", "Abplatzung", "Fehlstelle", "Sonstige"
        public string? MeasurementSubject { get; set; }
    }
}
