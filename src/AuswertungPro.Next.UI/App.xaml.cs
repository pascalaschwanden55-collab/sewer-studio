
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Composition;
using AuswertungPro.Next.UI.Views.Windows;
using AuswertungPro.Next.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI
{
    public partial class App : System.Windows.Application
    {
    

        // Phase 5.1.B Etappe 4 Sub-E: Legacy-ServiceProvider entfernt — nur noch DI-Container.
        private static System.IServiceProvider? _diServices;
        private static int _handlingException;

        /// <summary>
        /// Microsoft.Extensions.DependencyInjection-Container. Disposed in OnExit.
        /// Background-Tasks (Warmup + BrainMirror) werden ueber StartBackgroundServices() gestartet.
        /// </summary>
        public static System.IServiceProvider DiServices
            => _diServices ?? throw new InvalidOperationException("DI services are not initialized.");

        /// <summary>Convenience-Helper: <c>App.DiServices.GetRequiredService&lt;T&gt;()</c>.</summary>
        public static T Resolve<T>() where T : notnull
            => ServiceProviderServiceExtensions.GetRequiredService<T>(DiServices);

        protected override async void OnStartup(StartupEventArgs e)
        {
            string? logPath = null;
            StartupSplashWindow? splash = null;
            try
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                splash = new StartupSplashWindow();
                splash.Show();
                splash.Activate();
                await Dispatcher.Yield(DispatcherPriority.Background);
                var splashStart = DateTime.UtcNow;

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // Settings
                var settings = AppSettings.Load();
                ThemeManager.ApplyTheme(Resources, settings.UiTheme);

                // Logging
                var logDir = Path.Combine(AppSettings.AppDataDir, "logs");
                Directory.CreateDirectory(logDir);
                logPath = Path.Combine(logDir, $"app-{DateTime.Now:yyyyMMdd}.log");
                var loggerFactory = LoggerFactory.Create(b =>
                {
                    b.AddProvider(new FileLoggerProvider(logPath));
                });
                var logger = loggerFactory.CreateLogger("App");

                var diagnostics = new DiagnosticsOptions
                {
                    EnableDiagnostics = settings.EnableDiagnostics,
                    ExplicitPdfToTextPath = settings.PdfToTextPath
                };

                // Phase 5.3 Sub-D: KnowledgeBase-Pfad-Resolver an die KnowledgeBase-Schicht
                // (Infrastructure) durchreichen — ohne dass Infrastructure UI-Klassen
                // wie KnowledgeRoot kennt.
                AuswertungPro.Next.Application.Ai.KnowledgeBase.KnowledgeBasePathProvider.SetResolver(
                    () => Ai.KnowledgeRoot.GetKnowledgeDbPath());

                // Phase 5.3: Generischer KnowledgeRoot-Provider fuer Stores in
                // Application/Infrastructure ohne UI-Dependency auf KnowledgeRoot.
                AuswertungPro.Next.Application.Ai.KnowledgeRootProvider.SetResolver(
                    () => Ai.KnowledgeRoot.GetRoot());

                // Phase 5.3: AppData-Pfad-Resolver (LocalAppData\SewerStudio).
                AuswertungPro.Next.Application.Ai.AppDataPathProvider.SetResolver(
                    () => AppSettings.AppDataDir);

                // Phase 5.3: KnowledgeMirror-Notifier (E:\Brain Sync) — UI haelt
                // KnowledgeMirrorService Singleton, Application/Infrastructure-Stores
                // koennen NotifyChanged() ohne UI-Reference rufen.
                AuswertungPro.Next.Application.Ai.KnowledgeMirrorNotifier.SetNotifier(
                    () => Services.KnowledgeMirrorService.Current?.NotifyChanged());

                // Phase 5.3: Sidecar-Auth-Token-Bridge (entkoppelt VisionPipelineClient
                // vom UI-PythonSidecarService — beide static).
                AuswertungPro.Next.Application.Ai.SidecarAuthTokenAccessor.SetResolvers(
                    tokenResolver: () => Ai.PythonSidecarService.CurrentAuthToken,
                    tokenFilePathResolver: () => Ai.PythonSidecarService.TokenFilePath);

                // Phase 5.3: OllamaConfig-Loader (UI -> Application).
                AuswertungPro.Next.Application.Ai.Ollama.OllamaConfigProvider.SetLoader(
                    () => Ai.AiPlatformConfig.Load().ToOllamaConfig());

                // Phase 5.3: AiRuntimeConfig-Loader (analog zu OllamaConfigProvider).
                AuswertungPro.Next.Application.Ai.AiRuntimeConfigProvider.SetLoader(
                    () => Ai.AiPlatformConfig.Load().ToRuntimeConfig());

                // Phase 5.1.B Etappe 4 Sub-E: Nur noch DI-Container — Legacy-ServiceProvider entfernt.
                var diCollection = new ServiceCollection();
                diCollection.AddSewerStudioInfrastructure(settings, diagnostics, logger, loggerFactory);
                diCollection.AddSewerStudioCoreServices();
                diCollection.AddSewerStudioAiServices();
                _diServices = diCollection.BuildServiceProvider();

                // Background-Tasks (Modell-Warmup + BrainMirror-Sync) starten.
                ServiceCollectionConfigurator.StartBackgroundServices(_diServices);

                // Sidecar (YOLO/Florence-2/SAM 2) im Hintergrund starten.
                if (settings.SidecarAutoStart != false)
                {
                    var sidecar = Resolve<PythonSidecarService>();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await sidecar.StartAsync();
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "[Sidecar] Auto-Start fehlgeschlagen — Qwen-only Fallback");
                        }
                    });
                }

                // Global exception handling (after services initialized, but before first window).
                // 2026-04-26: Direkt-Crash-Logger zusaetzlich zum normalen Logger.
                // Wenn die App durch native Exception oder IsTerminating=true killt,
                // hat der FileLoggerProvider keine Zeit zu flushen — direkt-sync-write
                // garantiert dass die Crash-Info auf der Platte landet.
                var crashLogPath = Path.Combine(AppSettings.AppDataDir, "logs", $"crash-{DateTime.Now:yyyyMMdd_HHmmss}.log");
                void LogCrashSync(string source, Exception ex, bool isTerminating)
                {
                    try
                    {
                        var line =
                            $"=== {DateTimeOffset.Now:O} ===" + Environment.NewLine +
                            $"Source: {source}" + Environment.NewLine +
                            $"IsTerminating: {isTerminating}" + Environment.NewLine +
                            $"Type: {ex.GetType().FullName}" + Environment.NewLine +
                            $"Message: {ex.Message}" + Environment.NewLine +
                            $"Stack:{Environment.NewLine}{ex}" + Environment.NewLine + Environment.NewLine;
                        File.AppendAllText(crashLogPath, line);
                    }
                    catch { /* niemals werfen aus dem Crash-Logger */ }
                }

                DispatcherUnhandledException += (_, exArgs) =>
                {
                    LogCrashSync("UI.DispatcherUnhandledException", exArgs.Exception, false);
                    exArgs.Handled = true;
                    HandleException(exArgs.Exception, "UI.DispatcherUnhandledException");
                };

                AppDomain.CurrentDomain.UnhandledException += (_, exArgs) =>
                {
                    if (exArgs.ExceptionObject is Exception ex)
                    {
                        LogCrashSync("AppDomain.UnhandledException", ex, exArgs.IsTerminating);
                        HandleException(ex, "AppDomain.UnhandledException");
                    }
                };

                TaskScheduler.UnobservedTaskException += (_, exArgs) =>
                {
                    LogCrashSync("TaskScheduler.UnobservedTaskException", exArgs.Exception, false);
                    exArgs.SetObserved();
                    HandleException(exArgs.Exception, "TaskScheduler.UnobservedTaskException");
                };

                logger.LogInformation("App startup complete. LogPath={LogPath}", logPath);

                // Einmal-Migration: bestehende KB-Samples ohne QualityGateLevel nachtraeglich bewerten
                try
                {
                    var backfilled = AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseManager.BackfillQualityGateLevels();
                    if (backfilled > 0)
                        logger.LogInformation("QualityGate-Backfill: {Count} Samples nachtraeglich bewertet", backfilled);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "QualityGate-Backfill fehlgeschlagen (nicht kritisch)");
                }

#if DEBUG
                // Optional self-test: only in Debug and explicitly enabled to avoid startup side effects.
                if (string.Equals(
                        Environment.GetEnvironmentVariable("SEWERSTUDIO_RUN_CATALOG_SELFTEST"),
                        "1",
                        StringComparison.Ordinal))
                {
                    var codeCatalog = Resolve<ICodeCatalogProvider>();
                    AuswertungPro.Next.UI.ViewModels.Protocol.CodeCatalogProviderTest.RunTest(codeCatalog);
                }
#endif
                // Call base last so anything that touches DI (App.Services) is ready.
                base.OnStartup(e);

                var mainWindow = new MainWindow
                {
                    Opacity = 0
                };
                MainWindow = mainWindow;
                mainWindow.Show();

                var minSplashDuration = TimeSpan.FromMilliseconds(10000);
                var elapsed = DateTime.UtcNow - splashStart;
                if (elapsed < minSplashDuration)
                    await splash.WaitAsync(minSplashDuration - elapsed);

                await Task.WhenAll(
                    AnimateOpacityAsync(mainWindow, to: 1, duration: TimeSpan.FromMilliseconds(500), EasingMode.EaseOut),
                    splash.FadeOutAndCloseAsync(TimeSpan.FromMilliseconds(500)));

                // WICHTIG: OnExplicitShutdown statt OnMainWindowClose.
                // MainWindow.Closing ruft Application.Current.Shutdown() explizit auf
                // (MainWindow.xaml.cs:46). OnMainWindowClose fuehrte dazu, dass bei
                // Owner-Chain-Edge-Cases (z.B. VsaCodeExplorerWindow Topmost innerhalb
                // Trainings-Modus) das Schliessen des PlayerWindow die App beendete,
                // weil WPF-Application.MainWindow in diesen Faellen versehentlich auf
                // das PlayerWindow zeigen kann.
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

            }
            catch (Exception ex)
            {
                try
                {
                    splash?.Close();
                }
                catch
                {
                    // ignore splash close errors during crash reporting
                }

                var fallbackLog = logPath;
                try
                {
                    if (string.IsNullOrWhiteSpace(fallbackLog))
                    {
                        var logDir = Path.Combine(AppSettings.AppDataDir, "logs");
                        Directory.CreateDirectory(logDir);
                        fallbackLog = Path.Combine(logDir, $"app-{DateTime.Now:yyyyMMdd}.log");
                    }

                    File.AppendAllText(fallbackLog, $"{DateTimeOffset.Now:O} [Fatal] Startup exception: {ex}\n");
                }
                catch
                {
                    // ignore file I/O errors during crash reporting
                }

                try
                {
                    MessageBox.Show(
                        $"SewerStudio konnte nicht gestartet werden.\n\n{ex.Message}\n\nDetails: {fallbackLog}",
                        "SewerStudio",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch
                {
                    // ignore message box errors during startup
                }

                Shutdown(-1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // DI-Container disposed alle Singleton-Services die IDisposable implementieren
                // (Sidecar, KbHttp via KnowledgeBaseModule.Services-record, etc.).
                (_diServices as IDisposable)?.Dispose();
            }
            catch
            {
                // best effort DI shutdown
            }

            try
            {
                AppSettings.FlushPendingSave();
            }
            catch
            {
                // best effort flush during shutdown
            }

            base.OnExit(e);
        }

        private static Task AnimateOpacityAsync(UIElement element, double to, TimeSpan duration, EasingMode easingMode)
        {
            if (duration <= TimeSpan.Zero)
            {
                element.Opacity = to;
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<object?>();
            var anim = new DoubleAnimation
            {
                To = to,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = easingMode }
            };

            anim.Completed += (_, _) => tcs.TrySetResult(null);
            element.BeginAnimation(UIElement.OpacityProperty, anim);
            return tcs.Task;
        }

        private void HandleException(Exception ex, string context)
        {
            if (Interlocked.Exchange(ref _handlingException, 1) == 1)
                return;

            try
            {
                if (_diServices is null)
                    return;

                var gen = Resolve<ErrorCodeGenerator>();
                var code = gen.GenerateForException("APP", ex, context);

                Resolve<Microsoft.Extensions.Logging.ILogger>()
                    .LogError(ex, "Unhandled exception {Code} {Context}", code, context);

                try
                {
                    if (Resolve<DiagnosticsOptions>().EnableDiagnostics)
                    {
                        MessageBox.Show(
                            $"Es ist ein Fehler aufgetreten.\n\nCode: {code}\n{ex.Message}\n\nDetails: siehe Log-Datei",
                            "SewerStudio",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    else
                    {
                        MessageBox.Show(
                            "Es ist ein Fehler aufgetreten.",
                            "SewerStudio",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                catch
                {
                    // Avoid recursive crash if MessageBox throws (e.g., during shutdown).
                }
            }
            finally
            {
                Interlocked.Exchange(ref _handlingException, 0);
            }
        }
    }


}
