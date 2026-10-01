using System.Net;
using System.Text;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class SingleFrameLocalizedDetectionTests
{
    [Fact]
    public async Task Qualifizierte_Yolo_Box_erreicht_Sam_auch_ohne_Dino_Treffer()
    {
        using var route = new DetectorRoute();
        var result = await Analyze(route);
        Assert.True(result.HasDetections);
        Assert.True(result.HasMasks);
        Assert.Empty(result.DinoDetections);
        Assert.Contains("BAI_dichtung", route.SamRequest);
        Assert.False(result.Degraded);
    }

    [Fact]
    public async Task Unqualifizierter_Detektor_liefert_keine_Box_an_Sam()
    {
        using var route = new DetectorRoute { Qualified = false };
        var result = await Analyze(route);
        Assert.Null(route.SamRequest);
        Assert.False(result.HasDetections);
        Assert.True(result.Degraded);
        Assert.DoesNotContain("/detect/yolo", route.Paths);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    public async Task Fremder_oder_fehlender_Hash_darf_auch_keinen_Negativfilter_liefern(string hash)
    {
        using var route = new DetectorRoute { ResponseHash = hash, Relevant = false };
        var result = await Analyze(route);
        Assert.True(result.Degraded);
        Assert.Null(result.YoloMaxConfidence);
        Assert.Contains("/detect/dino", route.Paths);
        Assert.Null(route.SamRequest);
    }

    private static async Task<SingleFrameResult> Analyze(DetectorRoute route)
    {
        using var client = new VisionPipelineClient(new Uri("http://localhost:8100"), new HttpClient(route));
        return await new SingleFrameMultiModelService(client).AnalyzeFrameAsync(
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jWZkAAAAASUVORK5CYII="),
            300, currentMeterM: 10, reachLengthM: 50);
    }

    private sealed class DetectorRoute : HttpMessageHandler
    {
        public bool Qualified { get; init; } = true;
        public string? ResponseHash { get; init; }
        public bool Relevant { get; init; } = true;
        public List<string> Paths { get; } = [];
        public string? SamRequest { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            var hash = new string('a', 64);
            var json = path switch
            {
                "/health" => $$$$"""{"status":"ok","version":"test","gpu":null,"detector_qualification":{"qualified":{{{{Qualified.ToString().ToLowerInvariant()}}}},"reason":null,"artifact":{"sha256":"{{{{hash}}}}"}}}""",
                "/classify/yolo" => """{"predictions":[],"inference_time_ms":0,"classifier_loaded":false}""",
                "/detect/yolo" => $$"""{"is_relevant":{{Relevant.ToString().ToLowerInvariant()}},"detections":[{"x1":0.7,"y1":0.4,"x2":0.9,"y2":0.6,"class_name":"BAI_dichtung","confidence":0.9}],"frame_class":"defect","inference_time_ms":1,"detector_qualified":true,"detector_artifact_sha256":"{{ResponseHash ?? hash}}"}""",
                "/detect/dino" => """{"detections":[],"inference_time_ms":1}""",
                "/segment/sam" => """{"masks":[{"label":"BAI_dichtung","confidence":0.9,"bbox":[0.7,0.4,0.9,0.6],"mask_rle":"0,1","mask_area_pixels":1,"image_area_pixels":1,"height_pixels":1,"width_pixels":1,"centroid_x":0.8,"centroid_y":0.5}],"image_width":1,"image_height":1,"inference_time_ms":1}""",
                _ => throw new InvalidOperationException(path)
            };
            if (path == "/segment/sam") SamRequest = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
