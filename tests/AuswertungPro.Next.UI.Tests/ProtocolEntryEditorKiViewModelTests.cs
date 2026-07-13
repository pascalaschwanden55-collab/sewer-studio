using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Protocol;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProtocolEntryEditorKiViewModelTests
{
    [Fact]
    public async Task GetKiSuggestionAsync_uebergibt_erlaubte_codes_und_veroeffentlicht_vorschlag()
    {
        var service = new CapturingAiService
        {
            Result = new AiSuggestion("BAB", 0.82, "Riss sichtbar", ["test"])
        };
        var editor = new ProtocolEntryEditorViewModel(new Catalog("BAB", "BBA"));
        var viewModel = new ProtocolEntryEditorKiViewModel(editor, service);

        await viewModel.GetKiSuggestionAsync();

        var input = Assert.IsType<AiInput>(service.LastInput);
        Assert.Equal(["BAB", "BBA"], input.AllowedCodes);
        Assert.Equal(string.Empty, input.ProjectFolderAbs);
        Assert.Null(input.HaltungId);
        Assert.Null(input.Meter);
        Assert.Equal("BAB", viewModel.KiSuggestion?.SuggestedCode);
        Assert.Contains("BAB", viewModel.KiStatus, StringComparison.Ordinal);
        Assert.False(viewModel.IsKiLoading);
        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public async Task SuggestAsync_baut_vollstaendigen_eingabekontext_und_uebernimmt_bekannten_code()
    {
        var service = new CapturingAiService
        {
            Result = new AiSuggestion("bab", 0.91, "Riss im Rohr", ["pruefen"])
        };
        var entry = new ProtocolEntryVM(new ProtocolEntry());
        var editor = new ProtocolEntryEditorViewModel(new Catalog("BAB", "BBA"));
        var viewModel = new ProtocolEntryEditorKiViewModel(editor, service, entry);

        var result = await viewModel.SuggestAsync(new ProtocolEntryKiSuggestionRequest(
            ProjectFolderAbs: @"C:\Projekt",
            HaltungId: "1.001-1.002",
            MeterStartText: "12,5",
            MeterEndText: "14.0",
            ZeitText: "01:02",
            ExistingCode: " BBA ",
            ExistingText: " Riss sichtbar ",
            VideoPathAbs: @"C:\Projekt\video.mpg",
            ImagePathsAbs: [@"C:\Projekt\bild.jpg"]));

        var input = Assert.IsType<AiInput>(service.LastInput);
        Assert.Equal(@"C:\Projekt", input.ProjectFolderAbs);
        Assert.Equal("1.001-1.002", input.HaltungId);
        Assert.Equal(12.5, input.Meter);
        Assert.Equal("BBA", input.ExistingCode);
        Assert.Equal("Riss sichtbar", input.ExistingText);
        Assert.Equal(TimeSpan.FromSeconds(62), input.Zeit);
        Assert.Equal(@"C:\Projekt\video.mpg", input.VideoPathAbs);
        Assert.Equal([@"C:\Projekt\bild.jpg"], input.ImagePathsAbs);
        Assert.Equal("BAB", result.AcceptedCode);
        Assert.Contains("übernommen", result.StatusText, StringComparison.Ordinal);
        Assert.Equal("KI-Hinweis: Riss im Rohr", result.ValidationText);
        Assert.Equal("bab", entry.Model.Ai?.SuggestedCode);
        Assert.False(viewModel.IsKiLoading);
    }

    [Fact]
    public async Task SuggestAsync_stoppt_vor_serviceaufruf_wenn_meterstart_ungueltig_ist()
    {
        var service = new CapturingAiService();
        var editor = new ProtocolEntryEditorViewModel(new Catalog("BAB"));
        var viewModel = new ProtocolEntryEditorKiViewModel(editor, service);

        var result = await viewModel.SuggestAsync(Request(meterStart: "unlesbar"));

        Assert.Equal("MeterStart ist ungültig.", result.ValidationText);
        Assert.False(result.RequestStarted);
        Assert.Equal(0, service.CallCount);
        Assert.False(viewModel.IsKiLoading);
    }

    [Fact]
    public async Task SuggestAsync_zeigt_keine_rohen_fehlerdetails_und_protokolliert_sie()
    {
        var warnings = new List<string>();
        var service = new CapturingAiService
        {
            Handler = (_, _) => throw new InvalidOperationException("internes Geheimdetail")
        };
        var editor = new ProtocolEntryEditorViewModel(new Catalog("BAB"));
        var viewModel = new ProtocolEntryEditorKiViewModel(editor, service, warningLogger: warnings.Add);

        var result = await viewModel.SuggestAsync(Request());

        Assert.DoesNotContain("Geheimdetail", result.ValidationText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tageslog", result.ValidationText, StringComparison.Ordinal);
        Assert.Contains(warnings, warning => warning.Contains("internes Geheimdetail", StringComparison.Ordinal));
        Assert.True(result.RequestStarted);
        Assert.False(viewModel.IsKiLoading);
    }

    [Fact]
    public async Task SuggestAsync_reicht_abbruch_weiter_und_meldet_ihn_verstaendlich()
    {
        var service = new CapturingAiService
        {
            Handler = async (_, ct) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return null;
            }
        };
        var editor = new ProtocolEntryEditorViewModel(new Catalog("BAB"));
        var viewModel = new ProtocolEntryEditorKiViewModel(editor, service);
        using var cts = new CancellationTokenSource();

        var task = viewModel.SuggestAsync(Request(), cts.Token);
        await cts.CancelAsync();
        var result = await task;

        Assert.Equal("KI-Vorschlag abgebrochen.", result.StatusText);
        Assert.True(result.RequestStarted);
        Assert.False(viewModel.IsKiLoading);
    }

    private static ProtocolEntryKiSuggestionRequest Request(string meterStart = "12.5")
        => new(
            ProjectFolderAbs: @"C:\Projekt",
            HaltungId: "1.001-1.002",
            MeterStartText: meterStart,
            MeterEndText: string.Empty,
            ZeitText: string.Empty,
            ExistingCode: "BAB",
            ExistingText: "Riss",
            VideoPathAbs: null,
            ImagePathsAbs: null);

    private sealed class CapturingAiService : IProtocolAiService
    {
        public AiSuggestion? Result { get; init; }
        public Func<AiInput, CancellationToken, Task<AiSuggestion?>>? Handler { get; init; }
        public AiInput? LastInput { get; private set; }
        public int CallCount { get; private set; }

        public Task<AiSuggestion?> SuggestAsync(AiInput input, CancellationToken ct = default)
        {
            CallCount++;
            LastInput = input;
            return Handler?.Invoke(input, ct) ?? Task.FromResult(Result);
        }
    }

    private sealed class Catalog(params string[] codes) : ICodeCatalogProvider
    {
        private readonly IReadOnlyList<CodeDefinition> _definitions = codes
            .Select(code => new CodeDefinition { Code = code, IsSelectable = true })
            .ToList();

        public IReadOnlyList<CodeDefinition> GetAll() => _definitions;

        public bool TryGet(string code, out CodeDefinition def)
        {
            var found = _definitions.FirstOrDefault(
                item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));
            def = found ?? new CodeDefinition();
            return found is not null;
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new NotSupportedException();

        public IReadOnlyList<string> AllowedCodes()
            => _definitions.Select(item => item.Code).ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => [];
    }
}
