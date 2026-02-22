namespace AuswertungPro.Next.Domain.Vsa;

/// <summary>
/// Eine codierte Feststellung (z.B. DIN EN 13508-2 / VSA) inkl. optionaler Quantifizierung.
/// </summary>
public sealed class VsaCodeFinding
{
    public string Code { get; set; } = "";      // z.B. "BAF"
    public string? Ch1 { get; set; }            // z.B. "C"
    public string? Ch2 { get; set; }            // z.B. "B"
    public string? QuantUnit { get; set; }      // z.B. "%", "mm", "Anzahl"
    public double? QuantValue { get; set; }     // z.B. 25
    public double? Length_m { get; set; }       // effektive Länge bei Streckenfeststellung (optional)
    public string Raw { get; set; } = "";       // Original-Textzeile (Debug)

    // Erweiterung für WinCan-ähnliche Bearbeitung:
    public double? MeterStart { get; set; }     // Startmeter (z.B. 0.00)
    public double? MeterEnd { get; set; }       // Endmeter (z.B. 12.46)
    public string? MPEG { get; set; }           // Zeitstempel im Video (z.B. "00:01:03")
    public DateTime? Timestamp { get; set; }    // Optional: exakter Zeitstempel
    public string? FotoPath { get; set; }       // Optional: Foto-Referenz
}
