using System;
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
    /// Uebernimmt den Text ins Gebiet und entfernt die Abweichung. Danach gilt
    /// er hier wie ueberall — genau das ist der Sinn des Knopfes.
    ///
    /// Kennt das Gebiet den Titel nicht, entsteht dort eine neue Zeile; sonst
    /// verschwaende der Text spurlos.
    /// </summary>
    public static void PromoteToArea(
        DossierAreaSettings area, DossierDefinition dossier, string title, string? text)
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
            area.Topics.Add(new DossierTopicRow { Title = titel, Text = text ?? "" });
        else
            gebietsThema.Text = text ?? "";

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
    private static readonly string[] TitelMitLeitungen =
    {
        "Schäden",
        "Sanierungskonzept",
        "Kostenschätzung"
    };

    public static bool SupportsHoldingInsert(string? title)
    {
        var titel = (title ?? string.Empty).Trim();

        return titel.Length > 0
            && TitelMitLeitungen.Any(t =>
                titel.StartsWith(t, StringComparison.OrdinalIgnoreCase));
    }

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
