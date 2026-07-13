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
            "Eigentuemer" => new GridDropdownFieldSpec(
                optionField,
                "EigentuemerOptions",
                AllowFreeText: false,
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
            _ => null!
        };

        return spec is not null;
    }
}
