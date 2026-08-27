using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Fehlerpruefung 11.07., Kritisch 2: Nur ausgewaehlte KI-Eintraege gelangen ins
/// Fachprotokoll; uebernommene tragen Ai.Accepted=true (echte Nutzerentscheidung).
/// </summary>
public sealed class AiProtocolAcceptancePolicyTests
{
    private static ProtocolDocument DokumentMit(params Guid[] entryIds)
    {
        var doc = new ProtocolDocument
        {
            Original = new ProtocolRevision(),
            Current = new ProtocolRevision()
        };
        foreach (var id in entryIds)
        {
            foreach (var rev in new[] { doc.Original, doc.Current })
            {
                rev.Entries.Add(new ProtocolEntry
                {
                    EntryId = id,
                    Code = "BAB",
                    Source = ProtocolEntrySource.Ai,
                    Ai = new ProtocolEntryAiMeta { SuggestedCode = "BAB" }
                });
                rev.Changes.Add(new ProtocolChange { EntryId = id, Kind = ProtocolChangeKind.Add });
            }
        }
        return doc;
    }

    [Fact]
    public void NurAusgewaehlte_bleiben_und_werden_als_Accepted_markiert()
    {
        var behalten = Guid.NewGuid();
        var verworfen = Guid.NewGuid();
        var doc = DokumentMit(behalten, verworfen);

        AiProtocolAcceptancePolicy.Apply(doc, new HashSet<Guid> { behalten });

        Assert.Single(doc.Current.Entries);
        Assert.Equal(behalten, doc.Current.Entries[0].EntryId);
        Assert.True(doc.Current.Entries[0].Ai!.Accepted);
        Assert.Single(doc.Original.Entries); // beide Revisionen gefiltert
        Assert.Single(doc.Current.Changes);  // Aenderungsliste mitgefiltert
    }

    [Fact]
    public void LeereAuswahl_ergibt_leeres_Protokoll()
    {
        var doc = DokumentMit(Guid.NewGuid(), Guid.NewGuid());

        AiProtocolAcceptancePolicy.Apply(doc, new HashSet<Guid>());

        Assert.Empty(doc.Current.Entries);
        Assert.Empty(doc.Original.Entries);
    }

    [Fact]
    public void KI_Vorschlaege_verlangen_weiterhin_eine_ausdrueckliche_Annahme()
    {
        var imported = Event(ProtocolEntrySource.Imported);
        var aiAccepted = Event(ProtocolEntrySource.Ai, aiDecision: CodingUserDecision.Accepted);
        var aiRejected = Event(ProtocolEntrySource.Ai, aiDecision: CodingUserDecision.Rejected);
        var aiOpen = Event(ProtocolEntrySource.Ai, aiDecision: CodingUserDecision.Ignored);
        var manualAccepted = Event(ProtocolEntrySource.Manual, reviewDecision: CodingUserDecision.Accepted);

        var result = AiProtocolAcceptancePolicy.FilterCodingEvents(
            [imported, aiAccepted, aiRejected, aiOpen, manualAccepted]);

        Assert.Equal([imported, aiAccepted, manualAccepted], result);
    }

    [Fact]
    public void Selbst_codierter_Eintrag_geht_auch_ohne_Bestaetigung_ins_Protokoll()
    {
        // Regression: Eine im VSA-Codierfenster erzeugte Handcodierung startet als
        // "Manuell codiert - bitte bestaetigen" (Ignored). Sie verschwand beim
        // "Uebernehmen" kommentarlos - unter anderem Rohranfang und Rohrende.
        var manualOpen = Event(ProtocolEntrySource.Manual, reviewDecision: CodingUserDecision.Ignored);

        Assert.True(AiProtocolAcceptancePolicy.CanApply(manualOpen));
    }

    [Fact]
    public void Selbst_codierter_Eintrag_bleibt_nach_Ablehnen_draussen()
    {
        var manualRejected = Event(ProtocolEntrySource.Manual, reviewDecision: CodingUserDecision.Rejected);

        Assert.False(AiProtocolAcceptancePolicy.CanApply(manualRejected));
    }

    [Fact]
    public void Ein_KI_Vorschlag_mit_Pruefkontext_bleibt_bestaetigungspflichtig()
    {
        // Beide Kontexte vorhanden: der KI-Kontext entscheidet, nicht der Pruefkontext.
        var event_ = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BCE", Source = ProtocolEntrySource.Ai },
            AiContext = new CodingEventAiContext { Decision = CodingUserDecision.Ignored },
            ReviewContext = new CodingEventReviewContext { Decision = CodingUserDecision.Ignored }
        };

        Assert.False(AiProtocolAcceptancePolicy.CanApply(event_));
    }

    private static CodingEvent Event(
        ProtocolEntrySource source,
        CodingUserDecision? aiDecision = null,
        CodingUserDecision? reviewDecision = null)
        => new()
        {
            Entry = new ProtocolEntry { Code = "BAB", Source = source },
            AiContext = aiDecision.HasValue
                ? new CodingEventAiContext { Decision = aiDecision.Value }
                : null,
            ReviewContext = reviewDecision.HasValue
                ? new CodingEventReviewContext { Decision = reviewDecision.Value }
                : null
        };
}
