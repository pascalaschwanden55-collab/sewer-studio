using System.Windows;
using AuswertungPro.Next.Application.UseCases.Verteilung;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Öffnet <see cref="VerteilenWindow"/> modal über dem Hauptfenster.</summary>
public sealed class VerteilenDialogService : IVerteilenDialog
{
    private readonly IVerteilVorschau _vorschau;
    private readonly Func<IDialogService> _dialogs;

    /// <param name="dialogs">Liefert die aktuellen Datei-/Ordnerdialoge (der ServiceProvider kann sie tauschen).</param>
    public VerteilenDialogService(IVerteilVorschau vorschau, Func<IDialogService> dialogs)
    {
        _vorschau = vorschau ?? throw new ArgumentNullException(nameof(vorschau));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    public VerteilenErgebnis Zeige(VerteilenVorgabe vorgabe)
    {
        ArgumentNullException.ThrowIfNull(vorgabe);
        using var viewModel = new VerteilenViewModel(vorgabe, _vorschau, _dialogs());
        var fenster = new VerteilenWindow(viewModel);
        var besitzer = System.Windows.Application.Current?.MainWindow;
        if (besitzer is not null && !ReferenceEquals(besitzer, fenster) && besitzer.IsLoaded)
            fenster.Owner = besitzer;
        else
            fenster.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        fenster.ShowDialog();
        return viewModel.Rueckgabe;
    }
}
