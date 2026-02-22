// AuswertungPro – KI Videoanalyse Modul
using System.Net.Http;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Analysis;
using AuswertungPro.Next.UI.Ai.Analysis.Models;
using AuswertungPro.Next.UI.Ai.Analysis.Services;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Ollama;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Fenster für die 2-Stufen-KI-Videoanalyse.
/// Ergebnis (FinalResult) steht nach DialogResult=true zur Verfügung.
/// </summary>
public partial class VideoAnalysisWindow : Window
{
    public VideoAnalysisViewModel Vm { get; }

    /// <summary>
    /// Erzeugt das Fenster mit minimal benötigten Abhängigkeiten.
    /// Ollama-Konfiguration wird aus Umgebungsvariablen geladen.
    /// </summary>
    public VideoAnalysisWindow(
        double meterStart = 0,
        double meterEnd   = 100)
    {
        InitializeComponent();

        var config   = OllamaConfig.Load();
        var http     = new HttpClient { Timeout = config.RequestTimeout };
        var client   = new OllamaClient(config.BaseUri, http);

        var kbCtx    = new KnowledgeBaseContext();
        var embedder = new EmbeddingService(http, config);
        var retrieval = new RetrievalService(kbCtx, embedder);

        var vision         = new VisionDetectionService(client, config);
        var classification = new ClassificationService(client, config, retrieval);

        Vm = new VideoAnalysisViewModel(vision, classification)
        {
            MeterStart = meterStart,
            MeterEnd   = meterEnd
        };
        DataContext = Vm;

        Closed += (_, _) => kbCtx.Dispose();
    }

    /// <summary>Gibt das Analyseergebnis zurück (nach DialogResult=true).</summary>
    public System.Collections.Generic.IReadOnlyList<AnalysisObservation>? Result
        => Vm.FinalResult;

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.IsRunning)
        {
            Vm.CancelCommand.Execute(null);
            return;
        }
        DialogResult = false;
        Close();
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
