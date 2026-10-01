using System.Linq;
using System.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Statischer Einstieg fuer den Nova-Dialog (<see cref="NovaDialogWindow"/>). Wird von
/// <see cref="Services.DialogService"/> ueber <see cref="Services.IDialogService"/> verwendet
/// und direkt von Fenstern, denen kein <see cref="Services.IDialogService"/> injiziert ist
/// (z. B. <see cref="DossierPreviewWindow"/>) - «derselbe Dienst», wie in der Aufgabe verlangt.
/// </summary>
public static class NovaDialog
{
    public static void ZeigeInfo(string text, string titel = "Hinweis", Window? owner = null)
        => Zeige(NovaDialogArt.Info, NovaDialogKnopfsatz.Ok, titel, text, standardNein: false, owner);

    public static void ZeigeWarnung(string text, string titel = "Warnung", Window? owner = null)
        => Zeige(NovaDialogArt.Warnung, NovaDialogKnopfsatz.Ok, titel, text, standardNein: false, owner);

    public static void ZeigeFehler(string text, string titel = "Fehler", Window? owner = null)
        => Zeige(NovaDialogArt.Fehler, NovaDialogKnopfsatz.Ok, titel, text, standardNein: false, owner);

    public static bool ZeigeBestaetigung(string text, string titel = "Bestätigung", Window? owner = null)
        => Zeige(NovaDialogArt.Frage, NovaDialogKnopfsatz.JaNein, titel, text, standardNein: false, owner)
            == DialogConfirm.Yes;

    public static bool ZeigeWarnendeBestaetigung(
        string text, string titel = "Bestätigung", bool standardNein = true, Window? owner = null)
        => Zeige(NovaDialogArt.Warnung, NovaDialogKnopfsatz.JaNein, titel, text, standardNein, owner)
            == DialogConfirm.Yes;

    public static DialogConfirm ZeigeDreiWegeBestaetigung(string text, string titel = "Bestätigung", Window? owner = null)
        => Zeige(NovaDialogArt.Frage, NovaDialogKnopfsatz.JaNeinAbbrechen, titel, text, standardNein: false, owner);

    private static DialogConfirm Zeige(
        NovaDialogArt art, NovaDialogKnopfsatz knoepfe, string titel, string text, bool standardNein, Window? owner)
    {
        var fenster = new NovaDialogWindow(art, knoepfe, titel, text ?? string.Empty, standardNein);
        var ermittelterOwner = owner ?? ErmittleAktivesFenster();
        if (ermittelterOwner is not null && !ReferenceEquals(ermittelterOwner, fenster))
            fenster.Owner = ermittelterOwner;

        fenster.ShowDialog();
        return fenster.Ergebnis;
    }

    /// <summary>Aktives Fenster der Anwendung, sonst das Hauptfenster - beide nur, wenn sie
    /// tatsaechlich sichtbar sind (ein noch nicht gezeigtes Fenster darf laut WPF nicht als
    /// Owner dienen).</summary>
    private static Window? ErmittleAktivesFenster()
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return null;

        var aktiv = app.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible);
        if (aktiv is not null)
            return aktiv;

        return app.MainWindow is { IsVisible: true } haupt ? haupt : null;
    }
}
