using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Nova, Aufklapp-Liste: der ausdrueckliche Sprung auf eine Haltung (Dossier, Karte, globale
/// Suche). Er ist etwas anderes als ein blosser Auswahlwechsel: In der Aufklapp-Liste wird die
/// Haltung dabei zusaetzlich aufgeklappt, waehrend Pfeiltasten nur die Auswahl verschieben.
/// </summary>
public sealed partial class DataPageViewModel
{
    internal void ZeigeProtokolleintrag(HaltungRecord record, Guid eintragId)
    {
        if (!_disposed && _shell.IsProjectReady && _shell.Project.Data.Contains(record))
            _protocolWindowController.Open(record, eintragId);
    }

    internal Task<bool> ZeigeProtokolleintragAsync(HaltungRecord record, Guid eintragId, CancellationToken ct,
        Func<bool>? istAktuell = null)
        => !_disposed && _shell.IsProjectReady && _shell.Project.Data.Contains(record)
            ? _protocolWindowController.OpenAsync(record, eintragId, ct, istAktuell)
            : Task.FromResult(false);

    /// <summary>Eine Haltung soll ausdruecklich gezeigt werden; die Seite entscheidet, wie.</summary>
    public event Action<HaltungRecord>? HaltungAnzeigen;

    private HaltungRecord? _offenerAnzeigeAuftrag;

    /// <summary>
    /// Waehlt die Haltung und meldet den Sprung. Beim Wechsel auf die Haltungsseite gibt es die
    /// Seite noch gar nicht — dann bleibt der Auftrag liegen, bis sie ihn mit
    /// <see cref="NimmAnzeigeAuftrag"/> abholt. Sonst ginge genau der erste Sprung verloren.
    /// </summary>
    public void ZeigeHaltung(HaltungRecord? record)
    {
        if (record is null)
            return;

        Selected = record;
        if (HaltungAnzeigen is { } melde)
            melde(record);
        else
            _offenerAnzeigeAuftrag = record;
    }

    /// <summary>Holt einen liegengebliebenen Auftrag ab; danach ist er verbraucht.</summary>
    public HaltungRecord? NimmAnzeigeAuftrag()
    {
        var record = _offenerAnzeigeAuftrag;
        _offenerAnzeigeAuftrag = null;
        return record;
    }
}
