using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>Nur Attribute, die die jeweilige SIA405-Bauwerksklasse tatsaechlich besitzt.</summary>
public static class XtfBauwerkFelder
{
    public static List<KeyValuePair<string, string>> Schacht(
        SchachtRecord record, string nummer, string klasse, List<string> hinweise)
    {
        var felder = new List<KeyValuePair<string, string>> { new("Bezeichnung", nummer) };
        foreach (var (xtfName, projektFeld) in XtfSchachtPlanBuilder.Felder)
        {
            if (xtfName == "Material" && klasse != "Normschacht"
                || xtfName == "Funktion" && klasse is not ("Normschacht" or "Spezialbauwerk"))
                continue;
            var roh = (XtfSchachtPlanBuilder.Wert(record, projektFeld) ?? "").Trim();
            if (roh.Length == 0) continue;
            var wert = xtfName == "Funktion" && klasse == "Spezialbauwerk"
                ? AbwasserbauwerkVokabular.Spezialfunktion(roh)
                : XtfSchachtPlanBuilder.NachXtfWert(xtfName, roh);
            if (!string.IsNullOrEmpty(wert)) felder.Add(new(xtfName, wert));
            else if (xtfName == "Bemerkung" && XtfStammdatenPlanBuilder.BemerkungZuLang(roh, out var zeichen))
                hinweise.Add($"Schacht {nummer}: die Bemerkung ist {zeichen} Zeichen lang, das Modell laesst {XtfStammdatenPlanBuilder.BemerkungGrenze} zu — nicht geschrieben im Standardfeld; siehe Zusatzangaben.");
            else hinweise.Add($"Schacht {nummer}: {xtfName} = \"{roh}\" passt nicht ins Standardfeld; siehe Zusatzangaben.");
        }
        if (klasse is "Normschacht" or "Versickerungsanlage")
        {
            var masse = XtfSchachtPlanBuilder.Masse(record, $"Schacht {nummer}", hinweise);
            if (masse is not null)
            {
                felder.Add(new("Dimension1", masse.Value.Dimension1));
                felder.Add(new("Dimension2", masse.Value.Dimension2));
                var widerspruch = XtfSchachtPlanBuilder.Formwiderspruch(record, masse);
                if (widerspruch is not null) hinweise.Add($"Schacht {nummer}: {widerspruch}");
            }
        }
        if (klasse == "Versickerungsanlage")
        {
            var art = XtfSchachtPlanBuilder.Wert(record, FieldKeys.InfiltrationType)?.Trim();
            var funktion = XtfSchachtPlanBuilder.Wert(record, "Funktion")?.Trim();
            if (string.IsNullOrEmpty(art) && (string.Equals(funktion, "Sickerschacht", StringComparison.OrdinalIgnoreCase)
                || string.Equals(funktion, "Versickerungsschacht", StringComparison.OrdinalIgnoreCase)))
                art = "Versickerungsschacht";
            var norm = AbwasserbauwerkVokabular.Versickerungsarten.FirstOrDefault(v => v.Length > 0 && v.Equals(art, StringComparison.OrdinalIgnoreCase));
            if (norm is not null) felder.Add(new("Art", norm));
            else if (!string.IsNullOrWhiteSpace(art)) hinweise.Add($"Schacht {nummer}: Versickerungsart \"{art}\" ist kein SIA405-Wert; siehe Zusatzangaben.");
        }
        ErgaenzeGemeinsame(felder, key => XtfSchachtPlanBuilder.Wert(record, key), nummer, hinweise);
        return felder;
    }

    public static void ErgaenzeGemeinsame(List<KeyValuePair<string, string>> felder,
        Func<string, string?> wert, string nummer, List<string> hinweise)
    {
        var ort = wert(FieldKeys.Street)?.Trim();
        if (!string.IsNullOrEmpty(ort))
        {
            if (ort.EnumerateRunes().Count() <= 50) felder.Add(new("Standortname", ort));
            else hinweise.Add($"{nummer}: Strasse ist laenger als 50 Zeichen; vollstaendig in den Zusatzangaben.");
        }
        var kosten = wert(FieldKeys.GrossCost)?.Trim();
        if (!string.IsNullOrEmpty(kosten) && !felder.Any(f => f.Key == "Bruttokosten"))
        {
            var norm = XtfStammdatenPlanBuilder.NachXtfWert("Bruttokosten", kosten, "SIA405_ABWASSER_2020_LV95");
            if (norm is not null) felder.Add(new("Bruttokosten", norm));
            else hinweise.Add($"{nummer}: Bruttokosten \"{kosten}\" sind im Standardfeld nicht gueltig; siehe Zusatzangaben.");
        }
        var datum = wert(FieldKeys.InspectionYear)?.Trim();
        if (string.IsNullOrEmpty(datum)) return;
        var jahr = int.TryParse(datum, out var zahl) && datum.Length == 4 ? zahl : 0;
        if (jahr == 0 && DateOnly.TryParseExact(datum,
            ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "yyyyMMdd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var tag))
            jahr = tag.Year;
        if (jahr is >= 1800 and <= 2100) felder.Add(new("Zustandserhebung_Jahr", jahr.ToString(CultureInfo.InvariantCulture)));
    }
}
