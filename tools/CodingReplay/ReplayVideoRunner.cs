using System.Diagnostics;
using System.Globalization;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.UseCases.CodingReplay;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Coding;

namespace CodingReplay;

internal static class ReplayVideoRunner
{
    internal static Task<int> RunAsync(
        IReadOnlyDictionary<string, string> options,
        CancellationToken ct)
        => RunCoreAsync(options, null, ct);

    internal static Task<int> RunCandidateAsync(
        IReadOnlyDictionary<string, string> options,
        CancellationToken ct)
    {
        string Required(string key) => options.TryGetValue(key, out var value)
            ? value
            : throw new ArgumentException("Fehlt: " + key);
        var candidates = ReplayCandidateDetectionReader.Load(
            Required("--package"),
            Required("--candidate-detections"),
            Required("--candidate-sha256"));
        return RunCoreAsync(options, candidates, ct);
    }

    private static async Task<int> RunCoreAsync(
        IReadOnlyDictionary<string, string> options,
        ReplayCandidateDetectionSet? candidates,
        CancellationToken ct)
    {
        string Required(string key) => options.TryGetValue(key, out var value)
            ? value
            : throw new ArgumentException("Fehlt: " + key);

        var packageFolder = ReplayFiles.SafePath(Required("--package"));
        var package = ReplayVideoPackageBuilder.Load(packageFolder);
        var maxMinutes = int.Parse(options.GetValueOrDefault("--max-minutes", "300"), CultureInfo.InvariantCulture);
        var perFrameMinutes = int.Parse(options.GetValueOrDefault("--frame-timeout-minutes", "4"), CultureInfo.InvariantCulture);
        if (maxMinutes is < 1 or > 1440 || perFrameMinutes is < 1 or > 30)
            throw new ArgumentException("Ungueltige Laufgrenze.");

        var protectedRoots = package.Sources.Select(source => Path.GetDirectoryName(source.Path)!)
            .Append(Path.GetDirectoryName(package.Video.Path)!)
            .ToArray();
        var output = ReplayFiles.NewFolder(
            Required("--output"),
            candidates is null ? "videolauf" : "videolauf-kandidat",
            protectedRoots);
        ReplayFiles.WriteNew(
            Path.Combine(output, "video-package.json"),
            ReplayFiles.Read(Path.Combine(packageFolder, "video-package.json")));
        if (candidates is not null)
        {
            ReplayFiles.WriteNew(
                Path.Combine(output, "candidate-detections.json"),
                ReplayFiles.Read(candidates.FilePath));
        }
        Directory.CreateDirectory(Path.Combine(output, "osd-inputs"));
        Directory.CreateDirectory(Path.Combine(output, "frames"));
        var eventPhotoRoot = Path.Combine(output, "event-photos");
        Directory.CreateDirectory(eventPhotoRoot);

        var results = new List<ReplayVideoFrameObservation>(package.Frames.Count);
        ReplayVideoFinalization? finalization = null;
        string? interruption = null;
        using var totalDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        totalDeadline.CancelAfter(TimeSpan.FromMinutes(maxMinutes));

        try
        {
            var settingsPath = options.GetValueOrDefault(
                "--settings",
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SewerStudio",
                    "settings.json"));
            var settings = ReplayRuntime.Load(settingsPath);
            if (!settings.MultiModelEnabled)
                throw new InvalidOperationException("Mehrmodellweg ist in den aktuellen Einstellungen nicht aktiv.");
            if (!settings.SidecarUrl.IsLoopback || !settings.OllamaBaseUri.IsLoopback)
                throw new InvalidOperationException("Der Videonachlauf verwendet ausschliesslich lokale KI-Endpunkte.");

            var catalogPath = ReplayFiles.SafePath(Required("--catalog"));
            var packageCatalog = package.Sources.SingleOrDefault(source =>
                string.Equals(ReplayFiles.SafePath(source.Path), catalogPath, StringComparison.OrdinalIgnoreCase));
            if (packageCatalog is null
                || !ReplayFiles.HashFile(catalogPath).Equals(packageCatalog.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Der Laufkatalog stimmt nicht mit dem vorbereiteten Videopaket ueberein.");
            var catalog = new ManifestCodeCatalogProvider(catalogPath);
            if (catalog.LastLoadErrors.Count > 0 || catalog.AllowedCodes().Count == 0)
                throw new InvalidDataException("Katalog ist ungueltig.");
            VsaCodeResolver.ConfigureCatalog(catalog);

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            using var rawVision = new VisionPipelineClient(
                settings.SidecarUrl,
                http,
                SidecarTokenResolver.Resolve(settings.SidecarToken));
            var health = await rawVision.HealthCheckAsync(totalDeadline.Token);
            if (health is null)
                throw new InvalidOperationException("Sidecar ist nicht erreichbar; kein Frame wurde als schadensfrei gewertet.");
            ReplayFiles.WriteJson(Path.Combine(output, "runtime.json"), new
            {
                Path = candidates is null
                    ? "player_multimodel_fixed_video_frames_persistent_session"
                    : "player_multimodel_fixed_video_frames_persistent_session_development_candidate_unqualified",
                settings.VisionModel,
                settings.OllamaNumCtx,
                settings.OllamaKeepAlive,
                settings.OllamaRequestTimeout,
                OsdPrompt = CodingOsdMeterReader.Prompt,
                OsdTimeoutSeconds = 8,
                OsdTemperature = 0,
                OsdSeed = 42,
                HttpTimeoutSeconds = 180,
                settings.YoloConfidence,
                settings.DinoBoxThreshold,
                settings.DinoTextThreshold,
                settings.MultiModelEnabled,
                settings.PipelineMode,
                SettingsSha256 = File.Exists(settingsPath) ? ReplayFiles.HashFile(settingsPath) : null,
                CatalogSha256 = ReplayFiles.HashFile(catalogPath),
                Health = health,
                CandidateStatus = candidates is null ? null : "development_candidate_unqualified",
                CandidatePurpose = candidates?.Document.Purpose,
                CandidateId = candidates?.Document.CandidateId,
                CandidateRole = candidates?.Document.Model.Role,
                CandidateExpectedWeightsSha256 = candidates?.Document.Model.ExpectedWeightsSha256,
                CandidateActualWeightsSha256 = candidates?.Document.Model.ActualWeightsSha256,
                CandidateDetectionFileSha256 = candidates?.FileSha256,
                CandidateQualified = candidates is null ? (bool?)null : false,
                Code = new[]
                    {
                        typeof(CodingReplayUseCase).Assembly,
                        typeof(SingleFrameMultiModelService).Assembly,
                        typeof(CodingMultiModelInferenceWorkflow).Assembly,
                        typeof(ReplayCodingAnalyzer).Assembly
                    }
                    .Select(assembly => new
                    {
                        assembly.GetName().Name,
                        Sha256 = ReplayFiles.HashFile(assembly.Location)
                    })
                    .ToArray(),
                DetectorQualificationUnchanged = true,
                Limitations = new[]
                {
                    "Feste 5-Sekunden-Folge, keine Messung verworfener Live-Timer-Ticks",
                    "Nur vorhandene menschliche Ereignisanker werden bewertet",
                    "Weitere Ereigniszeilen bleiben fachlich unbewertet",
                    "Keine Precision-/Recall-Aussage fuer das vollstaendige Video"
                }
            });

            var vision = new ReplayVisionClient(rawVision);
            using var ollama = new OllamaClient(
                settings.OllamaBaseUri,
                ownedTimeout: settings.OllamaRequestTimeout,
                keepAlive: settings.OllamaKeepAlive,
                numCtx: settings.OllamaNumCtx);
            string? currentFrameId = null;
            Exception? osdEvidenceError = null;
            using var osd = new CodingOsdMeterService((search, token) =>
            {
                try
                {
                    if (currentFrameId is null)
                        throw new InvalidOperationException("Keine aktive Videobildkennung.");
                    ReplayFiles.WriteNew(
                        Path.Combine(output, "osd-inputs", currentFrameId + ".png"),
                        search);
                }
                catch (Exception ex)
                {
                    osdEvidenceError = ex;
                    throw;
                }
                return ollama.ChatWithOptionsAsync(
                    settings.VisionModel,
                    [new OllamaClient.ChatMessage(
                        "user",
                        CodingOsdMeterReader.Prompt,
                        [Convert.ToBase64String(search)])],
                    CodingOsdMeterReader.CreateOllamaOptions(),
                    token);
            });
            var analyzer = new ReplayCodingAnalyzer(
                vision,
                new SingleFrameMultiModelService(
                    vision,
                    settings.YoloConfidence,
                    settings.DinoBoxThreshold,
                    settings.DinoTextThreshold),
                osd,
                new CodeCatalogSelectionCatalog(catalog));
            var context = new ReplayVideoAnalysisContext(
                package.Holding,
                package.Video.Path,
                package.DiameterMm,
                package.ReachLengthM,
                package.Video.DurationSeconds,
                eventPhotoRoot);
            var session = candidates is null
                ? analyzer.StartVideoSession(context)
                : analyzer.StartCandidateVideoSession(context, candidates);

            for (var index = 0; index < package.Frames.Count; index++)
            {
                totalDeadline.Token.ThrowIfCancellationRequested();
                var frame = package.Frames[index];
                currentFrameId = frame.Id;
                var image = ReplayFiles.Read(Path.Combine(packageFolder, frame.Image), 32 * 1024 * 1024);
                if (!ReplayFiles.Hash(image).Equals(frame.ImageSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Videobild {frame.Id} stimmt nicht mit der Paket-Pruefsumme ueberein.");

                using var frameDeadline = CancellationTokenSource.CreateLinkedTokenSource(totalDeadline.Token);
                frameDeadline.CancelAfter(TimeSpan.FromMinutes(perFrameMinutes));
                ReplayVideoFrameObservation observation;
                var watch = Stopwatch.StartNew();
                try
                {
                    observation = await session.AnalyzeFrameAsync(frame, image, frameDeadline.Token);
                }
                catch (OperationCanceledException) when (totalDeadline.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    observation = new ReplayVideoFrameObservation(
                        frame.Id,
                        frame.TimestampSeconds,
                        "Timeout",
                        0,
                        "unknown",
                        [],
                        session.Events,
                        new Dictionary<string, string>
                        {
                            ["elapsed_ms"] = watch.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture)
                        },
                        "Bildzeitlimit erreicht.");
                }
                catch (Exception ex)
                {
                    observation = new ReplayVideoFrameObservation(
                        frame.Id,
                        frame.TimestampSeconds,
                        "TechnicalError",
                        0,
                        "unknown",
                        [],
                        session.Events,
                        new Dictionary<string, string>
                        {
                            ["elapsed_ms"] = watch.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture)
                        },
                        ex.Message);
                }

                if (osdEvidenceError is not null)
                    throw new IOException("OSD-Eingabebeleg konnte nicht geschrieben werden.", osdEvidenceError);
                ReplayFiles.WriteJson(Path.Combine(output, "frames", frame.Id + ".json"), observation);
                results.Add(observation);
                Console.WriteLine(
                    $"{results.Count}/{package.Frames.Count}: {frame.TimestampSeconds:F0}s " +
                    $"{observation.Outcome}, {observation.Events.Count} Ereignisse, " +
                    $"{observation.TechnicalError}");
            }

            finalization = session.FinalizeSequence();
            ReplayFiles.WriteJson(Path.Combine(output, "final-events.json"), finalization);
        }
        catch (OperationCanceledException)
        {
            interruption = "Abbruch oder Gesamtzeitlimit. Bereits geschriebene Frame-Belege bleiben erhalten.";
        }
        catch (Exception ex)
        {
            interruption = ex.Message;
        }

        ReplayVideoReport.Write(output, package, results, finalization, interruption);
        Console.WriteLine(Path.Combine(output, "video-bericht.html"));
        return interruption is null
               && results.Count == package.Frames.Count
               && results.All(result => result.TechnicalError is null)
               && finalization?.CanExit == true
            ? 0
            : 2;
    }
}
