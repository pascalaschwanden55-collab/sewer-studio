using System;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Ein minimaler Befehl fuer Menuepunkte, die nur eine Aktion ausloesen.
/// Waehrend der Ausfuehrung ist er gesperrt, damit ein zweiter Klick keine
/// zweite Abfrage beim Kanton startet.
/// </summary>
internal sealed class EinfacherBefehl : ICommand
{
    private readonly Func<System.Threading.Tasks.Task> _aktion;
    private bool _laeuft;

    public EinfacherBefehl(Func<System.Threading.Tasks.Task> aktion)
        => _aktion = aktion ?? throw new ArgumentNullException(nameof(aktion));

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_laeuft;

    public async void Execute(object? parameter)
    {
        _ = parameter;
        if (_laeuft)
            return;

        _laeuft = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await _aktion().ConfigureAwait(true);
        }
        finally
        {
            _laeuft = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
