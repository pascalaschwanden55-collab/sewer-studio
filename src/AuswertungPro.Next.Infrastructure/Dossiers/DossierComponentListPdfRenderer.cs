using System;
using System.Collections.Generic;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Eine frisch erzeugte Bauteilliste mit ihrer Beschriftung. Der Name wandert
/// in die Erfolgsmeldung, damit sichtbar ist, was im Gesamt-PDF steckt.
/// </summary>
internal sealed record DossierComponentListPdf(string Label, byte[] Pdf);

/// <summary>
/// Rendert Haltungs- und Schachtliste frisch aus dem aktuellen Dossierstand,
/// damit Gesamt-PDF und Vorschau denselben Stand zeigen wie die Protokolle.
/// Der Renderer schreibt keine Datei: Die Bytes gehen direkt in den
/// <see cref="DossierPdfPackageComposer"/>, der Kundenordner bleibt unberuehrt.
///
/// Ohne Haltungen entfaellt die Haltungsliste, ohne Schaechte die Schachtliste.
/// Ein Blatt, das nur den Tabellenkopf zeigt, waere kein Nachweis, sondern
/// eine leere Seite im Kundendossier.
/// </summary>
internal sealed class DossierComponentListPdfRenderer
{
    private readonly IDossierHoldingListPdfService _holdingListPdf;
    private readonly IDossierShaftListPdfService _shaftListPdf;
    private readonly Func<DateTime> _currentTime;

    public DossierComponentListPdfRenderer(
        IDossierHoldingListPdfService holdingListPdf,
        IDossierShaftListPdfService shaftListPdf,
        Func<DateTime>? currentTime = null)
    {
        _holdingListPdf = holdingListPdf
            ?? throw new ArgumentNullException(nameof(holdingListPdf));
        _shaftListPdf = shaftListPdf
            ?? throw new ArgumentNullException(nameof(shaftListPdf));
        _currentTime = currentTime ?? (() => DateTime.Now);
    }

    /// <summary>
    /// Haltungsliste zuerst, danach die Schachtliste — dieselbe Reihenfolge wie
    /// ueberall sonst im Dossier.
    /// </summary>
    public IReadOnlyList<DossierComponentListPdf> Render(
        DossierDefinition dossier,
        DossierSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(snapshot);

        var stand = _currentTime();
        var lists = new List<DossierComponentListPdf>(2);

        if (snapshot.Holdings.Count > 0)
        {
            lists.Add(new DossierComponentListPdf(
                DossierMandatoryPageMarkers.HoldingListLabel,
                _holdingListPdf.CreatePdf(
                    DossierHoldingListPdfModelBuilder.Build(dossier, snapshot, stand))));
        }

        if (snapshot.Shafts.Count > 0)
        {
            lists.Add(new DossierComponentListPdf(
                DossierMandatoryPageMarkers.ShaftListLabel,
                _shaftListPdf.CreatePdf(
                    DossierShaftListPdfModelBuilder.Build(dossier, snapshot, stand))));
        }

        return lists;
    }
}
