using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Aus zwei gespeicherten Höhenangaben die dritte anzeigen. Keine Rückschreibung oder erfundene Deckelzuordnung.</summary>
public sealed class SchachtHoehenRechnung
{
    public const string Deckelfeld = "schacht.deckelhoehe", Sohlenfeld = "schacht.sohlenhoehe", Tiefenfeld = "schacht.tiefe";
    private readonly Dictionary<string, string> _werte;
    public string? BerechnetesFeld { get; private set; }
    public string Warnung { get; private set; } = "";
    private SchachtHoehenRechnung(string deckel, string sohle, string tiefe)
        => _werte = new() { [Deckelfeld] = deckel, [Sohlenfeld] = sohle, [Tiefenfeld] = tiefe };
    public static bool IstHoehenfeld(string id) => id is Deckelfeld or Sohlenfeld or Tiefenfeld;
    public string Lies(string id) => _werte.GetValueOrDefault(id, "");
    public string Hinweis(string id) => Warnung.Length > 0 ? Warnung : BerechnetesFeld != id ? "" : id switch
    {
        Deckelfeld => "Berechnet: Sohlenhöhe + Tiefe.",
        Sohlenfeld => "Berechnet: Deckelhöhe − Tiefe.",
        _ => "Berechnet: Deckelhöhe − Sohlenhöhe."
    };

    public static SchachtHoehenRechnung Fuer(ObjektaktenBearbeitung b)
    {
        var schacht = b.Projekt.SchaechteData.Single(s => s.Id == b.WurzelId);
        var wurzel = b.Wurzel;
        var deckel = SchachtDeckelAnzeige.Waehle(b.Projekt, wurzel);
        var d = deckel?.Werte.GetValueOrDefault("deckel.hoehe");
        var s = wurzel.Werte.GetValueOrDefault(Sohlenfeld);
        var tiefenkey = SchachtFeldnamen.Feld(schacht, "Tiefe");
        var r = new SchachtHoehenRechnung(d?.Text ?? "", s?.Text ?? "", schacht.GetFieldValue(tiefenkey));
        var gesperrt = new HashSet<string>();
        if (d?.VonHand == true) gesperrt.Add(Deckelfeld);
        if (s?.VonHand == true) gesperrt.Add(Sohlenfeld);
        if (schacht.IsUserEdited(tiefenkey) || wurzel.Werte.GetValueOrDefault(Tiefenfeld)?.VonHand == true)
            gesperrt.Add(Tiefenfeld);
        if (deckel is null && (wurzel.HauptdeckelId is not null
            || b.Projekt.Objektakten.Any(a => a.Art == "deckel" && a.Bezuege.Contains(wurzel.Id))))
        {
            gesperrt.Add(Deckelfeld);
            r.Warnung = "Bitte zuerst einen eindeutigen Hauptdeckel wählen.";
        }
        r.Rechne(gesperrt);
        return r;
    }

    private void Rechne(HashSet<string> gesperrt)
    {
        var zahlen = new Dictionary<string, decimal>();
        foreach (var (feld, text) in _werte)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (!FachzahlParser.TryParseMeasurement(text, out var zahl))
            { Warnung = "Bitte gültige Zahlen für Deckelhöhe, Sohlenhöhe und Tiefe eingeben."; return; }
            zahlen[feld] = zahl;
        }
        if (zahlen.GetValueOrDefault(Tiefenfeld) < 0)
        { Warnung = "Bitte prüfen: Die Tiefe darf nicht negativ sein."; return; }
        try
        {
            if (zahlen.TryGetValue(Deckelfeld, out var d) && zahlen.TryGetValue(Sohlenfeld, out var s) && d < s)
            { Warnung = "Bitte prüfen: Deckel liegt unter der Sohle."; return; }
            if (zahlen.Count == 3)
            {
                if (Math.Abs(zahlen[Deckelfeld] - zahlen[Sohlenfeld] - zahlen[Tiefenfeld]) > 0.001m)
                    Warnung = "Bitte prüfen: Deckelhöhe, Sohlenhöhe und Tiefe widersprechen sich (mehr als 1 mm).";
                return;
            }
            if (zahlen.Count != 2) return;
            var fehlt = _werte.Keys.Single(f => !zahlen.ContainsKey(f));
            if (gesperrt.Contains(fehlt)) return;
            var wert = fehlt switch
            {
                Deckelfeld => zahlen[Sohlenfeld] + zahlen[Tiefenfeld],
                Sohlenfeld => zahlen[Deckelfeld] - zahlen[Tiefenfeld],
                _ => zahlen[Deckelfeld] - zahlen[Sohlenfeld]
            };
            _werte[fehlt] = wert.ToString("0.000", CultureInfo.InvariantCulture);
            BerechnetesFeld = fehlt;
        }
        catch (OverflowException) { Warnung = "Bitte prüfen: Die Höhenangaben sind zu gross für die Berechnung."; }
    }
}
