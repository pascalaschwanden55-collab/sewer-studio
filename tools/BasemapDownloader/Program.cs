// Laedt swisstopo-WMTS-Kacheln (ch.swisstopo.pixelkarte-farbe, EPSG:3857, JPEG)
// fuer ein WGS84-Bounding-Box nach {out}/{z}/{x}/{y}.jpeg.
// Fortsetzbar (ueberspringt vorhandene Dateien), parallel, mit Retry.
// swisstopo-Daten sind frei nutzbar (Open Government Data).
//
// Beispiel:
//   dotnet run -- --out c:\Sewer-Studio_KI_4.5\basemap_tiles\uri --minz 8 --maxz 18
using System.Diagnostics;
using System.Globalization;
using System.Net;

string outDir = "tiles";
// Kachel-URL mit Platzhaltern {z}/{x}/{y}; Default = swisstopo Landeskarte farbig.
string urlTemplate = "https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/3857/{z}/{x}/{y}.jpeg";
string ext = ".jpeg";
// Bounding-Box Kanton Uri (WGS84), leicht gepolstert.
double minLon = 8.40, minLat = 46.50, maxLon = 9.00, maxLat = 46.98;
int minZ = 8, maxZ = 18, concurrency = 8;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--out": outDir = args[++i]; break;
        case "--url": urlTemplate = args[++i]; break;
        case "--ext": ext = args[++i]; break;
        case "--minlon": minLon = Parse(args[++i]); break;
        case "--minlat": minLat = Parse(args[++i]); break;
        case "--maxlon": maxLon = Parse(args[++i]); break;
        case "--maxlat": maxLat = Parse(args[++i]); break;
        case "--minz": minZ = int.Parse(args[++i]); break;
        case "--maxz": maxZ = int.Parse(args[++i]); break;
        case "--concurrency": concurrency = int.Parse(args[++i]); break;
    }
}

static double Parse(string s) => double.Parse(s, CultureInfo.InvariantCulture);
static int Lon2X(double lon, int z) => (int)Math.Floor((lon + 180.0) / 360.0 * (1L << z));
static int Lat2Y(double lat, int z)
{
    double r = lat * Math.PI / 180.0;
    return (int)Math.Floor((1.0 - Math.Log(Math.Tan(r) + 1.0 / Math.Cos(r)) / Math.PI) / 2.0 * (1L << z));
}

var work = new List<(int z, int x, int y)>();
for (int z = minZ; z <= maxZ; z++)
{
    int x0 = Lon2X(minLon, z), x1 = Lon2X(maxLon, z);
    int y0 = Lat2Y(maxLat, z), y1 = Lat2Y(minLat, z); // Nordkante -> kleinere y
    for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
            work.Add((z, x, y));
}

Console.WriteLine($"Kanton Uri  bbox=({minLon},{minLat})-({maxLon},{maxLat})  z{minZ}-{maxZ}");
Console.WriteLine($"Gesamt {work.Count:N0} Kacheln  Ziel: {outDir}");

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("SewerStudio-BasemapDownloader/1.0");
var sem = new SemaphoreSlim(concurrency);
int done = 0, skipped = 0, failed = 0;
long bytes = 0;
var sw = Stopwatch.StartNew();
var tasks = new List<Task>(work.Count);

foreach (var t in work)
{
    await sem.WaitAsync();
    tasks.Add(Task.Run(async () =>
    {
        try
        {
            string dir = Path.Combine(outDir, t.z.ToString(), t.x.ToString());
            string file = Path.Combine(dir, t.y + ext);
            if (File.Exists(file) && new FileInfo(file).Length > 0)
            {
                Interlocked.Increment(ref skipped);
                return;
            }

            string url = urlTemplate
                .Replace("{z}", t.z.ToString())
                .Replace("{x}", t.x.ToString())
                .Replace("{y}", t.y.ToString());
            for (int attempt = 1; attempt <= 4; attempt++)
            {
                try
                {
                    using var resp = await http.GetAsync(url);
                    // 400/404 = Kachel ausserhalb der swisstopo-Abdeckung -> ueberspringen, nicht wiederholen.
                    if (resp.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
                    {
                        Interlocked.Increment(ref skipped);
                        return;
                    }
                    resp.EnsureSuccessStatusCode();
                    var data = await resp.Content.ReadAsByteArrayAsync();
                    Directory.CreateDirectory(dir);
                    await File.WriteAllBytesAsync(file, data);
                    Interlocked.Add(ref bytes, data.Length);
                    Interlocked.Increment(ref done);
                    return;
                }
                catch when (attempt < 4)
                {
                    await Task.Delay(400 * attempt);
                }
            }
            Interlocked.Increment(ref failed);
        }
        finally
        {
            sem.Release();
            int d = Volatile.Read(ref done) + Volatile.Read(ref skipped) + Volatile.Read(ref failed);
            if (d % 2000 == 0)
                Console.WriteLine($"{d,10:N0}/{work.Count:N0}  neu={done:N0} skip={skipped:N0} fail={failed:N0}  {bytes / 1024 / 1024:N0} MB  {sw.Elapsed:hh\\:mm\\:ss}");
        }
    }));
}

await Task.WhenAll(tasks);
Console.WriteLine($"FERTIG.  neu={done:N0} skip={skipped:N0} fail={failed:N0}  {bytes / 1024 / 1024:N0} MB  Dauer {sw.Elapsed:hh\\:mm\\:ss}");
return failed > 0 ? 1 : 0;
