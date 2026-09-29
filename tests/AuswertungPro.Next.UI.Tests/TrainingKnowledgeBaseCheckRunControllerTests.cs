using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingKnowledgeBaseCheckRunControllerTests
{
    [Fact]
    public void TryStart_stoppt_wenn_bereits_busy()
    {
        var result = TrainingKnowledgeBaseCheckRunController.TryStart(isBusy: true);

        Assert.True(result.ShouldStop);
        Assert.False(result.IsBusy);
        Assert.Null(result.StatusText);
    }

    [Fact]
    public void TryStart_setzt_busy_und_startstatus()
    {
        var result = TrainingKnowledgeBaseCheckRunController.TryStart(isBusy: false);

        Assert.False(result.ShouldStop);
        Assert.True(result.IsBusy);
        Assert.Equal("Prüfe Knowledge Base...", result.StatusText);
    }

    [Fact]
    public void ApplySuccess_schreibt_logzeilen_und_status()
    {
        var logs = new List<string>();
        string? status = null;
        var presentation = new TrainingKnowledgeBaseCheckPresentation(
            "KB geprüft.",
            ["Zeile 1", "Zeile 2"]);

        TrainingKnowledgeBaseCheckRunController.ApplySuccess(
            presentation,
            logs.Add,
            value => status = value);

        Assert.Equal(["Zeile 1", "Zeile 2"], logs);
        Assert.Equal("KB geprüft.", status);
    }

    [Fact]
    public void ApplyFailure_schreibt_status_und_log()
    {
        var logs = new List<string>();
        string? status = null;
        var exception = new InvalidOperationException("kaputt");

        TrainingKnowledgeBaseCheckRunController.ApplyFailure(
            exception,
            logs.Add,
            value => status = value);

        // Aufgabe 10c2: Der Status zeigt die UserError-Meldung, das Log behaelt den Rohtext.
        Assert.Equal("KB-Prüfung fehlgeschlagen: Der Vorgang konnte nicht abgeschlossen werden. Technische Details stehen im Programmlog.", status);
        Assert.Equal(["KB-Prüfung FEHLER: kaputt"], logs);
    }
}
