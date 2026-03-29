using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Options;
using NewsAgent.Models;

namespace NewsAgent.Dashboard;

/// <summary>
/// Maps Minimal API endpoints for the web dashboard.
/// </summary>
public static partial class DashboardEndpoints
{
    /// <summary>
    /// Registers dashboard routes on the WebApplication.
    /// </summary>
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/", HandleHome);
        app.MapGet("/digest/{filename}", HandleDigestView);
        app.MapGet("/digest/{filename}/raw", HandleDigestRaw);
        app.MapGet("/status", HandleStatus);
    }

    private static IResult HandleHome(IOptions<DigestConfig> config)
    {
        var outputDir = config.Value.Output.FilePath;

        var digests = !Directory.Exists(outputDir)
            ? []
            : Directory.GetFiles(outputDir, "digest-*.html")
                .Select(path =>
                {
                    var fi = new FileInfo(path);
                    var fileName = fi.Name;
                    var date = ExtractDateFromFilename(fileName);
                    var articleCount = CountArticles(path);
                    var sizeKb = fi.Length / 1024.0;
                    return new DigestEntry(fileName, date, articleCount, sizeKb);
                })
                .OrderByDescending(d => d.FileName)
                .ToList();

        var html = RenderHomePage(digests);
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static IResult HandleDigestView(string filename, IOptions<DigestConfig> config)
    {
        if (!IsValidFilename(filename))
            return Results.NotFound();

        var outputDir = config.Value.Output.FilePath;
        var filePath = Path.Combine(outputDir, filename);

        if (!File.Exists(filePath))
            return Results.NotFound();

        var digestContent = File.ReadAllText(filePath);

        // Find prev/next digests for navigation
        var allFiles = Directory.GetFiles(outputDir, "digest-*.html")
            .Select(Path.GetFileName)
            .OrderByDescending(f => f)
            .ToList();

        var currentIndex = allFiles.IndexOf(filename);
        var newer = currentIndex > 0 ? allFiles[currentIndex - 1] : null;
        var older = currentIndex < allFiles.Count - 1 ? allFiles[currentIndex + 1] : null;

        var html = RenderDigestPage(filename, digestContent, newer, older);
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static IResult HandleDigestRaw(string filename, IOptions<DigestConfig> config)
    {
        if (!IsValidFilename(filename))
            return Results.NotFound();

        var outputDir = config.Value.Output.FilePath;
        var filePath = Path.Combine(outputDir, filename);

        if (!File.Exists(filePath))
            return Results.NotFound();

        return Results.File(filePath, "text/html; charset=utf-8", filename);
    }

    private static IResult HandleStatus(IOptions<DigestConfig> config, StatusTracker tracker)
    {
        var snapshot = tracker.GetSnapshot(config.Value.Output.FilePath);
        return Results.Json(snapshot);
    }

    private static string ExtractDateFromFilename(string fileName)
    {
        // digest-2026-03-29-1754.html → 2026. 03. 29. 17:54
        var match = DateRegex().Match(fileName);
        if (!match.Success) return fileName;

        var y = match.Groups[1].Value;
        var m = match.Groups[2].Value;
        var d = match.Groups[3].Value;
        var time = match.Groups[4].Value;
        var h = time[..2];
        var min = time[2..];
        return $"{y}. {m}. {d}. {h}:{min}";
    }

    private static int CountArticles(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            // Count <strong> tags inside <p> — each is a news item title
            return StrongInParagraphRegex().Matches(content).Count;
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsValidFilename(string filename) =>
        SafeFilenameRegex().IsMatch(filename);

    private static string RenderHomePage(List<DigestEntry> digests)
    {
        var cards = digests.Count == 0
            ? "<p class=\"empty\">Még nincs digest. Futtasd a <code>--run-once</code> parancsot az első generáláshoz.</p>"
            : string.Join("\n", digests.Select(d => $"""
                <a href="/digest/{HttpUtility.UrlEncode(d.FileName)}" class="card">
                    <div class="card-date">{HttpUtility.HtmlEncode(d.Date)}</div>
                    <div class="card-meta">{d.ArticleCount} cikk · {d.SizeKb:F0} KB</div>
                </a>
                """));

        return $$"""
            <!DOCTYPE html>
            <html lang="hu">
            <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>News Agent — Archívum</title>
            {{SharedCss}}
            <style>
            .cards { display: grid; gap: 12px; margin-top: 20px; }
            .card {
                display: block;
                background: #fff;
                border-radius: 8px;
                padding: 16px 20px;
                box-shadow: 0 1px 4px rgba(0,0,0,0.08);
                text-decoration: none;
                color: inherit;
                transition: box-shadow 0.15s;
            }
            .card:hover { box-shadow: 0 2px 12px rgba(0,0,0,0.15); }
            .card-date { font-size: 1.1em; font-weight: 600; color: #1a1a2e; }
            .card-meta { font-size: 0.85em; color: #888; margin-top: 4px; }
            .empty { color: #888; margin-top: 20px; }
            .status-link { margin-top: 24px; display: block; font-size: 0.85em; }
            </style>
            </head>
            <body>
            <main>
                <h1>📰 News Agent — Archívum</h1>
                <p>{{digests.Count}} digest érhető el</p>
                <div class="cards">
                    {{cards}}
                </div>
                <a href="/status" class="status-link">Rendszer státusz →</a>
            </main>
            </body>
            </html>
            """;
    }

    private static string RenderDigestPage(string filename, string digestContent, string? newer, string? older)
    {
        // Extract the <main>...</main> content from the digest HTML
        var mainMatch = MainContentRegex().Match(digestContent);
        var innerContent = mainMatch.Success
            ? mainMatch.Groups[1].Value
            : digestContent;

        var navPrev = older is not null
            ? $"""<a href="/digest/{HttpUtility.UrlEncode(older)}">← Régebbi</a>"""
            : "<span></span>";
        var navNext = newer is not null
            ? $"""<a href="/digest/{HttpUtility.UrlEncode(newer)}">Újabb →</a>"""
            : "<span></span>";

        return $$"""
            <!DOCTYPE html>
            <html lang="hu">
            <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Digest — {{HttpUtility.HtmlEncode(filename)}}</title>
            {{SharedCss}}
            <style>
            .nav { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
            .nav a { color: #1a6dd4; text-decoration: none; font-size: 0.9em; }
            .nav a:hover { text-decoration: underline; }
            .download { font-size: 0.85em; margin-top: 20px; }
            .download a { color: #1a6dd4; text-decoration: none; }
            .download a:hover { text-decoration: underline; }
            </style>
            </head>
            <body>
            <main>
                <div class="nav">
                    {{navPrev}}
                    <a href="/">← Vissza az archívumhoz</a>
                    {{navNext}}
                </div>
                <hr>
                {{innerContent}}
                <hr>
                <div class="nav">
                    {{navPrev}}
                    <span></span>
                    {{navNext}}
                </div>
                <div class="download">
                    <a href="/digest/{{HttpUtility.UrlEncode(filename)}}/raw" download>⬇ HTML letöltése</a>
                </div>
            </main>
            </body>
            </html>
            """;
    }

    private const string SharedCss = """
        <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background: #f4f4f7;
            color: #1d1d1f;
            line-height: 1.7;
            padding: 24px 16px;
        }
        main {
            max-width: 680px;
            margin: 0 auto;
            background: #ffffff;
            border-radius: 8px;
            padding: 32px 28px;
            box-shadow: 0 1px 4px rgba(0,0,0,0.08);
        }
        h1 { font-size: 1.6em; color: #1a1a2e; margin-bottom: 8px; }
        h2 { font-size: 1.2em; color: #1a1a2e; margin-top: 24px; margin-bottom: 12px; }
        p { margin-bottom: 12px; }
        a { color: #1a6dd4; text-decoration: none; }
        a:hover { text-decoration: underline; }
        hr { border: none; border-top: 1px solid #e0e0e6; margin: 20px 0; }
        @media (max-width: 600px) {
            body { padding: 12px 8px; }
            main { padding: 20px 16px; }
        }
        </style>
        """;

    [GeneratedRegex(@"digest-(\d{4})-(\d{2})-(\d{2})-(\d{4})\.html")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"<p[^>]*>\s*<strong>", RegexOptions.IgnoreCase)]
    private static partial Regex StrongInParagraphRegex();

    [GeneratedRegex(@"^digest-\d{4}-\d{2}-\d{2}-\d{4}\.html$")]
    private static partial Regex SafeFilenameRegex();

    [GeneratedRegex(@"<main>(.*?)</main>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex MainContentRegex();

    private record DigestEntry(string FileName, string Date, int ArticleCount, double SizeKb);
}
