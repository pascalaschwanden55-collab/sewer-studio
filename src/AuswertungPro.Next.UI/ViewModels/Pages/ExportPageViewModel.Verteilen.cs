using System.IO;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.UseCases.Verteilung;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Ein Fenster «Verteilen» statt mehrerer Ja/Nein-Dialoge und Ordnerwahlen nacheinander.
/// Das Fenster fragt Art, Ablage, Quelle und Filme ab und zeigt eine schreibfreie Vorschau;
/// verteilt wird danach hier, mit genau dieser Quelle, über die bisherigen Verteilwege.
/// </summary>
public sealed partial class ExportPageViewModel
{
    private readonly IVerteilenDialog _verteilenDialog;

    /// <summary>Das Fenster hat «Ordnerbausteine einstellen» gewählt: die Seite zeigt sie.</summary>
    public event EventHandler? VerteilEinstellungenAngefordert;

    private async Task VerteilenAsync(VerteilArt art, DistributionVariant ablage)
    {
        var ergebnis = _verteilenDialog.Zeige(new VerteilenVorgabe(
            art,
            ablage,
            _settings.LastVideoSourceFolder,
            _shell.Project,
            BestimmeVerteilZiel));

        if (ergebnis.EinstellungenOeffnen)
        {
            VerteilEinstellungenAngefordert?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (ergebnis.Auftrag is not { } auftrag)
            return;

        switch (auftrag.Art)
        {
            case VerteilArt.Haltungen:
                await DistributeHoldingsAsync(auftrag);
                break;
            case VerteilArt.Schaechte:
                // Nur die Schachtverteilung speichert das Projekt am Ende selbst (Transaktion).
                SetShaftDistributionActive(true);
                await DistributeShaftsAsync(auftrag);
                break;
            case VerteilArt.Dichtheit:
                await DistributeDichtheitAsync(auftrag);
                break;
        }
    }

    /// <summary>
    /// Der Hauptordner, den der Verteilweg verwenden wird: der eigene aus den Ordnerbausteinen,
    /// sonst «Projektordner \ …_Verteilt». Ohne gespeichertes Projekt fragt das Verteilen wie
    /// bisher nach einem Ordner.
    /// </summary>
    internal VerteilZiel BestimmeVerteilZiel(VerteilArt art)
    {
        var cfg = art switch
        {
            VerteilArt.Schaechte => _settings.SchachtDistribution,
            VerteilArt.Dichtheit => _settings.DichtheitDistribution,
            _ => _settings.HaltungDistribution,
        };
        var baum = SnapshotDistributionTree(cfg);
        var eigener = ResolveConfiguredDistributionRoot(cfg);
        if (eigener is not null)
            return new VerteilZiel(eigener, $"Eigener Hauptordner:{Environment.NewLine}{eigener}", baum);

        var unterordner = art == VerteilArt.Schaechte
            ? ProjectStructure.SchaechteVerteilt
            : ProjectStructure.HaltungenVerteilt;
        var projektordner = _shell.GetProjectFolder();
        return string.IsNullOrWhiteSpace(projektordner)
            ? new VerteilZiel(null,
                $"Projekt noch nicht gespeichert – der Zielordner wird beim Verteilen abgefragt (darin «{unterordner}»).",
                baum)
            : new VerteilZiel(
                Path.Combine(projektordner, unterordner),
                $"Projektordner \\ {unterordner}{Environment.NewLine}{Path.Combine(projektordner, unterordner)}",
                baum);
    }
}
