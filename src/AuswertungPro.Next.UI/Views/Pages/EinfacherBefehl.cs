using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Gemeinsame Sperre aller Nachschlag-Befehle einer Seite. Jedes Feld hat
/// seinen eigenen Befehl, aber es darf immer nur eine Abfrage laufen: Zwei
/// gleichzeitige Grundbuchabfragen zaehlen doppelt gegen die Drosselung des
/// Kantons.
/// </summary>
internal sealed class NachschlagTor
{
    public bool Belegt { get; private set; }

    /// <summary>Wird ausgeloest, wenn sich der Zustand aendert.</summary>
    public event EventHandler? ZustandGeaendert;

    public bool Betreten()
    {
        if (Belegt)
            return false;

        Belegt = true;
        ZustandGeaendert?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Verlassen()
    {
        Belegt = false;
        ZustandGeaendert?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Ein minimaler Befehl fuer Menuepunkte, die eine Abfrage ausloesen.
///
/// <see cref="Execute"/> ist wie bei jedem <see cref="ICommand"/> ein
/// "async void" — eine unbehandelte Ausnahme wuerde dort das ganze Programm
/// beenden. Deshalb faengt der Befehl alles ab und reicht es an den
/// Fehlerbehandler weiter, statt die Oberflaeche zu verlieren.
/// </summary>
internal sealed class EinfacherBefehl : ICommand
{
    private readonly Func<Task> _aktion;
    private readonly Action<Exception> _beiFehler;
    private readonly NachschlagTor _tor;

    public EinfacherBefehl(Func<Task> aktion, Action<Exception> beiFehler, NachschlagTor? tor = null)
    {
        _aktion = aktion ?? throw new ArgumentNullException(nameof(aktion));
        _beiFehler = beiFehler ?? throw new ArgumentNullException(nameof(beiFehler));
        _tor = tor ?? new NachschlagTor();
        _tor.ZustandGeaendert += (_, _) => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_tor.Belegt;

    public async void Execute(object? parameter)
    {
        _ = parameter;

        if (!_tor.Betreten())
            return;

        try
        {
            await _aktion().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // Niemals durchreichen: async void beendet sonst das Programm.
            _beiFehler(ex);
        }
        finally
        {
            _tor.Verlassen();
        }
    }
}
