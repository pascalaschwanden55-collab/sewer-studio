// Phase 5.7 (Reflection-Reduktion) — DEMO-Migration:
// Frueher griff dieser Test via Reflection auf das private Feld
// `OllamaClient._http` zu, um BaseAddress und Timeout zu verifizieren.
// Mit `InternalsVisibleTo("AuswertungPro.Next.Pipeline.Tests")` (bereits
// in AuswertungPro.Next.Infrastructure.csproj gesetzt) und einem
// `internal HttpClient HttpForTesting`-Property im OllamaClient ist der
// Reflection-Umweg ueberfluessig. Verhalten und Test-Annahmen sind
// unveraendert — nur die Zugriffsart wurde gewechselt.
//
// Folge-Slice (NICHT in dieser Migration enthalten): die anderen 5
// Reflection-Tests (TrainingRunsStore, OperateurAnnotationServiceCommit,
// VsaYoloClassMapTryGet, TrainingSamplesWriterAdapter,
// HoldingFolderDistributorVideoMatching) — gehoeren in einen separaten
// Phase-5.7-Sprint, weil sie statische private Felder eines anderen
// Layers (Application) und KnowledgeRootProvider-Resolver-Felder beruehren.
using System;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using System.Net.Http;

namespace AuswertungPro.Next.Pipeline.Tests;

[Trait("Category", "Unit")]
public sealed class OllamaClientTests
{
    [Fact]
    public void Constructor_OwnedClient_UsesConfiguredTimeout()
    {
        var uri = new Uri("http://localhost:11434");
        var client = new OllamaClient(uri, ownedTimeout: TimeSpan.FromMinutes(42));

        var http = client.HttpForTesting;

        Assert.Equal(uri, http.BaseAddress);
        Assert.Equal(TimeSpan.FromMinutes(42), http.Timeout);
    }

    [Fact]
    public void Constructor_ProvidedClient_PreservesExistingTimeout()
    {
        var uri = new Uri("http://localhost:11434");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

        _ = new OllamaClient(uri, http, TimeSpan.FromMinutes(42));

        Assert.Equal(uri, http.BaseAddress);
        Assert.Equal(TimeSpan.FromMinutes(3), http.Timeout);
    }
}
