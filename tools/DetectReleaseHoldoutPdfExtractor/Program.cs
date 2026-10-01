using System.Text;

namespace DetectReleaseHoldoutPdfExtractor;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length == 1 && string.Equals(args[0], "--self-test", StringComparison.Ordinal))
            return GuardSelfTest.Run();

        if (args.Length != 1)
        {
            Console.Error.WriteLine(
                "Aufruf: DetectReleaseHoldoutPdfExtractor <auftrag.json>\n" +
                "Oder:   DetectReleaseHoldoutPdfExtractor --self-test");
            return 1;
        }

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            return await new ExtractionRunner().RunAsync(args[0], cancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Abgebrochen. Es wurde kein fertiger Prüfbeleg veröffentlicht.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fehler: {ex.Message}");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }
}
