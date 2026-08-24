using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Die zwei Schreibwege der Themenliste.
///
/// Beim Ausfuellen eines Dossiers gehoert der Text zuerst diesem Dossier — ein
/// Schadensbeschrieb gilt nur fuer diese Liegenschaft. Angaben wie
/// Ansprechpartner oder Unternehmer gelten dagegen fuer das ganze Gebiet;
/// dafuer gibt es den zweiten Weg.
///
/// Reine Logik ohne Oberflaeche, damit beide Regeln pruefbar bleiben.
/// </summary>
public static class DossierTopicEditing
{
    /// <summary>
    /// Setzt den Text als Abweichung dieses Dossiers. Die Gebietsvorgabe bleibt
    /// unberuehrt und gilt weiter fuer alle anderen Liegenschaften.
    /// </summary>
    public static void SetForDossier(DossierDefinition dossier, string title, string? text)
    {
        ArgumentNullException.ThrowIfNull(dossier);

        var titel = (title ?? string.Empty).Trim();
        if (titel.Length == 0)
            return;

        dossier.Topics ??= new();

        var vorhanden = dossier.Topics.FirstOrDefault(t =>
            t is not null && string.Equals(t.Title, titel, StringComparison.OrdinalIgnoreCase));

        if (vorhanden is null)
        {
            dossier.Topics.Add(new DossierTopicRow { Title = titel, Text = text ?? "" });
            return;
        }

        vorhanden.Text = text ?? "";
    }

    /// <summary>
    /// Setzt die Schriftfarbe als Abweichung dieses Dossiers.
    ///
    /// Ein leerer Wert heisst Schwarz — die Farbe der Vorlage. Die Zeile
    /// entsteht dabei notfalls: sonst liesse sich die Farbe eines reinen
    /// Gebietsthemas gar nicht setzen.
    /// </summary>
    public static void SetColorForDossier(
        DossierDefinition dossier, string title, string? colorHex, string? currentText = null)
    {
        ArgumentNullException.ThrowIfNull(dossier);

        var titel = (title ?? string.Empty).Trim();
        if (titel.Length == 0)
            return;

        dossier.Topics ??= new();

        var zeile = dossier.Topics.FirstOrDefault(t =>
            t is not null && string.Equals(t.Title, titel, StringComparison.OrdinalIgnoreCase));

        if (zeile is null)
        {
            zeile = new DossierTopicRow { Title = titel, Text = currentText ?? string.Empty };
            dossier.Topics.Add(zeile);
        }

        zeile.ColorHex = (colorHex ?? string.Empty).Trim();
        zeile.StyleRanges = new();
    }

    /// <summary>
    /// Speichert Klartext und gemischte Farben gemeinsam. Die alte Ganzzeilen-
    /// Farbe wird geleert, weil die Farbbereiche die genauere Angabe sind.
    /// </summary>
    public static void SetFormattedForDossier(
        DossierDefinition dossier,
        string title,
        string? text,
        IEnumerable<DossierTextStyleRange>? styleRanges)
    {
        ArgumentNullException.ThrowIfNull(dossier);

        var titel = (title ?? string.Empty).Trim();
        if (titel.Length == 0)
            return;

        SetForDossier(dossier, titel, text);
        var zeile = dossier.Topics.First(t =>
            t is not null && string.Equals(t.Title, titel, StringComparison.OrdinalIgnoreCase));

        zeile.ColorHex = string.Empty;
        zeile.StyleRanges = DossierTopicTextFormatting.Normalize(text, styleRanges);
    }

    /// <summary>Die gesetzte Schriftfarbe, oder leer fuer Schwarz.</summary>
    public static string ColorOf(
        DossierAreaSettings? area, DossierDefinition dossier, string title)
    {
        ArgumentNullException.ThrowIfNull(dossier);

        return DossierTopicResolver.Resolve(area, dossier)
            .FirstOrDefault(t => string.Equals(t.Title, title, StringComparison.OrdinalIgnoreCase))
            ?.ColorHex ?? string.Empty;
    }

    /// <summary>
    /// Uebernimmt den Text ins Gebiet und entfernt die Abweichung. Danach gilt
    /// er hier wie ueberall — genau das ist der Sinn des Knopfes.
    ///
    /// Kennt das Gebiet den Titel nicht, entsteht dort eine neue Zeile; sonst
    /// verschwaende der Text spurlos.
    /// </summary>
    public static void PromoteToArea(
        DossierAreaSettings area, DossierDefinition dossier, string title, string? text)
        => PromoteToArea(area, dossier, title, text, null, null);

    /// <summary>Uebernimmt Text und Teilfarben in die Gebietsvorgabe.</summary>
    public static void PromoteToArea(
        DossierAreaSettings area,
        DossierDefinition dossier,
        string title,
        string? text,
        IEnumerable<DossierTextStyleRange>? styleRanges,
        string? legacyColorHex)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(dossier);

        var titel = (title ?? string.Empty).Trim();
        if (titel.Length == 0)
            return;

        area.Topics ??= new();

        var gebietsThema = area.Topics.FirstOrDefault(t =>
            t is not null && string.Equals(t.Title, titel, StringComparison.OrdinalIgnoreCase));

        if (gebietsThema is null)
        {
            gebietsThema = new DossierTopicRow { Title = titel };
            area.Topics.Add(gebietsThema);
        }

        gebietsThema.Text = text ?? "";
        if (styleRanges is not null || legacyColorHex is not null)
        {
            gebietsThema.ColorHex = legacyColorHex?.Trim() ?? string.Empty;
            gebietsThema.StyleRanges = DossierTopicTextFormatting.Normalize(text, styleRanges);
        }

        RemoveDossierOverride(dossier, titel);
    }

    /// <summary>Entfernt die Abweichung dieses Dossiers, falls es eine gibt.</summary>
    public static void RemoveDossierOverride(DossierDefinition dossier, string title)
    {
        ArgumentNullException.ThrowIfNull(dossier);

        var titel = (title ?? string.Empty).Trim();
        if (titel.Length == 0 || dossier.Topics is null)
            return;

        dossier.Topics.RemoveAll(t =>
            t is not null && string.Equals(t.Title, titel, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Die Themen, in denen die betroffenen Leitungen und Schaechte stehen.
    /// Nur dort werden die Einfuegeknoepfe angeboten — ein Ansprechpartner oder
    /// ein Ausfuehrungstermin braucht keine Leitungsliste.
    ///
    /// Verglichen wird der Anfang des Titels, damit "Schaeden Pz. 30" ebenso
    /// zaehlt wie "Schaeden".
    /// </summary>
    public static bool SupportsHoldingInsert(string? title)
        => DossierTopicTitles.Matches(DossierTopicTitles.WithComponentButton, title);

    /// <summary>
    /// In diesen Themen werden alle Haltungen und danach alle Schaechte ohne
    /// einen manuellen Einfuegeschritt ausgegeben.
    /// </summary>
    public static bool IncludesComponentsAutomatically(string? title)
        => DossierTopicComponentListComposer.IsAutomaticTitle(title);

    /// <summary>Wahr, wenn dieses Dossier fuer den Titel etwas Eigenes fuehrt.</summary>
    public static bool HasDossierOverride(DossierDefinition dossier, string title)
    {
        ArgumentNullException.ThrowIfNull(dossier);

        var titel = (title ?? string.Empty).Trim();

        return titel.Length > 0
            && dossier.Topics is not null
            && dossier.Topics.Any(t =>
                t is not null && string.Equals(t.Title, titel, StringComparison.OrdinalIgnoreCase));
    }
}
