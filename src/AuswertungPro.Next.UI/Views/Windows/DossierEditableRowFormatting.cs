using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Gemeinsame Textformatierung und Vorschauadressen fuer dynamische
/// Eigentümer- und Änderungszeilen.
/// </summary>
internal static class DossierEditableRowFormatting
{
    public static List<DossierTextStyleRange> ReadStyles(
        object row,
        string key,
        string text)
    {
        var styles = Styles(row);
        return styles.TryGetValue(key, out var ranges)
            ? DossierTopicTextFormatting.Normalize(text, ranges)
            : new List<DossierTextStyleRange>();
    }

    public static void Save(
        object row,
        string styleKey,
        Action<object, string> write,
        RichTextBox box)
    {
        var value = DossierTopicRichTextEditor.Read(box);
        write(row, value.Text);

        var styles = Styles(row);
        if (value.StyleRanges.Count == 0)
            styles.Remove(styleKey);
        else
            styles[styleKey] = value.StyleRanges.ToList();
    }

    /// <summary>
    /// Die Spaltennamen des Editors und der Word-Wiederholzeile sind historisch
    /// nicht ueberall gleich. Die Klickadresse verwendet die Namen der Vorlage.
    /// </summary>
    public static string PreviewColumnKey(string listKey, string styleKey)
        => (listKey, styleKey) switch
        {
            ("Eigentuemer", "HouseNumber") => "Haus_Nr",
            ("Eigentuemer", "ParcelNumber") => "Pz_Nr",
            ("Eigentuemer", "Name") => "Eigentuemer_Zelle",
            ("Eigentuemer", "Phone") => "Telefon",
            ("Eigentuemer", "Mail") => "Mail",
            ("Eigentuemer", "Occupancy") => "Objektbewohner",
            ("Aenderungen", "Date") => "Datum",
            ("Aenderungen", "Change") => "Aenderung",
            _ => styleKey
        };

    private static Dictionary<string, List<DossierTextStyleRange>> Styles(object row)
        => row switch
        {
            DossierOwnerRow owner => owner.FieldStyles ??= new(),
            DossierChangeRow change => change.FieldStyles ??= new(),
            _ => throw new ArgumentException("Unbekannte Dossierzeile.", nameof(row))
        };
}
