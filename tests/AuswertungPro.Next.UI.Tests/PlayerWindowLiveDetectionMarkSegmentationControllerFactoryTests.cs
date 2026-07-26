using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowLiveDetectionMarkSegmentationControllerFactoryTests
{
    [Fact]
    public void Create_reads_runtime_calibration_and_content_rect_late_and_uses_same_canvas()
    {
        StaTestRunner.Run(() =>
        {
            var aiController = new CodingAiController();
            var overlayHost = new MutableOverlayToolHost();
            var overlayCanvas = new Canvas();
            var contentRect = Rect.Empty;
            var contentRectReads = 0;
            var controller = PlayerWindowLiveDetectionMarkSegmentationControllerFactory.Create(
                new PlayerWindowLiveDetectionMarkSegmentationDependencies(
                    AiController: aiController,
                    OverlayToolHost: overlayHost,
                    OverlayCanvas: overlayCanvas,
                    ResolveContentRect: () =>
                    {
                        contentRectReads++;
                        return contentRect;
                    }));
            var overlay = new OverlayGeometry
            {
                Points = [new NormalizedPoint(0.2, 0.2), new NormalizedPoint(0.8, 0.8)]
            };
            var frameBytes = FakePng(100, 100);

            var skipped = controller.TrySegmentAsync(overlay, frameBytes).GetAwaiter().GetResult();

            Assert.Null(skipped);
            Assert.Equal(0, overlayHost.CalibrationReads);
            Assert.Equal(0, contentRectReads);

            CancellationToken? observedToken = null;
            SamRequest? observedRequest = null;
            var segmentationService = new MarkBoxSegmentationService((request, token) =>
            {
                observedRequest = request;
                observedToken = token;
                return Task.FromResult(new SamResponse(
                    [Mask()],
                    ImageWidth: 100,
                    ImageHeight: 100,
                    InferenceTimeMs: 1));
            });
            overlayHost.CalibrationValue = new PipeCalibration
            {
                NominalDiameterMm = 300,
                NormalizedDiameter = 0.5,
                Source = CalibrationSource.Manual
            };
            aiController.ApplyRuntime(Runtime(segmentationService));

            var segmented = controller.TrySegmentAsync(overlay, frameBytes).GetAwaiter().GetResult();

            Assert.NotNull(segmented);
            Assert.Equal(1, overlayHost.CalibrationReads);
            Assert.NotNull(observedRequest);
            Assert.Equal(300, observedRequest!.PipeDiameterMm);
            Assert.Equal(CancellationToken.None, observedToken);
            contentRect = new Rect(10, 20, 640, 480);

            controller.ShowMask(segmented!, overlay);

            Assert.Equal(1, contentRectReads);
            Assert.NotEmpty(overlayCanvas.Children);
            Assert.DoesNotContain(
                overlayCanvas.Children.OfType<FrameworkElement>(),
                element => Equals(element.Tag, OverlayTags.BendMarker));

            controller.ShowMask(
                segmented with { IsBend = true, VanishX = 0.5, VanishY = 0.5 },
                overlay);

            Assert.Equal(2, contentRectReads);
            Assert.Contains(
                overlayCanvas.Children.OfType<FrameworkElement>(),
                element => Equals(element.Tag, OverlayTags.BendMarker));
        });
    }

    [Fact]
    public void Create_rejects_missing_dependencies()
        => Assert.Throws<ArgumentNullException>(() =>
            PlayerWindowLiveDetectionMarkSegmentationControllerFactory.Create(null!));

    private static CodingAiRuntime Runtime(MarkBoxSegmentationService segmentationService)
        => new(
            new AiRuntimeSettings(
                Enabled: true,
                new Uri("http://127.0.0.1:11434"),
                VisionModel: "vision-test",
                TextModel: "text-test",
                EmbedModel: null,
                FfmpegPath: null,
                OllamaRequestTimeout: TimeSpan.FromSeconds(1),
                OllamaKeepAlive: "1m",
                OllamaNumCtx: 1024),
            new PipelineConfig(
                MultiModelEnabled: true,
                new Uri("http://127.0.0.1:8100"),
                SidecarToken: null,
                PipelineMode.Auto,
                YoloConfidence: 0.35,
                YoloClassConfidence: [],
                DinoBoxThreshold: 0.3,
                DinoTextThreshold: 0.25,
                SidecarTimeoutSec: 1,
                PipeDiameterMmOverride: 300),
            ModelName: "vision-test",
            LiveDetection: null,
            EnhancedVision: null,
            QualityGate: null,
            ProtocolVerifier: null,
            VisionClient: null,
            MultiModel: null,
            BoxSegmentation: segmentationService,
            MultiModelError: null);

    private static byte[] FakePng(int width, int height)
    {
        var bytes = new byte[24];
        bytes[0] = 0x89;
        bytes[1] = 0x50;
        bytes[2] = 0x4E;
        bytes[3] = 0x47;
        bytes[16] = (byte)(width >> 24);
        bytes[17] = (byte)(width >> 16);
        bytes[18] = (byte)(width >> 8);
        bytes[19] = (byte)width;
        bytes[20] = (byte)(height >> 24);
        bytes[21] = (byte)(height >> 16);
        bytes[22] = (byte)(height >> 8);
        bytes[23] = (byte)height;
        return bytes;
    }

    private static SamMaskResult Mask()
        => new(
            Label: "manual",
            Confidence: 0.9,
            Bbox: [10, 10, 40, 40],
            MaskRle: "1,1,9999",
            MaskAreaPixels: 900,
            ImageAreaPixels: 10_000,
            HeightPixels: 30,
            WidthPixels: 30,
            CentroidX: 25,
            CentroidY: 25);

    private sealed class MutableOverlayToolHost : ICodingOverlayToolHost
    {
        public PipeCalibration? CalibrationValue { get; set; }
        public int CalibrationReads { get; private set; }
        public bool HasOverlayService => true;
        public OverlayToolType ActiveTool => OverlayToolType.None;
        public LevelMode ActiveLevelMode => LevelMode.Deposit;
        public bool PipeBendSnapEnabled => false;
        public bool IsDrawing => false;
        public bool IsMultiPointTool => false;
        public int DrawPointCount => 0;
        public PipeCalibration? Calibration
        {
            get
            {
                CalibrationReads++;
                return CalibrationValue;
            }
        }
        public int? NominalDiameterMm => CalibrationValue?.NominalDiameterMm;
        public bool IsCalibrated => CalibrationValue?.IsCalibrated == true;
        public bool SetActiveTool(OverlayToolType tool) => false;
        public bool SetActiveLevelMode(LevelMode mode) => false;
        public bool SetCalibration(PipeCalibration calibration) => false;
        public bool CancelDraw() => false;
    }
}
