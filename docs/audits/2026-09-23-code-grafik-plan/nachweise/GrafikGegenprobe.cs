using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Ai.Pipeline;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var results = new List<object>();
        var geometry = new OverlayGeometry { ToolType = OverlayToolType.Rectangle,
            Points = [new(.2,.2), new(.4,.2), new(.4,.4), new(.2,.4)] };
        var ev = new CodingEvent { Overlay=geometry, AiContext=new CodingEventAiContext { Confidence=.9 } };
        ev.Entry.Code="BAB";
        foreach(var aspect in new[]{4d/3,16d/9})
        {
            var canvas=new Canvas {Width=1600,Height=900};
            var content=CodingOverlayViewportMapper.GetContentRect(1600,900,aspect);
            int mappingCalls=0;
            CodingAiOverlayRenderer.Render(canvas,[ev],1600,900,new(.5,.5),p=>{mappingCalls++;return CodingOverlayViewportMapper.NormToPixel(p,content);});
            var rect=canvas.Children.OfType<System.Windows.Shapes.Rectangle>().Single();
            var expected=CodingOverlayViewportMapper.NormToPixel(new(.2,.2),content);
            results.Add(new { name="KI-Rechteck mit Video-Seitenverhaeltnis", aspect, actualLeft=Canvas.GetLeft(rect), expectedLeft=expected.X,
                actualWidth=rect.Width, expectedWidth=.2*content.Width, mappingCalls });
        }
        foreach(var size in new[]{20d,1600d})
        {
            string? failure=null;
            try { CodingAiOverlayRenderer.Render(new Canvas(),[ev],size,size,new(.5,.5),p=>new Point(p.X*size,p.Y*size)); }
            catch(Exception e) { failure=e.GetType().Name+": "+e.Message; }
            results.Add(new { name="KI-Rechteck auf kleinem Canvas", width=size, failure });
        }
        foreach(var x in new[]{1,2})
        {
            var mask=new bool[540,960];
            for(int y=100;y<440;y++) mask[y,x]=true;
            var reduced=SamMaskRenderer.ExtractContourGeometry(mask,960,540,960,540);
            var original=SamMaskRenderer.ExtractContourGeometry(mask,960,540,960,540,960);
            results.Add(new{name="Duenne SAM-Kontur", sourceX=x, maskPixels=340, defaultContourEmpty=reduced.Bounds.IsEmpty,fullResolutionEmpty=original.Bounds.IsEmpty});
        }
        var fixture=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../cache-fixture"));
        Directory.CreateDirectory(fixture);
        var source=Path.Combine(fixture,"source.txt"); var cachePath=Path.Combine(fixture,"cache.json");
        // Unique cache avoids overwriting previous evidence; synthetic source only.
        cachePath=Path.Combine(fixture,$"cache-{Guid.NewGuid():N}.json");
        File.WriteAllText(source,"alte Geometrie"); var stamp=File.GetLastWriteTimeUtc(source);
        var cache=new TextCache(cachePath); var first=cache.Load(source).Single();
        File.WriteAllText(source,"neue Geometrie"); File.SetLastWriteTimeUtc(source,stamp);
        var second=cache.Load(source).Single();
        results.Add(new{name="XTF-Cache bei geaendertem Inhalt und gleicher Zeit", first, second, actualSource=File.ReadAllText(source),cache.Extractions});
        var appResources=new ResourceDictionary { Source=new Uri("pack://application:,,,/SewerStudio;component/Theme/Theme.xaml") };
        app.Resources.MergedDictionaries.Add(appResources);
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("pack://application:,,,/SewerStudio;component/Theme/Controls.xaml") });
        var bg=((SolidColorBrush)app.FindResource("CardGlassBrush")).Color;
        var fg=((SolidColorBrush)app.FindResource("SuccessBrush")).Color;
        var replacement=((SolidColorBrush)app.FindResource("SuccessTextBrush")).Color;
        results.Add(new{name="Kontrast gruener Legendentext im dunklen Player", background=bg.ToString(),foreground=fg.ToString(),contrast=Contrast(fg,bg), suggestedContrast=Contrast(replacement,bg), fontSize=app.FindResource("TextXS")});
        var assembly=typeof(CodingAiOverlayRenderer).Assembly;
        var galleryType=assembly.GetType("AuswertungPro.Next.UI.Controls.PhotoGalleryPanel")!;
        var gallery=(UserControl)Activator.CreateInstance(galleryType)!;
        var list=(ListBox)gallery.FindName("FotoListe");
        list.ItemsSource=Enumerable.Range(1,1000).Select(n=>new {Pfad="",Beschriftung=$"Prueffoto {n}"}).ToArray();
        var host=new Window { Content=gallery,Width=760,Height=520,Left=-10000,Top=-10000,ShowActivated=false,ShowInTaskbar=false };
        host.Show(); host.UpdateLayout();
        var created=Enumerable.Range(0,1000).Count(n=>list.ItemContainerGenerator.ContainerFromIndex(n)!=null);
        results.Add(new{name="Fotogalerie mit 1000 synthetischen Eintraegen",total=1000,createdContainers=created,viewportHeight=list.ActualHeight,availableHeight=host.ActualHeight,galleryContentHeight=((FrameworkElement)gallery.Content).ActualHeight});
        host.Close();
        var result=JsonSerializer.Serialize(results,new JsonSerializerOptions {WriteIndented=true});
        File.WriteAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../results.json")),result);
        Console.WriteLine(result);
        app.Shutdown();
    }
    static double L(Color c) { double F(byte b) {var s=b/255d;return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4);} return .2126*F(c.R)+.7152*F(c.G)+.0722*F(c.B); }
    static double Contrast(Color a,Color b) => (Math.Max(L(a),L(b))+.05)/(Math.Min(L(a),L(b))+.05);
    sealed class TextCache(string path) : XtfJsonGeometryCache<string>(path,1,new JsonSerializerOptions())
    { public int Extractions {get;private set;} protected override IEnumerable<string> Extract(string path) { Extractions++; yield return File.ReadAllText(path); } }
}
