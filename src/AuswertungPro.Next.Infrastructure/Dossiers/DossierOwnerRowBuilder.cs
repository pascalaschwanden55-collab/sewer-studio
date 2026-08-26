using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Baut die Eigentümer-Wiederholzeilen samt der gemeinsam formatierten
/// Mehrzeilenzelle und den Klickadressen fuer die exakte Vorschau.
/// </summary>
internal static class DossierOwnerRowBuilder
{
    public static List<IReadOnlyDictionary<string, string>> Build(
        DossierDefinition dossier)
    {
        ArgumentNullException.ThrowIfNull(dossier);

        var rows = new List<IReadOnlyDictionary<string, string>>();
        foreach (var owner in dossier.Owners)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            DossierRowTextFormatting.AddValue(
                row,
                "Haus_Nr",
                owner.HouseNumber,
                DossierRowTextFormatting.Styles(owner.FieldStyles, "HouseNumber"));
            DossierRowTextFormatting.AddValue(
                row,
                "Pz_Nr",
                owner.ParcelNumber,
                DossierRowTextFormatting.Styles(owner.FieldStyles, "ParcelNumber"));

            var ownerCell = BuildCell(dossier, owner);
            row["Eigentuemer_Zelle"] = ownerCell.Text;
            row["Eigentuemer_Zelle" + DossierTopicTextFormatting.StyleRangesSuffix] =
                DossierTopicTextFormatting.Encode(ownerCell.StyleRanges);

            // Diese Einzelwerte und Beschriftungen werden nicht als eigene
            // Word-Spalten geschrieben. Sie liefern dem Trefferabgleich aber
            // eindeutige Textstuecke innerhalb der gemeinsamen Zelle.
            row["Telefon"] = owner.Phone ?? string.Empty;
            row["Mail"] = owner.Mail ?? string.Empty;
            row["Objektbewohner"] = owner.Occupancy ?? string.Empty;
            row[DossierOwnerCellLabels.Phone.CellKey] = ClickableLabel(
                dossier, owner.Phone, DossierOwnerCellLabels.Phone);
            row[DossierOwnerCellLabels.Mail.CellKey] = ClickableLabel(
                dossier, owner.Mail, DossierOwnerCellLabels.Mail);
            row[DossierOwnerCellLabels.Occupancy.CellKey] = ClickableLabel(
                dossier, owner.Occupancy, DossierOwnerCellLabels.Occupancy);
            rows.Add(row);
        }

        return rows;
    }

    private static string ClickableLabel(
        DossierDefinition dossier,
        string? value,
        DossierOwnerCellLabel label)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : DossierOwnerCellLabels.Text(dossier, label);

    /// <summary>Leere Angaben erzeugen keine leere Beschriftungszeile.</summary>
    private static DossierTopicTextFormatting.FormattedText BuildCell(
        DossierDefinition dossier,
        DossierOwnerRow owner)
    {
        var text = new StringBuilder();
        var ranges = new List<DossierTextStyleRange>();

        void AppendFormatted(DossierTopicTextFormatting.FormattedText formatted)
        {
            var offset = text.Length;
            text.Append(formatted.Text);
            ranges.AddRange(formatted.StyleRanges.Select(range => new DossierTextStyleRange
            {
                Start = offset + range.Start,
                Length = range.Length,
                ColorHex = range.ColorHex,
                Bold = range.Bold,
                Italic = range.Italic,
                Underline = range.Underline
            }));
        }

        void AddLine(
            string value,
            string styleKey,
            DossierOwnerCellLabel? label = null)
        {
            var formatted = DossierRowTextFormatting.Clean(
                value,
                DossierRowTextFormatting.Styles(owner.FieldStyles, styleKey));
            if (formatted.Text.Length == 0)
                return;

            if (text.Length > 0)
                text.Append('\n');

            if (label is not null)
            {
                var labelText = DossierRowTextFormatting.Clean(
                    DossierOwnerCellLabels.Text(dossier, label),
                    DossierOwnerCellLabels.Styles(dossier, label));
                AppendFormatted(labelText);
                if (labelText.Text.Length > 0)
                    text.Append(' ');
            }

            AppendFormatted(formatted);
        }

        AddLine(owner.Name, "Name");
        AddLine(owner.Phone, "Phone", DossierOwnerCellLabels.Phone);
        AddLine(owner.Mail, "Mail", DossierOwnerCellLabels.Mail);
        AddLine(owner.Occupancy, "Occupancy", DossierOwnerCellLabels.Occupancy);

        return new DossierTopicTextFormatting.FormattedText(text.ToString(), ranges);
    }
}
