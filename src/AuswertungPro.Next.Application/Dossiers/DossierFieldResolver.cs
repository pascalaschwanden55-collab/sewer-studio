using System;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Die aufgeloesten Textfelder eines Dossiers: Gebietsangaben, sofern das
/// Dossier sie nicht ueberschreibt.
/// </summary>
public sealed record DossierResolvedFields(
    string AreaTitle,
    string ExecutionDate,
    string ContactPerson,
    string Contractor,
    string SiteManagement,
    string Obstructions,
    string HouseConnectionText,
    string StormWaterText,
    string ResponseDeadline,
    string FooterLine);

/// <summary>
/// Loest die zweistufige Eingabe auf: Was im Dossier steht, gewinnt; ist das
/// Feld dort leer, gilt die Gebietsangabe. Pure Logik.
/// </summary>
public static class DossierFieldResolver
{
    public static DossierResolvedFields Resolve(
        DossierAreaSettings? area,
        DossierDefinition? dossier)
    {
        var a = area ?? new DossierAreaSettings();
        var d = dossier ?? new DossierDefinition();

        return new DossierResolvedFields(
            AreaTitle: Trim(a.AreaTitle),
            ExecutionDate: Inherit(d.ExecutionDateOverride, a.ExecutionDate),
            ContactPerson: Inherit(d.ContactPersonOverride, a.ContactPerson),
            Contractor: Inherit(d.ContractorOverride, a.Contractor),
            SiteManagement: Inherit(d.SiteManagementOverride, a.SiteManagement),
            Obstructions: Inherit(d.ObstructionsOverride, a.Obstructions),
            HouseConnectionText: Inherit(d.HouseConnectionTextOverride, a.HouseConnectionText),
            StormWaterText: Inherit(d.StormWaterTextOverride, a.StormWaterText),
            ResponseDeadline: Inherit(d.ResponseDeadlineOverride, a.ResponseDeadline),
            FooterLine: Trim(a.FooterLine));
    }

    /// <summary>
    /// Ein gesetzter, nicht leerer Ueberschreibwert gewinnt. Leerzeichen allein
    /// zaehlen nicht als Ueberschreibung — sonst loescht ein versehentliches
    /// Leerzeichen die Gebietsangabe stillschweigend.
    /// </summary>
    private static string Inherit(string? overrideValue, string? areaValue)
        => string.IsNullOrWhiteSpace(overrideValue) ? Trim(areaValue) : overrideValue.Trim();

    private static string Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
