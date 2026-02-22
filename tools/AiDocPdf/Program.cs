using System;
using System.IO;
using AuswertungPro.Next.Application.Reports;

var output = args.Length > 0
    ? args[0]
    : Path.Combine(Directory.GetCurrentDirectory(), "temp", $"KI_Uebersicht_{DateTime.Now:yyyyMMdd}.pdf");

var dir = Path.GetDirectoryName(output);
if (!string.IsNullOrWhiteSpace(dir))
    Directory.CreateDirectory(dir);

var builder = new AiDocumentationPdfBuilder();
var pdf = builder.BuildPdf(DateTimeOffset.Now);
File.WriteAllBytes(output, pdf);

Console.WriteLine(output);
