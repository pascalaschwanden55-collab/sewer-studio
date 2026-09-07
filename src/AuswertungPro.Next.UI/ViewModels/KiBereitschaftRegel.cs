using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels;

public enum KiBereitschaft { NichtGestartet, Startet, Bereit, PruefungNoetig }

/// <summary>BEWERTUNG N10: im Alltag "Analyse bereit", "Pruefung noetig" statt Modellnamen.</summary>
public static class KiBereitschaftRegel
{
    public static KiBereitschaft Bestimme(AiRuntimeStatus status) => status switch
    {
        { IsVisible: false } => KiBereitschaft.NichtGestartet,
        { Title: "KI STARTET" } => KiBereitschaft.Startet,
        { Title: "KI WARNUNG" } => KiBereitschaft.PruefungNoetig,
        _ => KiBereitschaft.Bereit
    };

    public static string Text(KiBereitschaft b) => b switch
    {
        KiBereitschaft.NichtGestartet => "KI nicht gestartet",
        KiBereitschaft.Startet => "KI startet",
        KiBereitschaft.PruefungNoetig => "Prüfung nötig",
        _ => "Analyse bereit"
    };
}
