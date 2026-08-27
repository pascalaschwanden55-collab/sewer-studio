using System;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingEventActionDialogService
{
    private readonly Action<string, string> _showInfo;
    private readonly Func<string, string, bool> _confirmWarn;

    public CodingEventActionDialogService(
        Action<string, string> showInfo,
        Func<string, string, bool> confirmWarn)
    {
        _showInfo = showInfo;
        _confirmWarn = confirmWarn;
    }

    public void ShowStretchCloseRequiresLaterMeter()
        => _showInfo(
            "Der aktuelle Meterstand muss gr\u00f6\u00dfer sein als der Anfang des Streckenschadens.",
            "Streckenschaden");

    public void ShowNotAnOpenStretchDamage()
        => _showInfo(
            "Diese Zeile ist kein offener Streckenschaden.\n\n"
            + "Schlie\u00dfen geht nur bei einer Zeile mit der Marke OFFEN - dort ist der "
            + "Anfang gesetzt und das Ende fehlt noch.",
            "Streckenschaden");

    public bool ConfirmDelete(string? code)
        => _confirmWarn($"Ereignis '{code}' l\u00f6schen?", "L\u00f6schen");
}
