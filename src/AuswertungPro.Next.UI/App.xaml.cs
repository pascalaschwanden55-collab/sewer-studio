
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
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI
{
    public partial class App : System.Windows.Application
    {

        private static ServiceProvider? _services;
        private static int _handlingException;

        public static IServiceProvider Services
            => _services ?? throw new InvalidOperationException("Services are not initialized.");

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

                _services = new ServiceProvider(settings, diagnostics, logger, loggerFactory);

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
                // Call base last so anything that touches DI (App.Services) is ready.
                base.OnStartup(e);

                var mainWindow = new MainWindow
                {
                    Opacity = 0
                };
                MainWindow = mainWindow;
                mainWindow.Show();

                var minSplashDuration = TimeSpan.FromMilliseconds(5000);
                var elapsed = DateTime.UtcNow - splashStart;
                if (elapsed < minSplashDuration)
                    await splash.WaitAsync(minSplashDuration - elapsed);

                await Task.WhenAll(
                    AnimateOpacityAsync(mainWindow, to: 1, duration: TimeSpan.FromMilliseconds(500), EasingMode.EaseOut),
                    splash.FadeOutAndCloseAsync(TimeSpan.FromMilliseconds(500)));

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
                if (_services is not ServiceProvider realSp)
                    return;

                var gen = realSp.ErrorCodes;
                var code = gen.GenerateForException("APP", ex, context);

                realSp.Logger.LogError(ex, "Unhandled exception {Code} {Context}", code, context);

                try
                {
                    if (realSp.Diagnostics.EnableDiagnostics)
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
