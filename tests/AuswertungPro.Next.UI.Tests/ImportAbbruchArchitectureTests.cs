using System;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter fuer den Abbruch des Ein-Knopf-Imports (Restpunkt aus Arbeitspaket 9 des
/// Uebergabeplans vom 2026-09-05).
///
/// Bis dahin uebergab der Controller fest <c>CancellationToken.None</c>: Der Lauf
/// kopierte Gigabyte und liess sich nicht stoppen, waehrend der manuelle Importweg
/// diesen Anschluss laengst hatte. Drei Dinge muessen zusammen halten, sonst ist der
/// Abbruchknopf wirkungslos oder er erschreckt den Benutzer mit einem Fehlerdialog.
/// </summary>
public sealed class ImportAbbruchArchitectureTests
{
    private static string Lies(params string[] teile)
    {
        var pfad = Path.Combine(TestRepoPaths.FindRepositoryRoot(), Path.Combine(teile));
        Assert.True(File.Exists(pfad), $"Datei nicht gefunden: {pfad}");
        return File.ReadAllText(pfad);
    }

    [Fact]
    public void Das_Abbruchsignal_erreicht_den_Laufkontext()
    {
        var controller = Lies("src", "AuswertungPro.Next.UI", "Services", "ImportOneClickProjectController.cs");

        Assert.Contains("CancellationToken CancellationToken = default", controller, StringComparison.Ordinal);

        // Der Laufkontext muss das Signal des Aufrufers tragen; ein fester
        // CancellationToken.None an dieser Stelle war genau der Fehler.
        var kontext = controller.IndexOf("new ImportRunContext(", StringComparison.Ordinal);
        Assert.True(kontext > 0, "ImportRunContext wird nicht mehr gebaut.");
        var kontextEnde = controller.IndexOf(");", kontext, StringComparison.Ordinal);
        var aufruf = controller[kontext..kontextEnde];

        Assert.Contains("actions.CancellationToken", aufruf, StringComparison.Ordinal);
        Assert.DoesNotContain("CancellationToken.None", aufruf, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_Abbruch_ist_kein_Fehler()
    {
        var controller = Lies("src", "AuswertungPro.Next.UI", "Services", "ImportOneClickProjectController.cs");

        var abbruch = controller.IndexOf("catch (OperationCanceledException)", StringComparison.Ordinal);
        var fehler = controller.IndexOf(
            "var userMessage = UserError.DescribeAndReport(ex, \"Kanalfernseh-Projekt importieren\")",
            StringComparison.Ordinal);

        Assert.True(abbruch > 0, "Der Abbruch braucht einen eigenen catch-Zweig.");
        Assert.True(abbruch < fehler,
            "Der Abbruch-Zweig muss VOR dem allgemeinen Fehlerzweig stehen, sonst zeigt "
            + "ein Abbruch einen roten Fehlerdialog.");
        Assert.Contains("Import abgebrochen", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Oberflaeche_stellt_den_Knopf_scharf()
    {
        var viewModel = Lies("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ImportPageViewModel.cs");

        var start = viewModel.IndexOf(
            "private async Task ImportKanalProjektAsync()", StringComparison.Ordinal);
        Assert.True(start > 0, "ImportKanalProjektAsync nicht gefunden.");
        var ausschnitt = viewModel[start..Math.Min(viewModel.Length, start + 2000)];

        Assert.Contains("_importCts = new CancellationTokenSource()", ausschnitt, StringComparison.Ordinal);
        Assert.Contains("CanCancel = true", ausschnitt, StringComparison.Ordinal);
        Assert.Contains("CancellationToken: _importCts.Token", ausschnitt, StringComparison.Ordinal);
        // Ohne das Zuruecksetzen im finally bliebe der Knopf nach einem Lauf aktiv.
        Assert.Contains("CanCancel = false", ausschnitt, StringComparison.Ordinal);
    }

    [Fact]
    public void Der_lange_Weg_prueft_den_Abbruch_an_jeder_Schrittgrenze()
    {
        var orchestrator = Lies(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "ProjectImportOrchestrator.cs");

        // Frueher gab es genau EINE Pruefung, und die lag tief im Katasterabgleich —
        // ein Ordner ohne Katasterdatei lief trotz Abbruch vollstaendig durch.
        var pruefungen = Anzahl(orchestrator, "ct.ThrowIfCancellationRequested();");
        Assert.True(pruefungen >= 4,
            $"Zu wenige Abbruchpruefungen ({pruefungen}) — die teuren Schritte "
            + "Archivieren, Parsen und Verteilen brauchen je eine.");

        // Ein Abbruch darf nicht als Importfehler enden: Jeder Sammel-catch der teuren
        // Schritte muss ihn vorher durchlassen.
        Assert.True(
            Anzahl(orchestrator, "catch (OperationCanceledException) { throw; }") >= 5,
            "Ein Sammel-catch ohne vorgelagerten OperationCanceledException-Zweig "
            + "verwandelt den Abbruch in eine Fehlermeldung.");
    }

    private static int Anzahl(string text, string suchbegriff)
    {
        var anzahl = 0;
        var pos = 0;
        while ((pos = text.IndexOf(suchbegriff, pos, StringComparison.Ordinal)) >= 0)
        {
            anzahl++;
            pos += suchbegriff.Length;
        }

        return anzahl;
    }
}
