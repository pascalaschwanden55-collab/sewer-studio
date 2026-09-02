namespace AuswertungPro.Next.UI.DataPage;

public sealed record GridDropdownFieldSpec(
    string OptionField,
    string ItemsSourcePath,
    bool AllowFreeText,
    bool Managed,
    string EditCommand = "",
    string PreviewCommand = "",
    string ResetCommand = "",
    string RemoveCommand = "",
    string AddCommand = "");

public static class GridDropdownFieldPolicy
{
    public static bool TryResolve(string optionField, out GridDropdownFieldSpec spec)
    {
        spec = optionField switch
        {
            "Sanieren_JaNein" => new GridDropdownFieldSpec(
                optionField,
                "SanierenOptions",
                AllowFreeText: true,
                Managed: true,
                EditCommand: "EditSanierenOptionsCommand",
                PreviewCommand: "PreviewSanierenOptionsCommand",
                ResetCommand: "ResetSanierenOptionsCommand",
                RemoveCommand: "RemoveSanierenOptionCommand",
                AddCommand: "AddSanierenOptionCommand"),
            // Freitext, obwohl die Liste fest ist: Der Kanton fuehrt mehr
            // Eigentuemer als die fuenf Kurzformen — "Abwasser Uri", die
            // einzelnen Gemeinden, "unbekannt". Bei AllowFreeText: false
            // bindet die Vorlage auf SelectedItem; ein Wert ausserhalb der
            // Liste ist dort nicht darstellbar, das Feld sieht leer aus und
            // die erste Bedienung ersetzt ihn durch den ersten Listeneintrag
            // — bisher "Kanton". Ein nachgeschlagener Eigentuemer ging so
            // verloren.
            "Eigentuemer" => new GridDropdownFieldSpec(
                optionField,
                "EigentuemerOptions",
                AllowFreeText: true,
                Managed: true,
                EditCommand: "EditEigentuemerOptionsCommand",
                PreviewCommand: "PreviewEigentuemerOptionsCommand",
                ResetCommand: "ResetEigentuemerOptionsCommand",
                RemoveCommand: "RemoveEigentuemerOptionCommand",
                AddCommand: "AddEigentuemerOptionCommand"),
            "Pruefungsresultat" => new GridDropdownFieldSpec(
                optionField,
                "PruefungsresultatOptions",
                AllowFreeText: true,
                Managed: true,
                EditCommand: "EditPruefungsresultatOptionsCommand",
                PreviewCommand: "PreviewPruefungsresultatOptionsCommand",
                ResetCommand: "ResetPruefungsresultatOptionsCommand",
                RemoveCommand: "RemovePruefungsresultatOptionCommand",
                AddCommand: "AddPruefungsresultatOptionCommand"),
            "Referenzpruefung" => new GridDropdownFieldSpec(
                optionField,
                "ReferenzpruefungOptions",
                AllowFreeText: true,
                Managed: true,
                EditCommand: "EditReferenzpruefungOptionsCommand",
                PreviewCommand: "PreviewReferenzpruefungOptionsCommand",
                ResetCommand: "ResetReferenzpruefungOptionsCommand",
                RemoveCommand: "RemoveReferenzpruefungOptionCommand",
                AddCommand: "AddReferenzpruefungOptionCommand"),
            // Rohrmaterial: feste Katalogwerte plus eigene Ergaenzungen. Freitext ist
            // erlaubt, ein neu getippter Wert wandert ueber EnsureOptionForField in die Liste.
            "Rohrmaterial" => new GridDropdownFieldSpec(
                optionField,
                "RohrmaterialOptions",
                AllowFreeText: true,
                Managed: true,
                EditCommand: "EditRohrmaterialOptionsCommand",
                PreviewCommand: "PreviewRohrmaterialOptionsCommand",
                ResetCommand: "ResetRohrmaterialOptionsCommand",
                RemoveCommand: "RemoveRohrmaterialOptionCommand",
                AddCommand: "AddRohrmaterialOptionCommand"),
            "Ausgefuehrt_durch" => new GridDropdownFieldSpec(
                optionField,
                "AusgefuehrtDurchOptions",
                AllowFreeText: true,
                Managed: false),
            "Schachtform" => new GridDropdownFieldSpec(
                optionField,
                "SchachtformOptions",
                AllowFreeText: false,
                Managed: false),
            // Belastungsklasse nach EN 124: feste Liste, kein Freitext. Eine getippte
            // Klasse waere eine unbelegte Aussage ueber die Tragfaehigkeit.
            "Belastungsklasse" => new GridDropdownFieldSpec(
                optionField,
                "BelastungsklasseOptions",
                AllowFreeText: false,
                Managed: false),
            // Schachtfunktion und -material nach SIA405: feste Listen, kein Freitext
            // und nicht vom Benutzer erweiterbar. Ein getippter Wert haette keinen
            // Normwert und koennte deshalb nie in eine XTF geschrieben werden.
            "Funktion" => new GridDropdownFieldSpec(
                optionField,
                "SchachtFunktionOptions",
                AllowFreeText: false,
                Managed: false),
            "Material" => new GridDropdownFieldSpec(
                optionField,
                "SchachtMaterialOptions",
                AllowFreeText: false,
                Managed: false),
            // Die vier SIA405-Felder der revidierten XTF: feste Wertelisten aus dem
            // Modell, kein Freitext. Ein getippter Wert waere im Export nicht
            // abbildbar und wuerde dort still liegen bleiben.
            "FunktionHierarchisch" => new GridDropdownFieldSpec(
                optionField,
                "FunktionHierarchischOptions",
                AllowFreeText: false,
                Managed: false),
            "Verbindungsart" => new GridDropdownFieldSpec(
                optionField,
                "VerbindungsartOptions",
                AllowFreeText: false,
                Managed: false),
            "Bettung_Umhuellung" => new GridDropdownFieldSpec(
                optionField,
                "BettungUmhuellungOptions",
                AllowFreeText: false,
                Managed: false),
            "Profiltyp" => new GridDropdownFieldSpec(
                optionField,
                "ProfiltypOptions",
                AllowFreeText: false,
                Managed: false),
            _ => null!
        };

        return spec is not null;
    }
}
