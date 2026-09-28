using AuswertungPro.Next.Application.UseCases.Verteilung;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    private IVerteilVorschau? _verteilVorschau;
    private IVerteilenDialog? _verteilenDialog;

    /// <summary>Schreibfreie Vorschau fuer das Fenster «Verteilen».</summary>
    public IVerteilVorschau VerteilVorschau => _verteilVorschau ??= new VerteilVorschauService();

    /// <summary>
    /// Fenster «Verteilen» auf der Export-Seite. Die Datei-/Ordnerdialoge werden bei jedem
    /// Oeffnen neu gelesen, weil <see cref="Dialogs"/> austauschbar ist.
    /// </summary>
    public IVerteilenDialog VerteilenDialog
    {
        get => _verteilenDialog ??= new VerteilenDialogService(VerteilVorschau, () => Dialogs);
        internal set => _verteilenDialog = value;
    }
}
