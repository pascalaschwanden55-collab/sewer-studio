
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
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Application.Vsa;
using AuswertungPro.Next.Infrastructure.Export;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.Infrastructure.Ai.Startup;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.UI.LiveControl;
using AuswertungPro.Next.UI.QgisBridge;
using AuswertungPro.Next.UI.Views.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI
{
    public partial class App : System.Windows.Application
    {

        private static ServiceProvider? _services;
        private static int _handlingException;
        private static bool _typographyDefaultsApplied;
        private static bool _windowIconDefaultsRegistered;
        private static System.Windows.Media.ImageSource? _appWindowIcon;
        private LiveControlServer? _liveControlServer;
        private QgisBridgeServer? _qgisBridgeServer;
        private SingleInstanceGuard? _singleInstanceGuard;

        // Tageslogs aelter als dieser Wert werden beim Start geloescht (Aufbewahrung).
        private const int LogRetentionDays = 60;

        public static IServiceProvider Services
            => _services ?? throw new InvalidOperationException("Services are not initialized.");

        protected override async void OnStartup(StartupEventArgs e)
        {
            string? logPath = null;
            StartupSplashWindow? splash = null;
            try
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                _singleInstanceGuard = new SingleInstanceGuard();
                if (!_singleInstanceGuard.TryAcquire())
                {
                    DialogHost.Current.Info(
                        "SewerStudio laeuft bereits. Bitte verwende das geoeffnete Programmfenster.",
                        "SewerStudio bereits gestartet");
                    Shutdown(0);
                    return;
                }

                ApplyTypographyDefaults();
                RegisterWindowIconDefaults();

                splash = new StartupSplashWindow();
                splash.Show();
                splash.Activate();
                await Dispatcher.Yield(DispatcherPriority.Background);

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // Settings
                var settings = AppSettings.Load();
                WindowStateManager.Configure(settings);
                ViewCustomizationStore.Configure(settings);
                if (settings.AiStartOnProgramStart && AiStartupService.ApplyRuntimeDefaults(settings))
                    settings.SaveImmediate();
                ThemeManager.ApplyTheme(Resources, settings.UiTheme);

                // Logging
                var logDir = Path.Combine(AppSettings.AppDataDir, "logs");
                Directory.CreateDirectory(logDir);
                // Aufbewahrung: alte Tageslogs entsorgen, damit der Log-Ordner nicht unbegrenzt waechst.
                FileLoggerProvider.CleanupOldLogs(logDir, LogRetentionDays);
                logPath = Path.Combine(logDir, $"app-{DateTime.Now:yyyyMMdd}.log");
                var loggerFactory = LoggerFactory.Create(b =>
                {
                    b.AddProvider(new FileLoggerProvider(logPath));
                });
                var logger = loggerFactory.CreateLogger("App");
                var bestEffortLogger = loggerFactory.CreateLogger("BestEffort");
                BestEffort.ConfigureDefaultErrorSink(
                    message => bestEffortLogger.LogWarning("{Message}", message));
                Helpers.TaskExtensions.ConfigureLogging(loggerFactory.CreateLogger("BackgroundTasks"));
                // QGIS-Bridge-Verarbeitung: gemeinsam fuer Live-Control-Server (gleicher Port)
                // und den eigenstaendigen Bridge-Host weiter unten.
                var qgisBridgeProcessor = new QgisBridgeRequestProcessor(
                    this,
                    settings,
                    loggerFactory.CreateLogger<QgisBridgeRequestProcessor>());
                _liveControlServer = LiveControlServer.TryStartFromEnvironment(
                    this,
                    loggerFactory.CreateLogger<LiveControlServer>(),
                    qgisBridgeProcessor);

                var diagnostics = new DiagnosticsOptions
                {
                    EnableDiagnostics = settings.EnableDiagnostics,
                    ExplicitPdfToTextPath = settings.PdfToTextPath
                };

                _services = new ServiceProvider(settings, diagnostics, logger, loggerFactory);
                DialogHost.Configure(_services.Dialogs);
                var splashReadyTask = splash.SignalReadyAsync();

                // Global exception handling (after services initialized, but before first window).
                DispatcherUnhandledException += (_, exArgs) =>
                {
                    exArgs.Handled = true;
                    HandleException(exArgs.Exception, "UI.DispatcherUnhandledException");
                };

                AppDomain.CurrentDomain.UnhandledException += (_, exArgs) =>
                {
                    if (exArgs.ExceptionObject is Exception ex)
                        HandleException(ex, "AppDomain.UnhandledException");
                };

                TaskScheduler.UnobservedTaskException += (_, exArgs) =>
                {
                    exArgs.SetObserved();
                    HandleException(exArgs.Exception, "TaskScheduler.UnobservedTaskException");
                };

                logger.LogInformation("App startup complete. LogPath={LogPath}", logPath);

#if DEBUG
                // Optional self-test: only in Debug and explicitly enabled to avoid startup side effects.
                if (string.Equals(
                        Environment.GetEnvironmentVariable("SEWERSTUDIO_RUN_CATALOG_SELFTEST"),
                        "1",
                        StringComparison.Ordinal))
                {
                    var codeCatalog = ((ServiceProvider)_services!).CodeCatalog;
                    AuswertungPro.Next.UI.ViewModels.Protocol.CodeCatalogProviderTest.RunTest(codeCatalog);
                }
#endif
                // Call base last so startup infrastructure is configured before first window creation.
                base.OnStartup(e);

                var mainWindow = new MainWindow
                {
                    Opacity = 0
                };
                MainWindow = mainWindow;
                mainWindow.Show();
                // Haelt Live-Control bereits Port 8765, liefert ER die /qgis-Endpunkte mit aus —
                // dann braucht es keinen zweiten Listener (Port-Konflikt).
                if (_liveControlServer is null)
                {
                    _qgisBridgeServer = QgisBridgeServer.TryStart(
                        qgisBridgeProcessor,
                        loggerFactory.CreateLogger<QgisBridgeServer>());
                }
                if (settings.AiStartOnProgramStart)
                    _ = StartAiOnStartupAsync(settings, logger, mainWindow);

                // Splash erst ausblenden, wenn der Fortschrittsbalken durchgelaufen ist –
                // sonst wird die Startanimation bei schnellem Start abgeschnitten.
                // Sicherheitskappe: max. 15 s, falls die Animation nicht meldet.
                await Task.WhenAny(
                    splashReadyTask,
                    splash.WaitForProgressAsync(),
                    Task.Delay(TimeSpan.FromMilliseconds(15000)));

                await Task.WhenAll(
                    AnimateOpacityAsync(mainWindow, to: 1, duration: TimeSpan.FromMilliseconds(500), EasingMode.EaseOut),
                    splash.FadeOutAndCloseAsync(TimeSpan.FromMilliseconds(500)));
                await mainWindow.PlayStartupEntranceAsync();

                ShutdownMode = ShutdownMode.OnMainWindowClose;

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
                    DialogHost.Current.Error(
                        $"SewerStudio konnte nicht gestartet werden.\n\n{UserError.Describe(ex)}\n\nDetails: {fallbackLog}",
                        "SewerStudio");
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
            AiStartedProcessLifetime.StopAllStartedProcesses();
            try
            {
                _qgisBridgeServer?.Dispose();
                _liveControlServer?.Dispose();
                AppSettings.FlushPendingSave();
                _singleInstanceGuard?.Dispose();
                _singleInstanceGuard = null;
            }
            catch
            {
                // best effort flush during shutdown
            }

            BestEffort.ConfigureDefaultErrorSink(null);
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

        private static async Task StartAiOnStartupAsync(AppSettings settings, ILogger logger, Window mainWindow)
        {
            if (mainWindow.DataContext is AuswertungPro.Next.UI.ViewModels.ShellViewModel shell)
                shell.SetStatus("Starte KI...");

            try
            {
                var result = await AiStartupService.StartAsync(settings);
                settings.SaveImmediate();
                logger.LogInformation("KI-Autostart abgeschlossen: {Summary}", result.Summary);

                if (mainWindow.DataContext is AuswertungPro.Next.UI.ViewModels.ShellViewModel shellAfter)
                    shellAfter.SetStatus(result.HasWarnings ? "KI-Start mit Warnung" : "KI gestartet");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "KI-Autostart fehlgeschlagen.");
                if (mainWindow.DataContext is AuswertungPro.Next.UI.ViewModels.ShellViewModel shellAfter)
                    shellAfter.SetStatus($"KI-Autostart fehlgeschlagen: {ex.Message}");
            }
        }

        private static void ApplyTypographyDefaults()
        {
            if (_typographyDefaultsApplied)
            {
                return;
            }

            _typographyDefaultsApplied = true;
            var fontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Display, Segoe UI, Aptos, Arial");

            TryOverrideMetadata(
                System.Windows.Documents.TextElement.FontFamilyProperty,
                typeof(System.Windows.Documents.TextElement),
                fontFamily);
            TryOverrideMetadata(
                System.Windows.Documents.TextElement.FontSizeProperty,
                typeof(System.Windows.Documents.TextElement),
                14.0);
            TryOverrideMetadata(
                System.Windows.Controls.Control.FontFamilyProperty,
                typeof(System.Windows.Controls.Control),
                fontFamily);
            TryOverrideMetadata(
                System.Windows.Controls.Control.FontSizeProperty,
                typeof(System.Windows.Controls.Control),
                14.0);
        }

        private static void TryOverrideMetadata(DependencyProperty property, Type targetType, object value)
        {
            try
            {
                property.OverrideMetadata(targetType, new FrameworkPropertyMetadata(value));
            }
            catch (InvalidOperationException)
            {
                // Test- und Design-Hosts koennen Metadata bereits versiegelt haben.
            }
            catch (ArgumentException)
            {
                // Einzelne Owner-Typen sind je nach WPF-Host schon registriert.
            }
        }

        private static void RegisterWindowIconDefaults()
        {
            if (_windowIconDefaultsRegistered)
            {
                return;
            }

            _windowIconDefaultsRegistered = true;
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(ApplyDefaultWindowIcon));
        }

        private static void ApplyDefaultWindowIcon(object sender, RoutedEventArgs e)
        {
            if (sender is not Window window)
            {
                return;
            }

            if (window.Icon is null)
            {
                var icon = LoadDefaultWindowIcon();
                if (icon is not null)
                {
                    window.Icon = icon;
                }
            }

            WindowBackdropHelper.Apply(
                window,
                ThemeManager.CurrentTheme,
                Fluent.GetBackdrop(window) == FluentBackdrop.Mica);
        }

        private static System.Windows.Media.ImageSource? LoadDefaultWindowIcon()
        {
            if (_appWindowIcon is not null)
            {
                return _appWindowIcon;
            }

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            if (!File.Exists(iconPath))
            {
                return null;
            }

            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                _appWindowIcon = bitmap;
                return _appWindowIcon;
            }
            catch (IOException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        private void HandleException(Exception ex, string context)
        {
            if (Interlocked.Exchange(ref _handlingException, 1) == 1)
                return;

            try
            {
                if (_services is not ServiceProvider realSp)
                    return;

                var gen = realSp.ErrorCodes;
                var code = gen.GenerateForException("APP", ex, context);

                realSp.Logger.LogError(ex, "Unhandled exception {Code} {Context}", code, context);

                try
                {
                    if (realSp.Diagnostics.EnableDiagnostics)
                    {
                        realSp.Dialogs.Error(
                            $"Es ist ein Fehler aufgetreten.\n\nCode: {code}\n{UserError.Describe(ex)}\n\nDetails: siehe Log-Datei",
                            "SewerStudio");
                    }
                    else
                    {
                        realSp.Dialogs.Error(
                            "Es ist ein Fehler aufgetreten.",
                            "SewerStudio");
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
