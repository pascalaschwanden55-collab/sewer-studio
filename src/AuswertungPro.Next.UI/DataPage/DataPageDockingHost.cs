using System;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Abdocken und Andocken der Haltungsseite: Die gerade gezeigte Ansicht (Tabelle ODER alte
/// Haltungsansicht) wandert in ein eigenes Fenster und kommt an dieselbe Stelle zurueck.
///
/// Der Ablauf ist unveraendert aus <c>DataPage.RecordInteractions.cs</c> hierher gezogen — die
/// Seite reicht nur ihre Elemente herein. Die Entscheidung, WELCHE Ansicht abgedockt wird,
/// trifft weiterhin <see cref="GridDockingController"/>.
/// </summary>
public sealed class DataPageDockingHost
{
    /// <summary>Die benannten Elemente der Seite aus DataPage.xaml.</summary>
    public sealed record Elemente(
        Panel GridHost,
        UIElement Tabelle,
        UIElement AlteAnsicht,
        UIElement Platzhalter,
        Control AbdockenSchalter,
        MenuItem AlteAnsichtSchalter);

    private readonly Elemente _e;
    private readonly Func<DataPageViewModel?> _vm;
    private readonly Action<string, string> _warnen;
    private readonly Action _ansichtAnwenden;

    private FloatingGridWindow? _fenster;
    // Merkt sich, welche Ansicht (Tabelle ODER Haltungsansicht) gerade abgedockt ist,
    // damit sie beim Andocken an die richtige Stelle zurueckkommt.
    private UIElement? _abgedockteAnsicht;
    private bool _laeuftGerade;

    /// <param name="ansichtAnwenden">
    /// Wird nach jedem Wechsel gerufen. Andocken macht die Ansicht wieder sichtbar, und erst
    /// danach steht fest, welche Menuepunkte gelten (in der Aufklapp-Liste ist Abdocken gesperrt).
    /// </param>
    public DataPageDockingHost(
        Elemente elemente,
        Func<DataPageViewModel?> viewModel,
        Action<string, string> warnen,
        Action ansichtAnwenden)
    {
        _e = elemente ?? throw new ArgumentNullException(nameof(elemente));
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _warnen = warnen ?? throw new ArgumentNullException(nameof(warnen));
        _ansichtAnwenden = ansichtAnwenden ?? throw new ArgumentNullException(nameof(ansichtAnwenden));
    }

    /// <summary>Steht die Ansicht gerade in einem eigenen Fenster?</summary>
    public bool IstAbgedockt => _fenster is not null;

    public void Abdocken()
    {
        if (_fenster is not null)
        {
            _fenster.Activate();
            return;
        }

        try
        {
            // Guard-Flag setzen damit der Unloaded-Handler nicht interferiert
            _laeuftGerade = true;

            // Die aktuell gezeigte Ansicht abdocken: Haltungsansicht wenn der Umschalter
            // an ist, sonst die Tabelle. Umschalter sperren, solange abgedockt.
            var aktiv = GridDockingController.ResolveActiveView(
                _e.AlteAnsichtSchalter.IsChecked,
                _e.AlteAnsicht,
                _e.Tabelle);
            _abgedockteAnsicht = aktiv;
            _e.AlteAnsichtSchalter.IsEnabled = false;

            // FloatingGridWindow erstellen (VOR dem Entfernen der Ansicht!)
            _fenster = new FloatingGridWindow();
            _fenster.DockBackRequested += Andocken;
            _fenster.Closed += Fenster_Geschlossen;

            // DataContext auf FloatingWindow setzen (damit Bindings funktionieren)
            _fenster.DataContext = _vm();

            // Aktive Ansicht aus dem visuellen Baum entfernen und ins Floating-Fenster verschieben
            GridDockingController.ApplyUndockedState(
                _e.GridHost,
                aktiv,
                _e.Platzhalter,
                _e.AbdockenSchalter,
                _e.AlteAnsichtSchalter);
            _fenster.SetGridContent(aktiv);
            _ansichtAnwenden();

            // Fensterposition aus Settings laden
            var settings = _vm()?.Settings;
            _fenster.ApplySavedBounds(settings?.FloatingGridBounds);

            // Titel und Info aktualisieren
            AktualisiereFensterinfo();

            _fenster.Show();

            // Settings merken
            if (settings is not null)
                settings.IsGridFloating = true;
        }
        catch (Exception ex)
        {
            // Bei Fehler: alles zuruecksetzen
            BestEffort.ReportWarning($"[DataPage] Abdocken fehlgeschlagen: {ex}");
            _warnen($"Fehler beim Abdocken:\n{UserError.Describe(ex)}", "Abdocken");

            // Abgedockte Ansicht zuruecksetzen falls sie schon entfernt wurde
            GridDockingController.RestoreDockedState(
                _e.GridHost,
                view: _abgedockteAnsicht,
                fallbackView: null,
                _e.Platzhalter,
                _e.AbdockenSchalter,
                _e.AlteAnsichtSchalter);
            _abgedockteAnsicht = null;

            if (_fenster is not null)
            {
                _fenster.DockBackRequested -= Andocken;
                _fenster.Closed -= Fenster_Geschlossen;
                try
                {
                    _fenster.Close();
                }
                catch (Exception closeEx)
                {
                    BestEffort.ReportWarning($"[DataPage] Aufraeumen des abgedockten Fensters fehlgeschlagen: {closeEx}");
                }
                _fenster = null;
            }
        }
        finally
        {
            _laeuftGerade = false;
        }
    }

    public void Andocken()
    {
        if (_fenster is null)
            return;

        // Fensterposition speichern
        var settings = _vm()?.Settings;
        if (settings is not null)
        {
            settings.FloatingGridBounds = _fenster.GetBoundsString();
            settings.IsGridFloating = false;
        }

        // Ansicht aus dem Floating-Fenster entfernen
        var ansicht = _fenster.RemoveGridContent();
        _fenster.DockBackRequested -= Andocken;
        _fenster.Closed -= Fenster_Geschlossen;
        _fenster.Close();
        _fenster = null;

        HoleAnsichtZurueck(ansicht);
    }

    /// <summary>
    /// Die Seite wird verlassen: Eine abgedockte Ansicht kommt zurueck — aber nicht mitten im
    /// Abdocken selbst, sonst hebt der Unloaded-Handler den gerade laufenden Vorgang auf.
    /// </summary>
    public void BeimVerlassen()
    {
        if (_fenster is not null && !_laeuftGerade)
            Andocken();
    }

    // Holt die abgedockte Ansicht (Tabelle ODER Haltungsansicht) zurueck in den GridHost.
    private void HoleAnsichtZurueck(UIElement? ansicht)
    {
        if (GridDockingController.RestoreDockedState(
            _e.GridHost,
            ansicht,
            _abgedockteAnsicht,
            _e.Platzhalter,
            _e.AbdockenSchalter,
            _e.AlteAnsichtSchalter))
        {
            _abgedockteAnsicht = null;
        }

        _ansichtAnwenden();
    }

    private void Fenster_Geschlossen(object? sender, EventArgs e)
    {
        // Wenn das Floating-Fenster geschlossen wird (X-Button), Grid zurueck docken
        if (_fenster is null)
            return;

        var settings = _vm()?.Settings;
        if (settings is not null)
        {
            settings.FloatingGridBounds = _fenster.GetBoundsString();
            settings.IsGridFloating = false;
        }

        var ansicht = _fenster.RemoveGridContent();
        _fenster.DockBackRequested -= Andocken;
        _fenster = null;

        HoleAnsichtZurueck(ansicht);
    }

    private void AktualisiereFensterinfo()
    {
        if (_fenster is null)
            return;

        var vm = _vm();
        var projektname = vm?.Project?.Name;
        var anzahl = vm?.Records?.Count ?? 0;
        var gewaehlt = vm?.Selected?.GetFieldValue("Haltungsname");
        _fenster.UpdateInfo(projektname, anzahl, gewaehlt);
    }
}
