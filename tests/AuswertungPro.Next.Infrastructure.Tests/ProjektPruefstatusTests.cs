using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProjektPruefstatusTests
{
    [Fact]
    public void Sanierung_abgeschlossen_verdeckt_keinen_offenen_Befund()
    {
        var h = new HaltungRecord { Protocol = new() { Current = new() { Entries = [new() { Ai = new() }] } } };
        h.SetFieldValue(FieldKeys.WorkflowStatus, "abgeschlossen", FieldSource.Manual, true);
        Assert.Equal(HaltungPruefstand.KiAnalysiert, HaltungPruefstatus.Bestimme(h));
        Assert.Same(h, NaechsteAufgabeRegel.Naechste([h]));
    }

    [Fact]
    public void Erledigt_ist_Arbeitsmarkierung_und_keine_KI_Freigabe()
    {
        var h = new HaltungRecord { BearbeitungErledigt = true };
        Assert.Equal(HaltungPruefstand.Abgeschlossen, HaltungPruefstatus.Bestimme(h));
        Assert.Equal("Bearbeitung erledigt", HaltungPruefstatus.Text(HaltungPruefstatus.Bestimme(h)));
        h.Protocol = new() { Current = new() { Entries = [new() { Ai = new() }] } };
        Assert.Equal(HaltungPruefstand.KiAnalysiert, HaltungPruefstatus.Bestimme(h));
        Assert.False(h.Protocol.Current.Entries[0].Ai!.Accepted);
        Assert.True(h.BearbeitungErledigt);
    }

    [Fact]
    public void Bestaetigte_KI_bleibt_unabhaengig_vom_Arbeits_und_Sanierungsstand()
    {
        var h = new HaltungRecord { BearbeitungErledigt = true,
            Protocol = new() { Current = new() { Entries = [new() { Ai = new() { Accepted = true } }] } } };
        h.SetFieldValue(FieldKeys.WorkflowStatus, "abgeschlossen", FieldSource.Manual, true);
        Assert.Equal(KiAmpel.Bestaetigt, HaltungZeilenStatus.Bestimme(h).Ampel);
        Assert.Equal("bestätigt", HaltungZeilenStatus.Bestimme(h).AmpelText);
    }
}
