using System;
using System.Collections.Generic;

using AuswertungPro.Next.Application.Dossiers.Preview;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Wann die Eingabeseite neu aufgebaut werden darf — und wann nicht.
///
/// Der Fehler: Jede fertige Ausgabevorschau setzte die Seitenliste neu, das
/// löste die Auswahl erneut aus, und die Eingabeseite wurde komplett neu
/// gebaut. Wer gerade tippte, verlor den Cursor, und der aufgeklappte
/// Abschnitt klappte wieder zu — mitten im Schreiben.
///
/// Neu gebaut wird nur, wenn sich die gezeigten Vorlagenseiten wirklich
/// ändern. Die Eingaben selbst brauchen keinen Neuaufbau: Sie lesen und
/// schreiben direkt am Dossier, und die Listen frischen ihre Zeilen selbst auf.
/// </summary>
public sealed class DossierPreviewFieldRebuildTests
{
    private static DossierPreviewPage Seite(int nummer)
        => new(
            nummer,
            "Seite " + nummer,
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            Array.Empty<DossierPreviewBlock>(),
            Array.Empty<string>());

    [Fact]
    public void Ohne_bisherigen_Aufbau_wird_gebaut()
    {
        Assert.True(DossierPreviewFieldRebuild.IstNoetig(null, [Seite(1)]));
    }

    [Fact]
    public void Dieselben_Seiten_werden_nicht_neu_gebaut()
    {
        var eine = Seite(1);
        var zwei = Seite(2);

        Assert.False(DossierPreviewFieldRebuild.IstNoetig([eine, zwei], [eine, zwei]));
    }

    [Fact]
    public void Eine_andere_Seite_wird_neu_gebaut()
    {
        Assert.True(DossierPreviewFieldRebuild.IstNoetig([Seite(1)], [Seite(2)]));
    }

    [Fact]
    public void Ein_zusaetzliches_Kapitel_wird_neu_gebaut()
    {
        var eine = Seite(1);

        Assert.True(DossierPreviewFieldRebuild.IstNoetig([eine], [eine, Seite(2)]));
    }

    [Fact]
    public void Eine_andere_Reihenfolge_wird_neu_gebaut()
    {
        var eine = Seite(1);
        var zwei = Seite(2);

        Assert.True(DossierPreviewFieldRebuild.IstNoetig([eine, zwei], [zwei, eine]));
    }

    [Fact]
    public void Eine_inhaltsgleiche_Seite_wird_nicht_neu_gebaut()
    {
        // Verglichen wird der Inhalt, nicht das Objekt. Zwei inhaltsgleiche
        // Seiten ergeben dieselben Eingaben — ein Neuaufbau brächte nichts und
        // wuerde nur den Cursor kosten.
        Assert.False(DossierPreviewFieldRebuild.IstNoetig([Seite(1)], [Seite(1)]));
    }

    [Fact]
    public void Ein_neu_gelesenes_Dokument_wird_neu_gebaut()
    {
        // Ein frisch eingelesenes Dokument bringt eigene Inhaltslisten mit;
        // dann sind auch die Eingaben neu daran zu binden.
        var frisch = new DossierPreviewPage(
            1,
            "Seite 1",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            new List<DossierPreviewBlock>(),
            new List<string>());

        Assert.True(DossierPreviewFieldRebuild.IstNoetig([Seite(1)], [frisch]));
    }

    [Fact]
    public void Eine_leere_Seite_ohne_Kapitel_wird_gebaut()
    {
        // Beilagenseiten tragen kein Kapitel — der Hinweis dazu muss trotzdem
        // erscheinen.
        Assert.True(DossierPreviewFieldRebuild.IstNoetig([Seite(1)], []));
    }
}
