using System;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingApplyDialogService
{
    private readonly Func<string, string, bool> _confirmWarn;
    private readonly Func<string, string, DialogConfirm> _confirmCancel;

    public CodingApplyDialogService(
        Func<string, string, bool> confirmWarn,
        Func<string, string, DialogConfirm> confirmCancel)
    {
        _confirmWarn = confirmWarn;
        _confirmCancel = confirmCancel;
    }

    public bool ConfirmEmptyProtocol(CodingApplyEmptyProtocolGuardResult guard)
        => !guard.RequiresConfirmation || _confirmWarn(guard.Message, guard.Title);

    /// <summary>
    /// Fragt beim fehlenden Rohrende nach. Bewusst KEINE stille Ergaenzung: ein
    /// gesetztes BCE behauptet, die ganze Haltung sei befahren worden.
    /// </summary>
    public CodingApplyPipeEndDecision ConfirmMissingPipeEnd(CodingApplyPipeEndPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        if (prompt.ProposalMeter is not { } meter)
        {
            // Drei verschiedene Lagen, drei verschiedene S\u00e4tze.
            //
            // Bewusst NICHT "gespeicherte Haltungsl\u00e4nge": Im h\u00e4ufigsten Fall hat
            // der Codiermodus sie beim Einstieg selbst aus dem h\u00f6chsten
            // Protokollmeter abgeleitet (CodingHaltungslaengeResolver) - dann sitzt
            // sie zwangsl\u00e4ufig auf dem letzten Befund, und Pascal hat sie nie
            // eingegeben. Und kein Rat "korrigiere die Haltungsl\u00e4nge": das Feld
            // steuert auch l\u00e4ngenbasierte Kostenpositionen und den PDF-Export.
            var grund = prompt.RejectedLengthM switch
            {
                { } verworfen when Math.Abs(verworfen - prompt.LastObservationM) <= 0.005
                    => $"Die bekannte Haltungsl\u00e4nge ({verworfen:F2}m) liegt genau auf dem letzten "
                       + "Befund und taugt deshalb nicht als Rohrende.",
                { } verworfen
                    => $"Die bekannte Haltungsl\u00e4nge ({verworfen:F2}m) liegt nicht hinter dem letzten "
                       + $"Befund ({prompt.LastObservationM:F2}m) und taugt deshalb nicht als Rohrende.",
                _ => "Die Haltungsl\u00e4nge ist nicht bekannt, deshalb kann kein Meter "
                     + "vorgeschlagen werden."
            };

            return _confirmWarn(
                "Im Protokoll fehlt das Rohrende (BCE).\n\n"
                + grund
                + "\n\nJa = ohne Rohrende \u00fcbernehmen\n"
                + "Nein = nichts \u00fcbernehmen, Rohrende zuerst codieren",
                "Rohrende fehlt")
                ? CodingApplyPipeEndDecision.Skip
                : CodingApplyPipeEndDecision.Cancel;
        }

        var result = _confirmCancel(
            "Im Protokoll fehlt das Rohrende (BCE).\n\n"
            + $"Ja = Rohrende bei {meter:F2}m setzen (Haltungsl\u00e4nge)\n"
            + "Nein = ohne Rohrende \u00fcbernehmen\n"
            + "Abbrechen = nichts \u00fcbernehmen",
            "Rohrende fehlt");

        return result switch
        {
            DialogConfirm.Yes => CodingApplyPipeEndDecision.Insert,
            DialogConfirm.No => CodingApplyPipeEndDecision.Skip,
            _ => CodingApplyPipeEndDecision.Cancel
        };
    }

    public bool ConfirmUnappliedChangesOnClose(Func<bool> applyChanges)
    {
        var result = _confirmCancel(
            "Es gibt noch nicht \u00fcbernommene Codierungen.\n\n" +
            "Ja = \u00fcbernehmen\nNein = verwerfen\nAbbrechen = Fenster offen lassen",
            "Codier-Modus");

        return CodingUnappliedChangesClosePolicy.ShouldClose(result, applyChanges);
    }
}
