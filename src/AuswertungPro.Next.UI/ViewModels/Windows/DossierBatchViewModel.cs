using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

/// <summary>Eine Zeile der Vorschlagsliste.</summary>
public sealed class DossierBatchRow : INotifyPropertyChanged
{
    private bool _isSelected;

    public DossierBatchRow(DossierProposal proposal)
    {
        Proposal = proposal ?? throw new ArgumentNullException(nameof(proposal));
        Holdings = proposal.Holdings.Select(h => new DossierBatchHoldingRow(h)).ToList();
        _isSelected = proposal.Selectable;

        // Das Abhaken einer einzelnen Leitung in der aufklappbaren Zeilenansicht
        // muss die Zusammenfassung ("4 von 6 Leitungen") sofort nachziehen.
        foreach (var leitung in Holdings)
            leitung.PropertyChanged += (_, _) => Melde(nameof(HoldingSummary));
    }

    public DossierProposal Proposal { get; }

    public IReadOnlyList<DossierBatchHoldingRow> Holdings { get; }

    public bool CanSelect => Proposal.Selectable;

    public string ParcelNumber => Proposal.Parcel.Number;

    public string Name => Proposal.SuggestedName;

    public string OwnerSummary => Proposal.Registry is null
        ? Proposal.SkipReason
        : string.Join(" / ", Proposal.Registry.Owners.Select(o => o.Name));

    /// <summary>Zaehlt nur die angehakten Leitungen; der Rest ist Hinweis.</summary>
    public string HoldingSummary
        => $"{Holdings.Count(h => h.IsSelected)} von {Holdings.Count} Leitungen";

    public string SkipReason => Proposal.SkipReason;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            // Ein gesperrter Vorschlag bleibt gesperrt, auch wenn jemand klickt.
            var neu = value && CanSelect;

            if (neu == _isSelected)
            {
                // Der geklemmte Wert stimmt mit dem alten ueberein — die Bindung
                // hat das Kaestchen aber schon selbst umgestellt. Ohne Meldung
                // bliebe es sichtbar angehakt, obwohl es nichts bewirkt.
                if (value != neu)
                    Melde();

                return;
            }

            _isSelected = neu;
            Melde();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Melde([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Eine Leitung innerhalb einer Zeile.</summary>
public sealed class DossierBatchHoldingRow : INotifyPropertyChanged
{
    private bool _isSelected;

    public DossierBatchHoldingRow(ProposedHolding holding)
    {
        Holding = holding ?? throw new ArgumentNullException(nameof(holding));
        _isSelected = holding.Preselected;
    }

    public ProposedHolding Holding { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value == _isSelected)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public string Designation => Holding.Designation;

    /// <summary>
    /// Warum die Leitung so eingestuft ist. "aus dem Leitungsnamen" ist wichtig:
    /// dort ist "privat" eine Annahme aus der Knotenform, keine Auskunft des
    /// Kantons — das muss der Mensch sehen koennen.
    /// </summary>
    public string Note => Holding switch
    {
        { IsPrivate: false } => "gehört dem Werk",
        { InProject: false } => "nicht im Projekt",
        { Origin: "Name" } => "aus dem Leitungsnamen — privat angenommen",
        _ => ""
    };

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Der Zustand des Stapelanlage-Fensters. Enthaelt keine Regel und kein Netz —
/// er nimmt ein fertiges Ergebnis entgegen und gibt die Auswahl zurueck.
/// </summary>
public sealed class DossierBatchViewModel : INotifyPropertyChanged
{
    private string _warningText = string.Empty;

    public ObservableCollection<DossierBatchRow> Rows { get; } = new();

    public string WarningText
    {
        get => _warningText;
        private set
        {
            _warningText = value;
            Melde();
        }
    }

    public int SelectedCount => Rows.Count(r => r.IsSelected);

    public void Uebernehmen(DossierBatchProposalResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Rows.Clear();
        foreach (var vorschlag in result.Proposals)
            Rows.Add(new DossierBatchRow(vorschlag));

        WarningText = result.Warnings.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, result.Warnings);

        Melde(nameof(SelectedCount));
    }

    public IReadOnlyList<DossierCreationSelection> BaueAuswahl()
        => Rows
            .Where(r => r.IsSelected)
            .Select(r => new DossierCreationSelection(
                r.Proposal,
                r.Holdings.Where(h => h.IsSelected).Select(h => h.Designation).ToList()))
            .ToList();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Melde([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
