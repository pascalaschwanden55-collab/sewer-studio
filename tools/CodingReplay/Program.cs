using System.Globalization;
using System.Text.Json;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.UseCases.CodingReplay;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Coding;
using CodingReplay;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try { return ExecuteAsync(args).GetAwaiter().GetResult(); }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 2; }
    }

    private static async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "help")
        {
            Console.WriteLine("CodingReplay prepare --eval-root <Ordner> --review <JSON> --project <projekt.json> --output <Arbeitsordner>");
            Console.WriteLine("CodingReplay run --package <Messpaket> --output <Arbeitsordner> --catalog <Manifest> [--limit 6] [--max-minutes 20] [--settings <JSON>]");
            Console.WriteLine("CodingReplay prepare-video --video <Datei> --eval-root <Ordner> --review <JSON> --project <projekt.json> --catalog <Manifest> --holding <Name> --ffmpeg <ffmpeg.exe> --output <Arbeitsordner> [--step-seconds 5]");
            Console.WriteLine("CodingReplay run-video --package <Videopaket> --output <Arbeitsordner> --catalog <Manifest> [--max-minutes 300] [--frame-timeout-minutes 4] [--settings <JSON>]");
            Console.WriteLine("CodingReplay run-video-candidate --package <Videopaket> --candidate-detections <JSON> --candidate-sha256 <SHA-256> --output <Arbeitsordner> --catalog <Manifest> [--max-minutes 300] [--frame-timeout-minutes 4] [--settings <JSON>]");
            return 0;
        }
        if ((args.Length - 1) % 2 != 0) throw new ArgumentException("Option ohne Wert.");
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
            if (!options.TryAdd(args[i], args[i + 1])) throw new ArgumentException("Doppelte Option.");
        var allowed = args[0] switch
        {
            "prepare" => new[] { "--eval-root", "--review", "--project", "--output" },
            "run" => new[] { "--package", "--output", "--catalog", "--limit", "--max-minutes", "--settings" },
            "prepare-video" => new[] { "--video", "--eval-root", "--review", "--project", "--catalog", "--holding", "--ffmpeg", "--output", "--step-seconds" },
            "run-video" => new[] { "--package", "--output", "--catalog", "--max-minutes", "--frame-timeout-minutes", "--settings" },
            "run-video-candidate" => new[] { "--package", "--candidate-detections", "--candidate-sha256", "--output", "--catalog", "--max-minutes", "--frame-timeout-minutes", "--settings" },
            _ => throw new ArgumentException("Erwartet prepare, run, prepare-video, run-video oder run-video-candidate.")
        };
        if (options.Keys.Except(allowed).Any()) throw new ArgumentException("Unbekannte Option.");
        string Required(string key) => options.TryGetValue(key, out var value) ? value : throw new ArgumentException("Fehlt: " + key);
        if (args[0] == "prepare")
        {
            Console.WriteLine(ReplayPackageBuilder.Prepare(Required("--eval-root"), Required("--review"), Required("--project"), Required("--output")));
            return 0;
        }
        if (args[0] == "prepare-video")
        {
            var step = double.Parse(options.GetValueOrDefault("--step-seconds", "5"), CultureInfo.InvariantCulture);
            Console.WriteLine(await ReplayVideoPackageBuilder.PrepareAsync(
                Required("--video"), Required("--eval-root"), Required("--review"),
                Required("--project"), Required("--catalog"), Required("--holding"),
                Required("--ffmpeg"), Required("--output"), step, CancellationToken.None));
            return 0;
        }
        using var lease = new Mutex(false, "Local\\SewerStudio-CodingReplay");
        bool acquired;
        try { acquired = lease.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) throw new InvalidOperationException("Es laeuft bereits ein Bildvergleich.");
        // Der CLI-Prozess besitzt den Mutex bis zum Ende. Kein threadgebundenes Release nach await.
        if (args[0] is "run-video" or "run-video-candidate")
        {
            using var videoStop = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; videoStop.Cancel(); };
            return args[0] == "run-video"
                ? await ReplayVideoRunner.RunAsync(options, videoStop.Token)
                : await ReplayVideoRunner.RunCandidateAsync(options, videoStop.Token);
        }
        var packageFolder = ReplayFiles.SafePath(Required("--package"));
        var package = ReplayPackageBuilder.Load(packageFolder);
        var limit = int.Parse(options.GetValueOrDefault("--limit", "6"), CultureInfo.InvariantCulture);
        var minutes = int.Parse(options.GetValueOrDefault("--max-minutes", "20"), CultureInfo.InvariantCulture);
        if (limit < 1 || limit > package.Cases.Count || minutes < 1 || minutes > 120) throw new ArgumentException("Ungueltige Laufgrenze.");
        var cases = package.Cases.Take(limit).ToArray();
        var output = ReplayFiles.NewFolder(Required("--output"), "lauf", [@"C:\KI_BRAIN", @"D:\Haltungen",
            .. package.Sources.Select(s => Path.GetDirectoryName(s.Path)!)]);
        ReplayFiles.WriteNew(Path.Combine(output, "package.json"), ReplayFiles.Read(Path.Combine(packageFolder, "package.json")));
        var results = new List<CodingReplayResult>();
        string? interruption = null;
        using var stop = new CancellationTokenSource(TimeSpan.FromMinutes(minutes));
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
        try
        {
            var settingsPath = options.GetValueOrDefault("--settings", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SewerStudio", "settings.json"));
            var settings = ReplayRuntime.Load(settingsPath);
            if (!settings.MultiModelEnabled) throw new InvalidOperationException("Mehrmodellweg ist in den aktuellen Einstellungen nicht aktiv. Lauf wurde nicht als aktueller Codiermodus ausgefuehrt.");
            if (!settings.SidecarUrl.IsLoopback || !settings.OllamaBaseUri.IsLoopback)
                throw new InvalidOperationException("Der Messlauf verwendet ausschliesslich lokale KI-Endpunkte.");
            var catalogPath = ReplayFiles.SafePath(Required("--catalog"));
            var catalog = new ManifestCodeCatalogProvider(catalogPath);
            if (catalog.LastLoadErrors.Count > 0 || catalog.AllowedCodes().Count == 0) throw new InvalidDataException("Katalog ist ungueltig.");
            VsaCodeResolver.ConfigureCatalog(catalog);
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            using var rawVision = new VisionPipelineClient(settings.SidecarUrl, http, SidecarTokenResolver.Resolve(settings.SidecarToken));
            var health = await rawVision.HealthCheckAsync(stop.Token);
            if (health is null) throw new InvalidOperationException("Sidecar ist nicht erreichbar; kein Bild wurde als schadensfrei gewertet.");
            ReplayFiles.WriteJson(Path.Combine(output, "runtime.json"), new
            {
                Path = "player_multimodel_fixed_frames", settings.VisionModel, settings.OllamaNumCtx,
                settings.OllamaKeepAlive, settings.OllamaRequestTimeout,
                OsdPrompt = CodingOsdMeterReader.Prompt, OsdTimeoutSeconds = 8, HttpTimeoutSeconds = 180,
                settings.YoloConfidence, settings.DinoBoxThreshold, settings.DinoTextThreshold,
                settings.MultiModelEnabled, settings.PipelineMode,
                SettingsSha256 = File.Exists(settingsPath) ? ReplayFiles.Hash(ReplayFiles.Read(settingsPath)) : null,
                CatalogSha256 = ReplayFiles.Hash(ReplayFiles.Read(catalogPath)), Health = health,
                Code = new[] { typeof(CodingReplayUseCase).Assembly, typeof(SingleFrameMultiModelService).Assembly,
                    typeof(CodingMultiModelInferenceWorkflow).Assembly, typeof(ReplayCodingAnalyzer).Assembly }
                    .Select(a => new { a.GetName().Name, Sha256 = ReplayFiles.Hash(ReplayFiles.Read(a.Location)) }).ToArray(),
                Limitations = new[] { "Keine vollstaendige Gewichtsbindung des Sidecars", "Einzelbilder ohne Historie oder Kalibrierung", "Meter nur aus OSD; keine Timeline-Schaetzung", "Keine Bildauswahl-/Live-Taktmessung" }
            });
            var vision = new ReplayVisionClient(rawVision);
            using var ollama = new OllamaClient(settings.OllamaBaseUri,
                ownedTimeout: settings.OllamaRequestTimeout, keepAlive: settings.OllamaKeepAlive, numCtx: settings.OllamaNumCtx);
            Directory.CreateDirectory(Path.Combine(output, "osd-inputs"));
            string? currentCaseId = null;
            Exception? osdEvidenceError = null;
            using var osd = new CodingOsdMeterService((search, ct) =>
            {
                try
                {
                    if (currentCaseId is null) throw new InvalidOperationException("Keine aktive Bildkennung.");
                    ReplayFiles.WriteNew(Path.Combine(output, "osd-inputs", currentCaseId + ".png"), search);
                }
                catch (Exception ex) { osdEvidenceError = ex; throw; }
                return ollama.ChatAsync(settings.VisionModel,
                    [new OllamaClient.ChatMessage("user", CodingOsdMeterReader.Prompt, [Convert.ToBase64String(search)])], ct);
            });
            var analyzer = new ReplayCodingAnalyzer(vision,
                new SingleFrameMultiModelService(vision, settings.YoloConfidence, settings.DinoBoxThreshold, settings.DinoTextThreshold),
                osd, new CodeCatalogSelectionCatalog(catalog));
            var byId = cases.ToDictionary(c => c.Input.Id);
            await new CodingReplayUseCase(analyzer).RunAsync(cases.Select(c => c.Input).ToArray(), new(
                (id, ct) => { ct.ThrowIfCancellationRequested(); currentCaseId = id; return Task.FromResult(ReplayFiles.Read(Path.Combine(packageFolder, byId[id].Image), 32 * 1024 * 1024)); },
                (result, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    ReplayFiles.WriteJson(Path.Combine(output, result.Id + ".json"), result);
                    results.Add(result);
                    Console.WriteLine($"{results.Count}/{cases.Length}: {result.Id} {result.Status} {result.Observation?.Outcome} {result.Error}");
                    if (osdEvidenceError is not null) throw new IOException("OSD-Eingabebeleg konnte nicht geschrieben werden.", osdEvidenceError);
                    return Task.CompletedTask;
                }), TimeSpan.FromMinutes(4), stop.Token);
        }
        catch (OperationCanceledException) { interruption = "Abbruch oder Gesamtzeitlimit. Bereits geschriebene Ergebnisse bleiben erhalten."; }
        catch (Exception ex) { interruption = ex.Message; }
        ReplayReport.Write(output, packageFolder, cases, results, interruption);
        Console.WriteLine(Path.Combine(output, "bericht.html"));
        return interruption is null && results.All(r => r.Status == "measured") ? 0 : 2;
    }
}
