using System.IO;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sobald der Importbericht abgelegt ist, meldet ein Toast den fertigen Bericht
/// und bietet das sichere Oeffnen direkt an.
/// </summary>
public sealed class ImportReportToastTests
{
    private static ImportReportNavigationController Controller(ToastFake toasts, List<string> geoeffnet)
        => new(
            new DialogFake(),
            () => @"C:\Projekt\Projektdateien\projekt.json",
            pfad =>
            {
                geoeffnet.Add(pfad);
                return true;
            },
            toasts: toasts);

    [Fact]
    public void Abgelegter_Bericht_erzeugt_Toast_mit_Bericht_oeffnen()
    {
        var toasts = new ToastFake();
        var geoeffnet = new List<string>();
        var controller = Controller(toasts, geoeffnet);
        var bericht = Path.Combine(Path.GetTempPath(), "import_" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(bericht, "Bericht");

        try
        {
            controller.SetLastReportPath(bericht);

            Assert.Equal("Import abgeschlossen — Bericht liegt bereit.", toasts.Meldung);
            Assert.Equal("Bericht öffnen", toasts.AktionText);
            toasts.Aktion!();
            Assert.Equal([bericht], geoeffnet);
        }
        finally
        {
            File.Delete(bericht);
        }
    }

    [Fact]
    public void Ohne_Berichtspfad_gibt_es_keinen_Toast()
    {
        var toasts = new ToastFake();
        var controller = Controller(toasts, []);

        controller.SetLastReportPath(null);

        Assert.Null(toasts.Meldung);
    }

    private sealed class DialogFake : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => Assert.Fail(message);
        public bool Confirm(string message, string title = "Bestaetigung") => true;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => true;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Yes;
    }

    private sealed class ToastFake : IToastService
    {
        public string? Meldung { get; private set; }
        public string? AktionText { get; private set; }
        public Action? Aktion { get; private set; }

        public void Success(string message) => Meldung = message;

        public void Success(string message, string aktionText, Action aktion)
        {
            Meldung = message;
            AktionText = aktionText;
            Aktion = aktion;
        }

        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) => Assert.Fail(message);
    }
}
