using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Bekannte Fachfelder vor dem Rueckfall fuer freie Vorlagenspalten zuordnen.</summary>
internal static class SchachtDetailGruppen
{
    private static readonly IReadOnlyDictionary<string, string> Gruppen = Baue();
    internal static string? Fuer(string feld) => Gruppen.GetValueOrDefault(SchachtFeldnamen.Falte(feld));

    private static Dictionary<string, string> Baue()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string gruppe, params string[] felder)
        {
            foreach (var feld in felder) result[SchachtFeldnamen.Falte(feld)] = gruppe;
        }
        Add("Stammdaten", "Baujahr", "Bauwerksart", "Versickerungsart", "Status", "Belastungsklasse", "Eigentümer", "Eigentuemer");
        Add("Zustand und Inspektion", "Inspektionsdatum", "Primaere_Schaeden", "Primäre Schäden", "Sanierungsbedarf");
        Add("Dokumente und Medien", "Fotos", "Foto", "Bilder");
        Add("Sanierung und Kosten", "Ausgeführt durch", "Ausgefuehrt_durch");
        Add("Zustand und Inspektion", "Bemerkungen", "Bemerkung");
        return result;
    }
}
